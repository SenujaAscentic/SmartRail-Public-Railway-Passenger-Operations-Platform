using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using SmartRail.LiveOperations.Domain;
using Microsoft.Extensions.DependencyInjection;
using SmartRail.IntegrationTests.Identity;
using Xunit;

namespace SmartRail.IntegrationTests.LiveOperations;

// GET /ops/stream (contract section 4): the public events plus `source`, for staff; a station manager sees only their station;
// the stream ends when the access token expires (US-O3 star criterion: the ops screens show who supplied each value).
[Collection("Api")]
public class OpsStreamTests(ApiFixture api) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateOnly Day = new(2027, 3, 10);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await JourneyCleanup.RemoveAllAsync(api.ConnectionString);

    private Task<SseClient> Connect(string token, string query) =>
        SseClient.ConnectAsync(api.Factory.NewClient(), "/api/v1/ops/stream" + query, null, Ct, token);

    private async Task<string> StaffToken(string staffNumber) =>
        await (await api.Factory.NewClient().SignInAsync(staffNumber, api.DemoPassword, Ct)).AccessTokenAsync(Ct);

    private async Task Post(string path, object body, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Authorization", "Bearer " + token);
        var response = await api.Factory.NewClient().SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

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

    [Fact]
    public async Task The_staff_stream_adds_source_to_each_event_and_the_public_stream_still_does_not()
    {
        var journeyId = await RealJourneys.CreateAsync(api.Factory.Services, "1004", Day);
        var controller = await StaffToken("CTRL-01");
        using var staff = await Connect(controller, $"?journeyIds={journeyId}");
        using var open = await SseClient.ConnectAsync(api.Factory.CreateClient(), $"/api/v1/stream?journeyIds={journeyId}", null, Ct);
        Assert.Equal(HttpStatusCode.OK, staff.Status);
        await staff.NextEventAsync(TimeSpan.FromSeconds(5), Ct);
        await open.NextEventAsync(TimeSpan.FromSeconds(5), Ct);

        await Post($"/api/v1/ops/journeys/{journeyId}/delay", new { minutes = 9, reasonCode = "Weather", expectedVersion = 0 }, controller);

        var seenByStaff = await NextOf(staff, "DelayChanged");
        var seenByPublic = await NextOf(open, "DelayChanged");
        Assert.Equal("Controller", seenByStaff.GetProperty("source").GetString());
        Assert.False(seenByPublic.TryGetProperty("source", out _));
        Assert.Equal(seenByPublic.GetProperty("payload").GetRawText(), seenByStaff.GetProperty("payload").GetRawText());   // the same event plus one field
    }

    [Fact]
    public async Task The_staff_snapshot_says_who_supplied_the_delay_and_each_actual_time_and_the_public_snapshot_does_not()
    {
        var journeyId = await RealJourneys.CreateAsync(api.Factory.Services, "1004", Day, j =>
            j.RecordCrewTime(j.Stops[1].StationId, StopEventKind.Arrival, j.Stops[1].ScheduledArrival!.Value.AddMinutes(4)));
        using var staff = await Connect(await StaffToken("CTRL-01"), $"?journeyIds={journeyId}");
        using var open = await SseClient.ConnectAsync(api.Factory.CreateClient(), $"/api/v1/stream?journeyIds={journeyId}", null, Ct);

        var staffSnapshot = (await staff.NextEventAsync(TimeSpan.FromSeconds(5), Ct))!.Json;
        var publicSnapshot = (await open.NextEventAsync(TimeSpan.FromSeconds(5), Ct))!;

        Assert.Equal("Crew", staffSnapshot.GetProperty("delaySource").GetString());
        Assert.True(staffSnapshot.TryGetProperty("delaySetAt", out var setAt) && setAt.GetDateTime() > DateTime.MinValue);   // the age of the delay (O-3)
        var stops = staffSnapshot.GetProperty("stops").EnumerateArray().ToList();
        Assert.Equal("Crew", stops[1].GetProperty("actualArrivalSource").GetString());
        Assert.False(stops[0].TryGetProperty("actualArrivalSource", out _));   // the origin has only an inferred departure
        Assert.DoesNotContain("Source", publicSnapshot.Data!);                 // the public snapshot has no source field at all
        Assert.DoesNotContain("delaySetAt", publicSnapshot.Data!);             // and no staff-only time either
    }

    [Fact]
    public async Task A_station_manager_gets_only_the_events_of_their_own_station_whatever_they_ask_for()
    {
        var journeyId = await RealJourneys.CreateAsync(api.Factory.Services, "1004", Day);
        var stations = await RealJourneys.StationsAsync(api.Factory.Services, "KDT", "PLG");
        var controller = await StaffToken("CTRL-01");
        // The manager of Kandy asks for Polgahawela and for the journey; they still get Kandy's events only.
        using var stream = await Connect(await StaffToken("SM-KDT"), $"?stationIds={stations["PLG"]}&journeyIds={journeyId}");
        Assert.Equal(HttpStatusCode.OK, stream.Status);
        await stream.NextEventAsync(TimeSpan.FromSeconds(5), Ct);   // the snapshot

        await Post($"/api/v1/ops/journeys/{journeyId}/stops/{stations["PLG"]}/platform", new { platform = "3", conflictsAcknowledged = true, expectedVersion = 0 }, controller);
        await Post($"/api/v1/ops/journeys/{journeyId}/stops/{stations["KDT"]}/platform", new { platform = "2", conflictsAcknowledged = true, expectedVersion = 1 }, controller);

        var changed = await NextOf(stream, "PlatformChanged");
        Assert.Equal(stations["KDT"], changed.GetProperty("payload").GetProperty("stationId").GetGuid());   // the Polgahawela change never came
    }

    // Uses an application of its own (own database, own clock): a fake clock cannot go back, and the shared one must stay at 10:00.
    [Fact]
    public async Task The_staff_stream_ends_when_the_access_token_expires()
    {
        var clock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(new DateTimeOffset(2026, 10, 14, 4, 30, 0, TimeSpan.Zero));
        var connection = await api.CreateDatabaseAsync("ops_stream_expiry_test");
        using var factory = api.CreateFactory(
            builder => builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock)),
            useFixtureClock: false, connectionString: connection);
        var http = factory.NewClient();
        var token = await (await http.SignInAsync("CTRL-01", api.DemoPassword, Ct)).AccessTokenAsync(Ct);

        using var stream = await SseClient.ConnectAsync(http, $"/api/v1/ops/stream?journeyIds={Guid.NewGuid()}", null, Ct, token);
        Assert.Equal(HttpStatusCode.OK, stream.Status);
        Assert.True((await stream.NextAsync(TimeSpan.FromSeconds(5), Ct))!.IsComment);   // the heartbeat: connected and quiet

        clock.Advance(TimeSpan.FromMinutes(16));   // the token lives 15 minutes

        Assert.Null(await stream.NextEventAsync(TimeSpan.FromSeconds(5), Ct));
        Assert.True(stream.IsClosed);              // closed by the server, not just quiet
    }

    [Fact]
    public async Task A_controller_must_name_what_to_follow_and_an_unknown_caller_is_refused()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/ops/stream");
        request.Headers.Add("Authorization", "Bearer " + await StaffToken("CTRL-01"));
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Factory.NewClient().SendAsync(request, Ct)).StatusCode);

        var anonymous = await api.Factory.NewClient().GetAsync("/api/v1/ops/stream?lineIds=" + Guid.NewGuid(), Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }
}
