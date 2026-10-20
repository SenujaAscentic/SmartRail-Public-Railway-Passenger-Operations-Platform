using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.EntityFrameworkCore;
using SmartRail.CateringIntegration.Persistence;
using SmartRail.CateringIntegration.Relay;
using SmartRail.IntegrationTests.Identity;
using SmartRail.IntegrationTests.LiveOperations;
using Testcontainers.PostgreSql;
using Xunit;

namespace SmartRail.IntegrationTests.CateringIntegration;

// Flow 5 end to end: delays and cancellations in SmartRail reach the provider as signed webhooks (see CateringTestBase).
[Collection("Api")]
public sealed class CateringRelayTests(ApiFixture api) : CateringTestBase(api)
{
    private readonly ApiFixture _api = api;

    private async Task<Guid> NewJourney() => await RealJourneys.CreateAsync(_smartRail.Services, "1004", Future);

    private async Task Decide(Guid id, string action, object body)
    {
        var token = await (await _client.SignInAsync("CTRL-01", _api.DemoPassword, Ct)).AccessTokenAsync(Ct);
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/ops/journeys/{id}/{action}") { Content = JsonContent.Create(body) };
        request.Headers.Add("Authorization", "Bearer " + token);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(request, Ct)).StatusCode);
    }

    // The outbox dispatcher runs on a real timer, so the event reaches the feed a moment after the decision: run rounds until it is there.
    private async Task<JsonElement> ProviderEventsAfter(Func<JsonElement, bool> done)
    {
        for (var i = 0; i < 100; i++)
        {
            await RunRoundAsync();
            var events = await _provider.CreateClient().GetFromJsonAsync<JsonElement>("/events.json", Ct);
            if (done(events))
            {
                return events;
            }

            await Task.Delay(100, Ct);
        }

        throw new Xunit.Sdk.XunitException("The provider did not receive the expected events.");
    }

    private static List<JsonElement> ForService(JsonElement events, string type) =>
        events.EnumerateArray().Where(e => e.GetProperty("serviceNumber").GetString() == "1004" && e.GetProperty("type").GetString() == type).ToList();

    [Fact]
    public async Task A_controller_delay_reaches_the_provider_as_a_signed_ETA_for_each_catering_station_and_no_other()
    {
        var id = await NewJourney();

        await Decide(id, "delay", new { minutes = 15, reasonCode = "TrainFault", note = "Engine failure", expectedVersion = 0 });
        var events = await ProviderEventsAfter(e => ForService(e, "provider.eta-updated").Count > 0);

        // Service 1004 calls at both catering stations of the seed (Polgahawela and Peradeniya), and at no other that has catering.
        var etas = ForService(events, "provider.eta-updated");
        Assert.Equal(["PDA", "PLG"], etas.Select(e => e.GetProperty("stationRef").GetString()).Order());
        Assert.All(etas, eta => Assert.Equal(15, (eta.GetProperty("expectedArrival").GetDateTime() - eta.GetProperty("scheduledArrival").GetDateTime()).TotalMinutes));
    }

    [Fact]
    public async Task A_second_delay_of_the_same_size_sends_nothing_new_and_a_changed_delay_sends_a_new_ETA()
    {
        var id = await NewJourney();
        await Decide(id, "delay", new { minutes = 15, reasonCode = "TrainFault", note = "x", expectedVersion = 0 });
        await ProviderEventsAfter(e => ForService(e, "provider.eta-updated").Count == 2);

        await Decide(id, "delay", new { minutes = 15, reasonCode = "TrainFault", note = "again", expectedVersion = 1 });
        await Decide(id, "delay", new { minutes = 25, reasonCode = "TrainFault", note = "worse", expectedVersion = 2 });
        var events = await ProviderEventsAfter(e => ForService(e, "provider.eta-updated").Count >= 4);

        await RunRoundAsync();
        Assert.Equal(4, ForService(events, "provider.eta-updated").Count);
    }

    [Fact]
    public async Task A_cancellation_reaches_the_provider_for_the_catering_stops_with_the_reason()
    {
        var id = await NewJourney();

        await Decide(id, "cancel", new { reasonCode = "TrainFault", note = "Engine failure", expectedVersion = 0 });
        var events = await ProviderEventsAfter(e => ForService(e, "provider.journey-changed").Count > 0);

        var changed = ForService(events, "provider.journey-changed");
        Assert.All(changed, c => Assert.Equal("Cancelled", c.GetProperty("change").GetString()));
        Assert.Contains(changed, c => c.GetProperty("stationRef").GetString() == "PLG");
        Assert.Contains("Engine failure", changed[0].GetProperty("reason").GetString());
    }

    [Fact]
    public async Task When_the_provider_is_down_the_webhook_is_kept_and_delivered_later_once()
    {
        var id = await NewJourney();
        _providerDown = true;
        await Decide(id, "delay", new { minutes = 15, reasonCode = "TrainFault", note = "x", expectedVersion = 0 });

        // Give the dispatcher time, then run rounds while the provider is down: the delivery fails and waits.
        List<Delivery> pending = [];
        for (var i = 0; i < 100 && pending.Count == 0; i++)
        {
            await RunRoundAsync();
            pending = await WithDeliveries(d => d.Where(x => x.Type == "provider.eta-updated").ToListAsync(Ct));
            await Task.Delay(100, Ct);
        }

        Assert.Equal(2, pending.Count);   // one per catering station
        Assert.All(pending, p =>
        {
            Assert.Equal(DeliveryStatus.Pending, p.Status);
            Assert.Equal(1, p.Attempts);
            Assert.Equal("Unreachable", p.LastError);
        });
        Assert.Empty(ForService(await _provider.CreateClient().GetFromJsonAsync<JsonElement>("/events.json", Ct), "provider.eta-updated"));

        // Too early: still waiting (10 s). Later, with the provider back: delivered, and the provider has it once.
        _clock.Advance(TimeSpan.FromSeconds(5));
        await RunRoundAsync();
        Assert.All(await Etas(), x => Assert.Equal(1, x.Attempts));

        _providerDown = false;
        _clock.Advance(TimeSpan.FromSeconds(10));
        await RunRoundAsync();

        Assert.All(await Etas(), x =>
        {
            Assert.Equal(DeliveryStatus.Delivered, x.Status);
            Assert.Equal(2, x.Attempts);
        });
        Assert.Equal(2, ForService(await _provider.CreateClient().GetFromJsonAsync<JsonElement>("/events.json", Ct), "provider.eta-updated").Count);   // once each
    }

    [Fact]
    public async Task After_24_hours_of_retries_the_delivery_is_marked_Failed_and_nothing_else_is_stopped()
    {
        var id = await NewJourney();
        _providerDown = true;
        await Decide(id, "delay", new { minutes = 15, reasonCode = "TrainFault", note = "x", expectedVersion = 0 });
        for (var i = 0; i < 100 && await WithDeliveries(d => d.CountAsync(Ct)) == 0; i++)
        {
            await RunRoundAsync();
            await Task.Delay(100, Ct);
        }

        // Four early retries, then one every 15 minutes: after 23 hours it is still being tried ...
        var first = _clock.GetUtcNow();
        while (_clock.GetUtcNow() - first < TimeSpan.FromHours(23))
        {
            _clock.Advance(TimeSpan.FromMinutes(1));
            await RunRoundAsync();
        }

        Assert.All(await Etas(), x => Assert.Equal(DeliveryStatus.Pending, x.Status));

        // ... and 24 hours after the first attempt it gives up.
        while (_clock.GetUtcNow() - first < TimeSpan.FromHours(25))
        {
            _clock.Advance(TimeSpan.FromMinutes(1));
            await RunRoundAsync();
        }

        Assert.All(await Etas(), x =>
        {
            Assert.Equal(DeliveryStatus.Failed, x.Status);
            Assert.InRange(x.Attempts, 90, 100);
            Assert.InRange(x.NextAttemptAt - x.FirstAttemptAt!.Value, TimeSpan.Zero, TimeSpan.FromHours(24));   // the last retry was inside the 24 hours
        });
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/v1/journeys/{id}", Ct)).StatusCode);   // train information is unaffected
    }

    private Task<List<Delivery>> Etas() => WithDeliveries(d => d.Where(x => x.Type == "provider.eta-updated").ToListAsync(Ct));

    private async Task<T> WithDeliveries<T>(Func<IQueryable<Delivery>, Task<T>> query)
    {
        using var scope = _smartRail.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<CateringDbContext>().Deliveries.AsNoTracking());
    }
}
