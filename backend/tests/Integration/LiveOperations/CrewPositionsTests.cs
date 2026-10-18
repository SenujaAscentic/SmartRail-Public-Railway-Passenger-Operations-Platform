using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartRail.Identity;
using SmartRail.IntegrationTests.Identity;
using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Persistence;
using Xunit;

namespace SmartRail.IntegrationTests.LiveOperations;

// POST /api/v1/crew/journeys/{id}/positions: what happens to the fixes of an accepted batch (R-08, R-09, R-04, R-06). Who may send
// them (R-07) is in CrewPositionsR07Tests. The journey is service 1004, Kandy to Fort, DRV-02's, already Running; the first
// stretch of its route is Kandy to Peradeniya. The test clock is 14 Oct 2026, 04:30 UTC.
[Collection("Api")]
public class CrewPositionsTests(ApiFixture api) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateOnly Today = new(2026, 10, 14);

    private readonly HttpClient _client = api.Factory.NewClient();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await JourneyCleanup.RemoveAllAsync(api.ConnectionString);

    private DateTimeOffset Now => api.Clock.GetUtcNow();

    // ---- helpers ----

    private async Task<(double Lat, double Lng)> Coordinates(string code)
    {
        var id = (await RealJourneys.StationsAsync(api.Factory.Services, code))[code];
        var station = await (await _client.GetAsync($"/api/v1/stations/{id}", Ct)).Content.ReadFromJsonAsync<JsonElement>(Ct);
        return (station.GetProperty("latitude").GetDouble(), station.GetProperty("longitude").GetDouble());
    }

    // A point on the straight route from Kandy to Peradeniya: 0 is Kandy, 1 is Peradeniya.
    private async Task<(double Lat, double Lng)> Along(double fraction)
    {
        var (kLat, kLng) = await Coordinates("KDT");
        var (pLat, pLng) = await Coordinates("PDA");
        return (kLat + (pLat - kLat) * fraction, kLng + (pLng - kLng) * fraction);
    }

    private async Task<Guid> RunningJourney(DateOnly? date = null)
    {
        var driver = (await DriverIds("DRV-02"))["DRV-02"];
        return await RealJourneys.CreateAsync(api.Factory.Services, "1004", date ?? Today, j =>
        {
            j.AssignDriver(driver);
            j.Start(j.Stops[0].ScheduledDeparture!.Value, Guid.NewGuid(), Guid.NewGuid());
        });
    }

    private async Task<IReadOnlyDictionary<string, Guid>> DriverIds(params string[] numbers)
    {
        using var scope = api.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IStaffDirectory>().GetDriverIdsAsync(numbers, Ct);
    }

    private async Task<string> CabToken(DateTimeOffset? deviceTime = null) =>
        await (await _client.CabSignInAsync("CAB-02", api.CabDeviceSecrets["CAB-02"], "DRV-02", api.DemoPassword, deviceTime ?? Now, Ct)).AccessTokenAsync(Ct);

    private static object Fix((double Lat, double Lng) at, DateTimeOffset time, double speed = 40, bool gps = true) =>
        new { lat = at.Lat, lng = at.Lng, speedKmh = speed, accuracyM = 5.0, gpsTime = gps ? time : (DateTimeOffset?)null, deviceTime = time };

    private Task<HttpResponseMessage> Post(Guid journeyId, string token, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/crew/journeys/{journeyId}/positions") { Content = JsonContent.Create(body) };
        request.Headers.Add("Authorization", "Bearer " + token);
        return _client.SendAsync(request, Ct);
    }

    private Task<HttpResponseMessage> PostFixes(Guid journeyId, string token, Guid? action, params object[] fixes) =>
        Post(journeyId, token, new { clientActionId = action ?? Guid.NewGuid(), positions = fixes });

    private static async Task<(int Accepted, List<(int Index, string Reason)> Discarded)> Result(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return (body.GetProperty("accepted").GetInt32(),
            body.GetProperty("discarded").EnumerateArray().Select(d => (d.GetProperty("index").GetInt32(), d.GetProperty("reason").GetString()!)).ToList());
    }

    private async Task<OperationalJourney> Load(Guid id)
    {
        using var scope = api.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiveOperationsDbContext>();
        return await db.Journeys.AsNoTracking().Include(j => j.Stops).SingleAsync(j => j.Id == id, Ct);
    }

    private async Task<List<string>> EventTypes(Guid journeyId)
    {
        using var scope = api.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiveOperationsDbContext>();
        return await db.OutboxEvents.AsNoTracking().Where(e => e.JourneyId == journeyId).OrderBy(e => e.Id).Select(e => e.Type).ToListAsync(Ct);
    }

    // ---- R-08: what is discarded, and why ----

    [Fact]
    public async Task Discarded_fixes_are_listed_with_their_place_in_the_request_and_the_reason_and_change_nothing()
    {
        var id = await RunningJourney();
        var token = await CabToken();
        var kandy = await Along(0);
        Assert.Equal(1, (await Result(await PostFixes(id, token, null, Fix(kandy, Now)))).Accepted);

        var result = await Result(await PostFixes(id, token, null,
            Fix(kandy, Now.AddSeconds(-10)),                         // 0: older than the last accepted fix
            Fix((kandy.Lat + 0.1, kandy.Lng), Now.AddSeconds(20)),   // 1: 11 km from the route
            Fix(await Along(0.3), Now.AddSeconds(10))));             // 2: 2 km along the route in 10 seconds (about 750 km/h)

        Assert.Equal(0, result.Accepted);
        Assert.Equal([(0, "OlderThanLastAccepted"), (1, "OffRoute"), (2, "ImpliedSpeedTooHigh")], result.Discarded);
        Assert.Equal(Now.UtcDateTime, (await Load(id)).LastPositionAt);   // still the first fix
    }

    // A tablet that was in a tunnel sends its fixes together, not necessarily in order.
    [Fact]
    public async Task A_batch_is_worked_through_oldest_first_so_an_unordered_batch_is_not_thrown_away()
    {
        var id = await RunningJourney();
        var token = await CabToken();

        var result = await Result(await PostFixes(id, token, null,
            Fix(await Along(0.002), Now.AddSeconds(20)),
            Fix(await Along(0.000), Now),
            Fix(await Along(0.001), Now.AddSeconds(10))));

        Assert.Equal(3, result.Accepted);
        Assert.Empty(result.Discarded);
        Assert.Equal(Now.AddSeconds(20).UtcDateTime, (await Load(id)).LastPositionAt);
    }

    // ---- R-09: the time of a fix ----

    [Fact]
    public async Task The_time_of_a_fix_is_the_gps_time_else_the_tablet_clock_corrected_by_the_offset_measured_at_sign_in()
    {
        var id = await RunningJourney();
        var fastTablet = await CabToken(deviceTime: Now.AddMinutes(7));   // this tablet's clock runs 7 minutes ahead: offset -7 min
        var kandy = await Along(0);

        // No GPS time: the tablet says 04:37, the corrected time is 04:30.
        await Result(await PostFixes(id, fastTablet, null, Fix(kandy, Now.AddMinutes(7), gps: false)));
        Assert.Equal(Now.UtcDateTime, (await Load(id)).LastPositionAt);

        // With a GPS time, that is used, whatever the tablet clock says.
        await Result(await PostFixes(id, fastTablet, null, new
        {
            lat = kandy.Lat, lng = kandy.Lng, speedKmh = 0.0, accuracyM = 5.0, gpsTime = Now.AddSeconds(30), deviceTime = Now.AddMinutes(40),
        }));
        Assert.Equal(Now.AddSeconds(30).UtcDateTime, (await Load(id)).LastPositionAt);
    }

    // ---- the Domain does the work: stops, freshness, events ----

    [Fact]
    public async Task Fixes_at_a_station_arrive_and_depart_the_train_and_the_events_are_announced()
    {
        var id = await RunningJourney();
        var token = await CabToken();

        await Result(await PostFixes(id, token, null,
            Fix(await Along(0.0), Now, speed: 3),                    // slow inside Kandy's zone ...
            Fix(await Along(0.0), Now.AddSeconds(25), speed: 0),     // ... for 20 seconds: Arrived (timed when it stopped)
            Fix(await Along(0.1), Now.AddSeconds(60), speed: 30)));  // 700 m along the route: left the zone, Departed

        var journey = await Load(id);
        var origin = journey.Stops.OrderBy(s => s.Sequence).First();
        Assert.Equal(StopStatus.Departed, origin.Status);
        Assert.Equal(Now.UtcDateTime, origin.ActualArrival);
        Assert.Equal(Now.AddSeconds(60).UtcDateTime, origin.ActualDeparture);
        Assert.Equal(TrackingStatus.Live, journey.Tracking);
        var types = await EventTypes(id);
        Assert.Contains("DataFreshnessChanged", types);   // NotStarted -> Live
        Assert.Equal(2, types.Count(t => t == "StopStatusChanged"));   // Arrived, then Departed
    }

    // ---- the stream hears the train move ----

    [Fact]
    public async Task One_TrainPositionUpdated_is_announced_per_batch_with_the_stations_around_the_latest_fix()
    {
        var id = await RunningJourney();
        var token = await CabToken();
        using var stream = await SseClient.ConnectAsync(api.Factory.CreateClient(), $"/api/v1/stream?journeyIds={id}", null, Ct);
        await stream.NextEventAsync(TimeSpan.FromSeconds(5), Ct);   // the snapshot
        var kandy = (await RealJourneys.StationsAsync(api.Factory.Services, "KDT"))["KDT"];
        var peradeniya = (await RealJourneys.StationsAsync(api.Factory.Services, "PDA"))["PDA"];

        // 100 seconds apart and 15% of the way (about a kilometre) each: a plausible 38 km/h, so R-08 keeps all three.
        var result = await Result(await PostFixes(id, token, null,
            Fix(await Along(0.20), Now, speed: 50),
            Fix(await Along(0.50), Now.AddSeconds(200), speed: 62.5),    // the latest: halfway from Kandy to Peradeniya
            Fix(await Along(0.35), Now.AddSeconds(100), speed: 55)));
        Assert.Equal(3, result.Accepted);

        var types = new List<string>();
        SseFrame? position = null;
        while (position is null)
        {
            var frame = (await stream.NextEventAsync(TimeSpan.FromSeconds(5), Ct))!;
            types.Add(frame.Event!);
            position = frame.Event == "TrainPositionUpdated" ? frame : null;
        }

        var payload = position.Json.GetProperty("payload");
        Assert.Equal(kandy, payload.GetProperty("lastStationId").GetGuid());
        Assert.Equal(peradeniya, payload.GetProperty("nextStationId").GetGuid());
        Assert.Equal(0.5, payload.GetProperty("fractionToNext").GetDouble(), precision: 2);
        Assert.Equal(62.5, payload.GetProperty("speedKmh").GetDouble());
        Assert.DoesNotContain("source", position.Data!, StringComparison.OrdinalIgnoreCase);
        Assert.Single((await EventTypes(id)), t => t == "TrainPositionUpdated");   // one for the batch, not one per fix
    }

    [Fact]
    public async Task A_batch_in_which_nothing_is_accepted_announces_no_position()
    {
        var id = await RunningJourney();
        var token = await CabToken();
        var kandy = await Along(0);

        await Result(await PostFixes(id, token, null, Fix((kandy.Lat + 0.1, kandy.Lng), Now)));   // 11 km off the route

        Assert.DoesNotContain("TrainPositionUpdated", await EventTypes(id));
    }

    [Fact]
    public async Task The_journey_detail_shows_the_last_announced_position_with_the_same_fields_as_the_event_and_null_before_any()
    {
        var id = await RunningJourney();
        var kandy = (await RealJourneys.StationsAsync(api.Factory.Services, "KDT"))["KDT"];
        var peradeniya = (await RealJourneys.StationsAsync(api.Factory.Services, "PDA"))["PDA"];

        var before = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/journeys/{id}", Ct);
        Assert.Equal(JsonValueKind.Null, before.GetProperty("freshness").GetProperty("lastPosition").ValueKind);

        await Result(await PostFixes(id, await CabToken(), null, Fix(await Along(0.5), Now, speed: 62.5)));

        var position = (await _client.GetFromJsonAsync<JsonElement>($"/api/v1/journeys/{id}", Ct)).GetProperty("freshness").GetProperty("lastPosition");
        Assert.Equal(kandy, position.GetProperty("lastStationId").GetGuid());
        Assert.Equal(peradeniya, position.GetProperty("nextStationId").GetGuid());
        Assert.Equal(0.5, position.GetProperty("fractionToNext").GetDouble(), precision: 2);
        Assert.Equal(62.5, position.GetProperty("speedKmh").GetDouble());
        Assert.Equal((await Along(0.5)).Lat, position.GetProperty("latitude").GetDouble(), precision: 6);
    }

    // ---- repeats and validation ----

    [Fact]
    public async Task Sending_a_batch_again_with_the_same_clientActionId_gets_the_original_answer_and_changes_nothing()
    {
        var id = await RunningJourney();
        var token = await CabToken();
        var action = Guid.NewGuid();
        var first = await PostFixes(id, token, action, Fix(await Along(0), Now));

        var again = await PostFixes(id, token, action, Fix(await Along(0.001), Now.AddSeconds(30)));   // a different fix, the same action id

        Assert.Equal(await first.Content.ReadAsStringAsync(Ct), await again.Content.ReadAsStringAsync(Ct));
        Assert.Equal(Now.UtcDateTime, (await Load(id)).LastPositionAt);   // the second fix was never looked at
    }

    [Theory]
    [InlineData("noAction", "clientActionId")]
    [InlineData("empty", "positions")]
    [InlineData("tooMany", "positions")]
    [InlineData("badLat", "positions[0].lat")]
    [InlineData("noDeviceTime", "positions[0].deviceTime")]
    [InlineData("negativeSpeed", "positions[0].speedKmh")]
    public async Task A_badly_shaped_batch_is_a_400_naming_the_field(string problem, string field)
    {
        var id = await RunningJourney();
        var token = await CabToken();
        var good = Fix(await Along(0), Now);
        object body = problem switch
        {
            "noAction" => new { positions = new[] { good } },
            "empty" => new { clientActionId = Guid.NewGuid(), positions = Array.Empty<object>() },
            "tooMany" => new { clientActionId = Guid.NewGuid(), positions = Enumerable.Repeat(good, 101).ToArray() },
            "badLat" => new { clientActionId = Guid.NewGuid(), positions = new[] { new { lat = 91.0, lng = 80.0, speedKmh = 0.0, deviceTime = Now } } },
            "noDeviceTime" => new { clientActionId = Guid.NewGuid(), positions = new[] { new { lat = 7.0, lng = 80.0, speedKmh = 0.0 } } },
            _ => new { clientActionId = Guid.NewGuid(), positions = new[] { new { lat = 7.0, lng = 80.0, speedKmh = -5.0, deviceTime = Now } } },
        };

        var response = await Post(id, token, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("VALIDATION_FAILED", json.GetProperty("code").GetString());
        Assert.Contains(field, json.GetProperty("errors").EnumerateObject().Select(p => p.Name));
        Assert.Null((await Load(id)).LastPositionAt);
    }

    // ---- R-18: batches that overlap in time ----

    [Fact]
    public async Task Batches_that_arrive_at_the_same_moment_do_not_lose_each_other_and_the_latest_fix_wins()
    {
        var id = await RunningJourney();
        var token = await CabToken();
        var fixes = await Task.WhenAll(Enumerable.Range(0, 4).Select(async i => Fix(await Along(0.001 * i), Now.AddSeconds(10 * i))));

        var responses = await Task.WhenAll(fixes.Select(f => PostFixes(id, token, null, f)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var results = await Task.WhenAll(responses.Select(Result));
        Assert.Equal(4, results.Sum(r => r.Accepted + r.Discarded.Count));   // every fix was judged exactly once
        Assert.Equal(Now.AddSeconds(30).UtcDateTime, (await Load(id)).LastPositionAt);
    }
}
