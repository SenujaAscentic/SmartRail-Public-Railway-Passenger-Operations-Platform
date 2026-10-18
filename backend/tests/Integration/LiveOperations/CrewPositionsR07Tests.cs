using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartRail.Identity;
using SmartRail.Identity.Persistence;
using SmartRail.IntegrationTests.Identity;
using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Persistence;
using Xunit;

namespace SmartRail.IntegrationTests.LiveOperations;

// R-07 (position acceptance), written BEFORE the endpoint: a position is accepted only from a registered cab device with an active
// cab session, for a journey that is assigned to that driver and is Running. Threat T-19 depends on this. Test names follow
// domain model section 10 where it has them.
//
// How each part is answered (contract section 6 and R-07):
//   - no cab session / ended session / deactivated device: 401 (Identity refuses the token itself, contract 6: "a token whose cab session has ended")
//   - a token without a cab session (a phone), a journey of another driver, a journey that is not Running: 403
[Collection("Api")]
public class CrewPositionsR07Tests(ApiFixture api) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateOnly Today = new(2026, 10, 14);

    private readonly HttpClient _client = api.Factory.NewClient();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await JourneyCleanup.RemoveAllAsync(api.ConnectionString);

    // ---- helpers ----

    private async Task<Guid> DriverId(string staffNumber)
    {
        using var scope = api.Factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<IStaffDirectory>().GetDriverIdsAsync([staffNumber], Ct))[staffNumber];
    }

    private async Task<string> CabToken(string device, string driver) =>
        await (await _client.CabSignInAsync(device, api.CabDeviceSecrets[device], driver, api.DemoPassword, api.Clock.GetUtcNow(), Ct)).AccessTokenAsync(Ct);

    // A journey of DRV-02's service 1004 (Kandy to Fort, 11:00 local), optionally already Running.
    private async Task<Guid> Journey(DateOnly date, bool running, string driver = "DRV-02")
    {
        var id = await DriverId(driver);
        return await RealJourneys.CreateAsync(api.Factory.Services, "1004", date, j =>
        {
            j.AssignDriver(id);
            if (running)
            {
                // Started at its own scheduled departure, which is always inside the 60-minute window.
                Assert.Equal(StartResult.Started, j.Start(j.Stops[0].ScheduledDeparture!.Value, Guid.NewGuid(), Guid.NewGuid()));
            }
        });
    }

    // One plausible position at Kandy station (the journey's origin): on the route, slow, stamped with the test clock.
    private async Task<HttpResponseMessage> PostPosition(Guid journeyId, string? token)
    {
        var kandy = (await RealJourneys.StationsAsync(api.Factory.Services, "KDT"))["KDT"];
        var station = await (await _client.GetAsync($"/api/v1/stations/{kandy}", Ct)).Content.ReadFromJsonAsync<JsonElement>(Ct);
        var now = api.Clock.GetUtcNow();
        var body = new
        {
            clientActionId = Guid.NewGuid(),
            positions = new[]
            {
                new
                {
                    lat = station.GetProperty("latitude").GetDouble(), lng = station.GetProperty("longitude").GetDouble(),
                    speedKmh = 0.0, accuracyM = 5.0, gpsTime = now, deviceTime = now,
                },
            },
        };
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/crew/journeys/{journeyId}/positions") { Content = JsonContent.Create(body) };
        if (token is not null)
        {
            request.Headers.Add("Authorization", "Bearer " + token);
        }

        return await _client.SendAsync(request, Ct);
    }

    private async Task<DateTime?> LastPositionAt(Guid journeyId)
    {
        using var scope = api.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiveOperationsDbContext>();
        return (await db.Journeys.AsNoTracking().SingleAsync(j => j.Id == journeyId, Ct)).LastPositionAt;
    }

    private static async Task<string?> CodeOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();

    // ---- the one case that must work ----

    [Fact]
    public async Task A_position_is_accepted_from_the_assigned_driver_on_a_running_journey_with_an_active_cab_session()
    {
        var id = await Journey(Today, running: true);
        var token = await CabToken("CAB-02", "DRV-02");

        var response = await PostPosition(id, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(api.Clock.GetUtcNow().UtcDateTime, await LastPositionAt(id));
    }

    // ---- R-07, part by part ----

    [Fact]
    public async Task Position_rejected_without_active_cab_session()
    {
        var id = await Journey(Today, running: true);
        var ended = await CabToken("CAB-02", "DRV-02");
        await CabToken("CAB-04", "DRV-02");   // the same driver signs in on another tablet: the first session ends (R-19)

        var response = await PostPosition(id, ended);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(await LastPositionAt(id));
    }

    [Fact]
    public async Task Position_rejected_without_any_token()
    {
        var id = await Journey(Today, running: true);

        var response = await PostPosition(id, token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(await LastPositionAt(id));
    }

    // "Registered": a tablet that is not in the seed, or has been taken out of service, cannot get a cab session at all (Identity refuses
    // its sign-in with the one generic 401), so it has no token to send positions with. (A tablet deactivated AFTER it signed in keeps its
    // token for at most one access-token life, 15 min: the documented design of threat T-12.) And a token that is not a cab token,
    // for example a controller's, is refused by the policy.
    [Fact]
    public async Task Position_rejected_from_unregistered_device()
    {
        var id = await Journey(Today, running: true);

        var unknownTablet = await _client.CabSignInAsync("CAB-99", "a-secret", "DRV-02", api.DemoPassword, api.Clock.GetUtcNow(), Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownTablet.StatusCode);

        await SetDeviceActive("CAB-02", false);
        try
        {
            var deactivated = await _client.CabSignInAsync("CAB-02", api.CabDeviceSecrets["CAB-02"], "DRV-02", api.DemoPassword, api.Clock.GetUtcNow(), Ct);
            Assert.Equal(HttpStatusCode.Unauthorized, deactivated.StatusCode);
        }
        finally
        {
            await SetDeviceActive("CAB-02", true);
        }

        var staff = await api.TokenForAsync("Controller", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostPosition(id, staff)).StatusCode);
        Assert.Null(await LastPositionAt(id));
    }

    [Fact]
    public async Task Position_rejected_from_a_phone_token_even_for_the_assigned_driver()
    {
        var id = await Journey(Today, running: true);
        var phone = await (await _client.CrewSignInAsync("DRV-02", api.DemoPassword, Ct)).AccessTokenAsync(Ct);

        var response = await PostPosition(id, phone);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(await LastPositionAt(id));
    }

    [Fact]
    public async Task Position_rejected_for_journey_not_assigned_to_driver()
    {
        var id = await Journey(Today, running: true);          // DRV-02's journey
        var other = await CabToken("CAB-01", "DRV-01");         // another driver with a perfectly good cab session

        var response = await PostPosition(id, other);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("OUT_OF_SCOPE", await CodeOf(response));
        Assert.Null(await LastPositionAt(id));
    }

    [Fact]
    public async Task Position_rejected_for_a_journey_that_has_no_driver()
    {
        var id = await RealJourneys.CreateAsync(api.Factory.Services, "1004", Today.AddDays(1), j =>
            j.Start(j.Stops[0].ScheduledDeparture!.Value, Guid.NewGuid(), Guid.NewGuid()));   // running, but nobody is assigned
        var token = await CabToken("CAB-02", "DRV-02");

        var response = await PostPosition(id, token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("OUT_OF_SCOPE", await CodeOf(response));
    }

    [Fact]
    public async Task Position_rejected_for_journey_not_running()
    {
        var planned = await Journey(Today.AddDays(2), running: false);
        var completed = await Journey(Today.AddDays(3), running: true);
        var token = await CabToken("CAB-02", "DRV-02");
        // End the second journey by the domain rule, the way the endpoint does.
        using (var scope = api.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiveOperationsDbContext>();
            var journey = await db.Journeys.Include(j => j.Stops).SingleAsync(j => j.Id == completed, Ct);
            journey.End(api.Clock.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync(Ct);
        }

        var notStarted = await PostPosition(planned, token);
        var ended = await PostPosition(completed, token);

        Assert.Equal(HttpStatusCode.Forbidden, notStarted.StatusCode);   // R-07: rejected (403)
        Assert.Equal(HttpStatusCode.Forbidden, ended.StatusCode);
        Assert.Null(await LastPositionAt(planned));
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    [InlineData("not-an-id")]
    public async Task Position_for_an_unknown_journey_is_404(string id)
    {
        var token = await CabToken("CAB-02", "DRV-02");
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/crew/journeys/{id}/positions")
        {
            Content = JsonContent.Create(new
            {
                clientActionId = Guid.NewGuid(),
                positions = new[] { new { lat = 7.29, lng = 80.63, speedKmh = 0.0, accuracyM = 5.0, gpsTime = api.Clock.GetUtcNow(), deviceTime = api.Clock.GetUtcNow() } },
            }),
        };
        request.Headers.Add("Authorization", "Bearer " + token);

        var response = await _client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task SetDeviceActive(string deviceId, bool active)
    {
        using var scope = api.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        var device = await db.CabDevices.SingleAsync(d => d.Code == deviceId, Ct);
        device.Active = active;
        await db.SaveChangesAsync(Ct);
    }
}
