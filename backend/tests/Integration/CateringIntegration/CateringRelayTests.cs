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

// Flow 5 end to end: the controller delays or cancels a train in SmartRail, and the provider (RailBites, its own service and its
// own database) receives a signed webhook about the catering station. Both applications run in memory, on one clock, each with its
// own scratch database; SmartRail's HTTP calls to the provider go straight to RailBites' test server.
[Collection("Api")]
public sealed class CateringRelayTests(ApiFixture api) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string Secret = "test-shared-secret-value";
    private static readonly DateOnly Future = new(2027, 3, 5);

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 14, 4, 30, 0, TimeSpan.Zero));
    private readonly PostgreSqlContainer _providerDatabase = new PostgreSqlBuilder("postgres:17").WithDatabase("railbites").Build();
    private WebApplicationFactory<RailBitesApp> _provider = null!;
    private WebApplicationFactory<Program> _smartRail = null!;
    private HttpClient _client = null!;
    private RelayService _relay = null!;
    private bool _providerDown;

    public async ValueTask InitializeAsync()
    {
        await _providerDatabase.StartAsync();
        _provider = new WebApplicationFactory<RailBitesApp>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:RailBites", _providerDatabase.GetConnectionString());
            builder.UseSetting("RailBites:SharedSecret", Secret);
            builder.UseSetting("RailBites:ApiKey", "test-api-key-value");
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(_clock));
        });
        _ = _provider.Server;

        var database = await api.CreateDatabaseAsync("catering_relay_" + Guid.NewGuid().ToString("N")[..8]);
        _smartRail = api.CreateFactory(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Catering:ProviderUrl"] = "http://railbites.test",
                ["Catering:SharedSecret"] = Secret,
                ["Catering:RelayEnabled"] = "false",   // the tests run the rounds themselves
            }));
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<TimeProvider>(_clock);
                services.AddHttpClient("railbites").ConfigurePrimaryHttpMessageHandler(() => new ProviderSwitch(_provider.Server.CreateHandler(), () => _providerDown));
            });
        }, connectionString: database);
        _ = _smartRail.Server;

        _client = _smartRail.NewClient();
        _relay = _smartRail.Services.GetRequiredService<RelayService>();
        await _relay.RunOnceAsync(Ct);   // the first round only sets the cursor to "now"
    }

    public async ValueTask DisposeAsync()
    {
        await _smartRail.DisposeAsync();
        await _provider.DisposeAsync();
        await _providerDatabase.DisposeAsync();
    }

    // Makes the provider unreachable the way a real outage does.
    private sealed class ProviderSwitch(HttpMessageHandler inner, Func<bool> down) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            down() ? throw new HttpRequestException("provider down") : base.SendAsync(request, cancellationToken);
    }

    private async Task<Guid> NewJourney() => await RealJourneys.CreateAsync(_smartRail.Services, "1004", Future);

    private async Task Decide(Guid id, string action, object body)
    {
        var token = await (await _client.SignInAsync("CTRL-01", api.DemoPassword, Ct)).AccessTokenAsync(Ct);
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/ops/journeys/{id}/{action}") { Content = JsonContent.Create(body) };
        request.Headers.Add("Authorization", "Bearer " + token);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(request, Ct)).StatusCode);
    }

    // The outbox dispatcher runs on a real timer, so the event reaches the feed a moment after the decision: run rounds until it is there.
    private async Task<JsonElement> ProviderEventsAfter(Func<JsonElement, bool> done)
    {
        for (var i = 0; i < 100; i++)
        {
            await _relay.RunOnceAsync(Ct);
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

        await _relay.RunOnceAsync(Ct);
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
            await _relay.RunOnceAsync(Ct);
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
        await _relay.RunOnceAsync(Ct);
        Assert.All(await Etas(), x => Assert.Equal(1, x.Attempts));

        _providerDown = false;
        _clock.Advance(TimeSpan.FromSeconds(10));
        await _relay.RunOnceAsync(Ct);

        Assert.All(await Etas(), x =>
        {
            Assert.Equal(DeliveryStatus.Delivered, x.Status);
            Assert.Equal(2, x.Attempts);
        });
        Assert.Equal(2, ForService(await _provider.CreateClient().GetFromJsonAsync<JsonElement>("/events.json", Ct), "provider.eta-updated").Count);   // once each
    }

    [Fact]
    public async Task After_the_whole_retry_schedule_the_delivery_is_marked_Failed_and_nothing_else_is_stopped()
    {
        var id = await NewJourney();
        _providerDown = true;
        await Decide(id, "delay", new { minutes = 15, reasonCode = "TrainFault", note = "x", expectedVersion = 0 });
        for (var i = 0; i < 100 && await WithDeliveries(d => d.CountAsync(Ct)) == 0; i++)
        {
            await _relay.RunOnceAsync(Ct);
            await Task.Delay(100, Ct);
        }

        foreach (var wait in RelayService.RetryDelays)
        {
            _clock.Advance(wait);
            await _relay.RunOnceAsync(Ct);
        }

        Assert.All(await Etas(), x =>
        {
            Assert.Equal(DeliveryStatus.Failed, x.Status);
            Assert.Equal(RelayService.RetryDelays.Length + 1, x.Attempts);
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
