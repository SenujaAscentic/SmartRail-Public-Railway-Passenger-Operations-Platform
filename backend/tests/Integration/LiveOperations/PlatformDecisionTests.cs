using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SmartRail.IntegrationTests.Identity;
using SmartRail.LiveOperations.Domain;
using Xunit;

namespace SmartRail.IntegrationTests.LiveOperations;

// The platform change (contract 3.5, US-O5 a star story, R-11 to R-13). The test clock is 14 Oct 2026, 04:30 UTC = 10:00 in Colombo.
// Service 1002 (Kandy to Fort) leaves Kandy at 10:00, reaches Peradeniya at 10:05 and Polgahawela at 10:59. The journey used is
// YESTERDAY's (the generator made today's already) with a controller delay of exactly 24 hours, so every time falls on today and the
// lead times are: Kandy 0 min (Late), Peradeniya 5 min (ShortNotice), Polgahawela 59 min (Normal). Its control version is therefore 1.
[Collection("Api")]
public class PlatformDecisionTests(ApiFixture api) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateOnly Today = new(2026, 10, 14);
    private static readonly DateOnly Yesterday = new(2026, 10, 13);

    private const int Version = 1;   // the delay that moved the journey a day forward was the first decision

    private readonly HttpClient _client = api.Factory.NewClient();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await JourneyCleanup.RemoveAllAsync(api.ConnectionString);

    private Task<Guid> Journey(int delayMinutes = 1440, Action<OperationalJourney>? change = null) =>
        RealJourneys.CreateAsync(api.Factory.Services, "1002", Yesterday, j =>
        {
            j.SetDelayByController(0, new Delay(delayMinutes), new JourneyReason("Other", null), Guid.NewGuid(), api.Clock.GetUtcNow().UtcDateTime);
            change?.Invoke(j);
        });

    private async Task<Guid> Station(string code) => (await RealJourneys.StationsAsync(api.Factory.Services, code))[code];

    private async Task<HttpResponseMessage> Post(string path, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Authorization", "Bearer " + await api.TokenForAsync("Controller", Ct));
        return await _client.SendAsync(request, Ct);
    }

    private Task<HttpResponseMessage> Preview(Guid id, Guid station, string platform) =>
        Post($"/api/v1/ops/journeys/{id}/stops/{station}/platform/preview", new { platform });

    private Task<HttpResponseMessage> Change(Guid id, Guid station, string platform, int version, bool setHold = false, int? holdMinutes = null, bool acknowledged = false) =>
        Post($"/api/v1/ops/journeys/{id}/stops/{station}/platform", new { platform, setHold, holdMinutes, conflictsAcknowledged = acknowledged, expectedVersion = version });

    private static async Task<JsonElement> Body(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    private static JsonElement StopOf(JsonElement journey, Guid station) =>
        journey.GetProperty("stops").EnumerateArray().Single(s => s.GetProperty("stationId").GetGuid() == station);

    private static async Task<JsonElement> NextOf(SseClient stream, string type)
    {
        SseFrame? frame;
        do
        {
            frame = await stream.NextEventAsync(TimeSpan.FromSeconds(5), Ct);
            Assert.NotNull(frame);
        }
        while (frame!.Event != type);
        return frame.Json;
    }

    // ---- the change ----

    [Fact]
    public async Task A_platform_change_with_plenty_of_notice_shows_the_new_platform_and_the_old_one_and_is_announced_as_Normal()
    {
        var id = await Journey();
        var polgahawela = await Station("PLG");
        using var stream = await SseClient.ConnectAsync(api.Factory.CreateClient(), $"/api/v1/stream?journeyIds={id}", null, Ct);
        await stream.NextEventAsync(TimeSpan.FromSeconds(5), Ct);   // the snapshot

        var response = await Change(id, polgahawela, "3", version: Version);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Body(response);
        var stop = StopOf(body, polgahawela);
        Assert.Equal(("3", "2", "2"), (stop.GetProperty("platform").GetString(), stop.GetProperty("previousPlatform").GetString(), stop.GetProperty("plannedPlatform").GetString()));
        Assert.Equal(Version + 1, body.GetProperty("version").GetInt32());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("hold").ValueKind);
        Assert.Equal(body.GetRawText(), (await _client.GetFromJsonAsync<JsonElement>($"/api/v1/journeys/{id}", Ct)).GetRawText());

        var changed = (await NextOf(stream, "PlatformChanged")).GetProperty("payload");
        Assert.Equal("Normal", changed.GetProperty("leadTimeLevel").GetString());
        Assert.False(changed.GetProperty("holdSet").GetBoolean());
        Assert.False(changed.TryGetProperty("leadTimeMinutes", out _));   // staff and audit only
    }

    // ★ US-O5 AC5: under 5 minutes the controller is warned and MAY hold; without a hold nothing promises the train waits.
    [Fact]
    public async Task Under_5_minutes_the_preview_recommends_a_hold_and_the_change_goes_through_without_one_making_no_promise()
    {
        var id = await Journey();
        var kandy = await Station("KDT");

        var preview = await Body(await Preview(id, kandy, "2"));
        Assert.Equal("Late", preview.GetProperty("leadTimeLevel").GetString());
        Assert.True(preview.GetProperty("holdRecommended").GetBoolean());
        Assert.Equal(0, preview.GetProperty("leadTimeMinutes").GetInt32());

        using var stream = await SseClient.ConnectAsync(api.Factory.CreateClient(), $"/api/v1/stream?journeyIds={id}", null, Ct);
        await stream.NextEventAsync(TimeSpan.FromSeconds(5), Ct);
        var response = await Change(id, kandy, "2", version: Version);   // no hold

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));   // warned, not blocked
        Assert.Equal(JsonValueKind.Null, (await Body(response)).GetProperty("hold").ValueKind);
        var changed = (await NextOf(stream, "PlatformChanged")).GetProperty("payload");
        Assert.Equal("Late", changed.GetProperty("leadTimeLevel").GetString());
        Assert.False(changed.GetProperty("holdSet").GetBoolean());   // "the train will wait" must not be said
    }

    [Fact]
    public async Task Under_5_minutes_with_a_hold_sets_the_hold_and_the_announcement_says_so()
    {
        var id = await Journey();
        var kandy = await Station("KDT");
        using var stream = await SseClient.ConnectAsync(api.Factory.CreateClient(), $"/api/v1/stream?journeyIds={id}", null, Ct);
        await stream.NextEventAsync(TimeSpan.FromSeconds(5), Ct);

        var response = await Change(id, kandy, "2", version: Version, setHold: true, holdMinutes: 6);

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        var body = await Body(response);
        Assert.Equal(kandy, body.GetProperty("hold").GetProperty("stationId").GetGuid());
        Assert.Equal(6, body.GetProperty("hold").GetProperty("minutes").GetInt32());
        Assert.Equal(Version + 1, body.GetProperty("version").GetInt32());   // one decision
        Assert.True((await NextOf(stream, "PlatformChanged")).GetProperty("payload").GetProperty("holdSet").GetBoolean());
        Assert.False((await NextOf(stream, "JourneyHeld")).GetProperty("payload").GetProperty("released").GetBoolean());
    }

    [Fact]
    public async Task The_preview_at_5_minutes_is_short_notice_and_changes_nothing()
    {
        var id = await Journey();

        var preview = await Body(await Preview(id, await Station("PDA"), "3"));

        Assert.Equal("ShortNotice", preview.GetProperty("leadTimeLevel").GetString());
        Assert.False(preview.GetProperty("holdRecommended").GetBoolean());
        Assert.Equal(5, preview.GetProperty("leadTimeMinutes").GetInt32());
        Assert.Empty(preview.GetProperty("conflicts").EnumerateArray());
        Assert.Equal(Version, (await _client.GetFromJsonAsync<JsonElement>($"/api/v1/journeys/{id}", Ct)).GetProperty("version").GetInt32());
    }

    // ---- refusals ----

    [Fact]
    public async Task A_hold_with_the_change_needs_minutes_and_a_platform_is_required()
    {
        var id = await Journey();
        var polgahawela = await Station("PLG");

        var noMinutes = await Change(id, polgahawela, "3", Version, setHold: true);
        Assert.Equal(HttpStatusCode.BadRequest, noMinutes.StatusCode);
        Assert.True((await Body(noMinutes)).GetProperty("errors").TryGetProperty("holdMinutes", out _));

        var noPlatform = await Post($"/api/v1/ops/journeys/{id}/stops/{polgahawela}/platform", new { expectedVersion = Version });
        Assert.True((await Body(noPlatform)).GetProperty("errors").TryGetProperty("platform", out _));
    }

    [Fact]
    public async Task Platform_change_refused_for_platform_not_at_station()
    {
        var id = await Journey();
        var polgahawela = await Station("PLG");

        var response = await Change(id, polgahawela, "99", Version);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("RULE_VIOLATION", (await Body(response)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Preview(id, polgahawela, "99")).StatusCode);
        Assert.Equal(Version, (await _client.GetFromJsonAsync<JsonElement>($"/api/v1/journeys/{id}", Ct)).GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task Changing_to_the_platform_the_stop_already_has_is_a_422()
    {
        var id = await Journey();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Change(id, await Station("PLG"), "2", Version)).StatusCode);
    }

    [Fact]
    public async Task Platform_change_refused_for_departed_stop()
    {
        var kandy = await Station("KDT");
        var id = await Journey(change: j => j.RecordCrewTime(kandy, StopEventKind.Departure, j.Stops[0].ScheduledDeparture!.Value));

        var response = await Change(id, kandy, "2", Version);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("INVALID_STATE", (await Body(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_station_that_is_not_on_the_journey_is_a_404_and_a_stale_version_is_a_409()
    {
        var id = await Journey();

        Assert.Equal(HttpStatusCode.NotFound, (await Change(id, Guid.NewGuid(), "3", Version)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Preview(id, Guid.NewGuid(), "3")).StatusCode);

        var stale = await Change(id, await Station("PLG"), "3", version: 7);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("VERSION_CONFLICT", (await Body(stale)).GetProperty("code").GetString());
    }

    // ---- conflicts (R-12) ----

    // 1004 reaches Polgahawela at 11:59 on platform 3 (set up here). 1002, a day and an hour late, would arrive there at 11:59 as well.
    private async Task<(Guid Delayed, Guid Other)> ConflictSetUp()
    {
        var polgahawela = await Station("PLG");
        var other = await RealJourneys.CreateAsync(api.Factory.Services, "1004", Today, j =>
            j.ChangePlatformByController(0, polgahawela, "3", ["1", "2", "3", "4", "5"], null, Guid.NewGuid(), api.Clock.GetUtcNow().UtcDateTime));
        var delayed = await Journey(delayMinutes: 1440 + 60);   // Polgahawela at 11:59 today, like the other train
        return (delayed, other);
    }

    [Fact]
    public async Task The_preview_names_another_train_expected_at_the_same_platform_within_5_minutes()
    {
        var (delayed, other) = await ConflictSetUp();

        var preview = await Body(await Preview(delayed, await Station("PLG"), "3"));

        var conflict = Assert.Single(preview.GetProperty("conflicts").EnumerateArray());
        Assert.Equal(other, conflict.GetProperty("journeyId").GetGuid());
        Assert.Equal("1004", conflict.GetProperty("serviceNumber").GetString());
        Assert.Equal("3", conflict.GetProperty("platform").GetString());
    }

    [Fact]
    public async Task A_change_into_a_conflict_is_refused_422_until_the_controller_acknowledges_it()
    {
        var (delayed, _) = await ConflictSetUp();
        var polgahawela = await Station("PLG");

        var refused = await Change(delayed, polgahawela, "3", version: Version);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        var body = await Body(refused);
        Assert.Equal("CONFLICT_NOT_ACKNOWLEDGED", body.GetProperty("code").GetString());
        Assert.Equal("1004", body.GetProperty("conflicts")[0].GetProperty("serviceNumber").GetString());
        Assert.Equal(Version, (await _client.GetFromJsonAsync<JsonElement>($"/api/v1/journeys/{delayed}", Ct)).GetProperty("version").GetInt32());   // nothing happened

        var acknowledged = await Change(delayed, polgahawela, "3", version: Version, acknowledged: true);
        Assert.Equal(HttpStatusCode.OK, acknowledged.StatusCode);
        Assert.Equal(Version + 1, (await Body(acknowledged)).GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task A_stale_version_is_reported_before_a_conflict()
    {
        var (delayed, _) = await ConflictSetUp();

        var response = await Change(delayed, await Station("PLG"), "3", version: 0);   // the delay made it 1, so 0 is stale   // the delay made it 1

        Assert.Equal("VERSION_CONFLICT", (await Body(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_train_two_hours_away_on_the_same_platform_is_no_conflict()
    {
        var polgahawela = await Station("PLG");
        await RealJourneys.CreateAsync(api.Factory.Services, "1004", Today, j =>
            j.ChangePlatformByController(0, polgahawela, "3", ["1", "2", "3"], null, Guid.NewGuid(), api.Clock.GetUtcNow().UtcDateTime));   // 11:59
        var id = await Journey();   // 10:59: an hour before

        Assert.Empty((await Body(await Preview(id, polgahawela, "3"))).GetProperty("conflicts").EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, (await Change(id, polgahawela, "3", Version)).StatusCode);
    }
}
