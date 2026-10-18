using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartRail.Identity;
using SmartRail.IntegrationTests.Identity;
using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Features.Idempotency;
using SmartRail.LiveOperations.Outbox;
using SmartRail.LiveOperations.Persistence;
using Xunit;

namespace SmartRail.IntegrationTests.LiveOperations;

// POST /api/v1/crew/journeys/{id}/start and /end (contract 3.4, US-C2). The test clock is 14 Oct 2026, 04:30 UTC (10:00 in Colombo).
// Service 1004 is a weekend train from Kandy at 11:00 local (05:30 UTC), so today its window opens at 04:30: exactly now.
[Collection("Api")]
public class CrewStartEndTests(ApiFixture api) : IAsyncLifetime
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

    // A cab-session token from a real sign-in (it ends that driver's and that tablet's earlier session, R-19).
    private async Task<string> CabToken(string device, string driver) =>
        await (await _client.CabSignInAsync(device, api.CabDeviceSecrets[device], driver, api.DemoPassword, api.Clock.GetUtcNow(), Ct)).AccessTokenAsync(Ct);

    // A journey of service `number` on `date` that belongs to `driver` (staff number).
    private async Task<Guid> JourneyOf(string driver, string number, DateOnly date)
    {
        var id = await DriverId(driver);
        return await RealJourneys.CreateAsync(api.Factory.Services, number, date, j => j.AssignDriver(id));
    }

    private Task<HttpResponseMessage> Post(string action, Guid journeyId, string token, Guid? clientActionId = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/crew/journeys/{journeyId}/{action}")
        {
            Content = JsonContent.Create(new { clientActionId = clientActionId ?? Guid.NewGuid() }),
        };
        request.Headers.Add("Authorization", "Bearer " + token);
        return _client.SendAsync(request, Ct);
    }

    private async Task<(JourneyStatus Status, TrackingStatus Tracking)> StateOf(Guid journeyId)
    {
        using var scope = api.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiveOperationsDbContext>();
        var journey = await db.Journeys.AsNoTracking().SingleAsync(j => j.Id == journeyId, Ct);
        return (journey.Status, journey.Tracking);
    }

    private async Task<List<OutboxEvent>> EventsOf(Guid journeyId, string type)
    {
        using var scope = api.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiveOperationsDbContext>();
        return await db.OutboxEvents.AsNoTracking().Where(e => e.JourneyId == journeyId && e.Type == type).ToListAsync(Ct);
    }

    private static async Task<string?> CodeOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();

    // ---- Start ----

    [Fact]
    public async Task The_assigned_driver_can_start_exactly_60_minutes_before_departure_and_the_staff_side_keeps_the_tablet_and_session()
    {
        var id = await JourneyOf("DRV-02", "1004", Today);
        var token = await CabToken("CAB-02", "DRV-02");

        var response = await Post("start", id, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(id, body.GetProperty("journeyId").GetGuid());
        Assert.Equal("Running", body.GetProperty("status").GetString());
        Assert.Equal(api.Clock.GetUtcNow(), body.GetProperty("at").GetDateTimeOffset());
        Assert.Equal(JourneyStatus.Running, (await StateOf(id)).Status);

        var started = Assert.Single(await EventsOf(id, "JourneyStarted"));
        var jwt = AuthTestHelpers.Read(token);
        Assert.Contains(jwt.GetClaim("deviceId").Value, started.Json);          // the staff side knows which tablet ...
        Assert.Contains(jwt.GetClaim("crewSessionId").Value, started.Json);     // ... and which cab session
    }

    [Fact]
    public async Task Starting_more_than_60_minutes_before_departure_is_a_422_RULE_VIOLATION()
    {
        var id = await JourneyOf("DRV-02", "1004", Today.AddDays(1));   // departs 11:00 tomorrow
        var token = await CabToken("CAB-02", "DRV-02");

        var response = await Post("start", id, token);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("RULE_VIOLATION", await CodeOf(response));
        Assert.Equal(JourneyStatus.Planned, (await StateOf(id)).Status);
        Assert.Empty(await EventsOf(id, "JourneyStarted"));
    }

    [Fact]
    public async Task A_driver_cannot_start_a_journey_that_is_not_assigned_to_them_and_nothing_changes()
    {
        var id = await JourneyOf("DRV-02", "1004", Today.AddDays(2));
        var token = await CabToken("CAB-01", "DRV-01");   // another driver, on another tablet

        var response = await Post("start", id, token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("OUT_OF_SCOPE", await CodeOf(response));
        Assert.Equal(JourneyStatus.Planned, (await StateOf(id)).Status);
        Assert.Empty(await EventsOf(id, "JourneyStarted"));
    }

    [Fact]
    public async Task A_phone_token_cannot_start_a_journey_even_for_the_assigned_driver()
    {
        var id = await JourneyOf("DRV-02", "1004", Today.AddDays(3));
        var phone = await (await _client.CrewSignInAsync("DRV-02", api.DemoPassword, Ct)).AccessTokenAsync(Ct);

        var response = await Post("start", id, phone);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(JourneyStatus.Planned, (await StateOf(id)).Status);
    }

    [Fact]
    public async Task A_start_without_a_clientActionId_is_a_400()
    {
        var id = await JourneyOf("DRV-02", "1004", Today.AddDays(4));
        var token = await CabToken("CAB-02", "DRV-02");
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/crew/journeys/{id}/start") { Content = JsonContent.Create(new { }) };
        request.Headers.Add("Authorization", "Bearer " + token);

        var response = await _client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_FAILED", await CodeOf(response));
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    [InlineData("not-an-id")]
    public async Task An_unknown_or_malformed_journey_is_404(string id)
    {
        var token = await CabToken("CAB-02", "DRV-02");
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/crew/journeys/{id}/start") { Content = JsonContent.Create(new { clientActionId = Guid.NewGuid() }) };
        request.Headers.Add("Authorization", "Bearer " + token);

        var response = await _client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("NOT_FOUND", await CodeOf(response));
    }

    // ---- repeats (C-7) ----

    [Fact]
    public async Task Sending_the_same_start_again_gets_the_original_answer_and_changes_nothing()
    {
        var id = await JourneyOf("DRV-02", "1004", Today);
        var token = await CabToken("CAB-02", "DRV-02");
        var action = Guid.NewGuid();

        var first = await Post("start", id, token, action);
        var again = await Post("start", id, token, action);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(Ct), await again.Content.ReadAsStringAsync(Ct));
        Assert.Single(await EventsOf(id, "JourneyStarted"));
    }

    [Fact]
    public async Task A_new_start_on_a_running_journey_is_409_INVALID_STATE_and_a_reused_id_for_another_action_is_refused()
    {
        var id = await JourneyOf("DRV-02", "1004", Today);
        var token = await CabToken("CAB-02", "DRV-02");
        var action = Guid.NewGuid();
        await Post("start", id, token, action);

        var second = await Post("start", id, token);                // a different clientActionId: a new action, and it is not allowed
        var reused = await Post("end", id, token, action);          // the id of the start, used for an end

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("INVALID_STATE", await CodeOf(second));
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        Assert.Equal(JourneyStatus.Running, (await StateOf(id)).Status);
    }

    // Five identical requests at the same instant: they collide on the journey row and on the idempotency key, the losers are retried,
    // and every one of them is answered 200 with the same result while the journey starts exactly once (R-18 retry, C-7).
    [Fact]
    public async Task Identical_requests_sent_at_the_same_moment_start_the_journey_exactly_once()
    {
        var id = await JourneyOf("DRV-02", "1004", Today);
        var token = await CabToken("CAB-02", "DRV-02");
        var action = Guid.NewGuid();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Post("start", id, token, action)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var bodies = await Task.WhenAll(responses.Select(r => r.Content.ReadAsStringAsync(Ct)));
        Assert.Single(bodies.Distinct());
        Assert.Single(await EventsOf(id, "JourneyStarted"));
    }

    // ---- End ----

    [Fact]
    public async Task The_driver_ends_a_running_journey_and_it_is_Completed_with_tracking_Ended()
    {
        var id = await JourneyOf("DRV-02", "1004", Today);
        var token = await CabToken("CAB-02", "DRV-02");
        await Post("start", id, token);

        var response = await Post("end", id, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Completed", (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("status").GetString());
        Assert.Equal((JourneyStatus.Completed, TrackingStatus.Ended), await StateOf(id));
        var ended = Assert.Single(await EventsOf(id, "JourneyEnded"));
        Assert.Equal("Driver", JsonDocument.Parse(ended.Json).RootElement.GetProperty("payload").GetProperty("reason").GetString());

        // The passengers' view says so.
        var detail = await (await _client.GetAsync($"/api/v1/journeys/{id}", Ct)).Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("Completed", detail.GetProperty("status").GetProperty("kind").GetString());
    }

    [Fact]
    public async Task A_journey_that_is_not_running_cannot_be_ended_and_another_driver_cannot_end_it()
    {
        var id = await JourneyOf("DRV-02", "1004", Today);
        var own = await CabToken("CAB-02", "DRV-02");

        var notRunning = await Post("end", id, own);
        Assert.Equal(HttpStatusCode.Conflict, notRunning.StatusCode);
        Assert.Equal("INVALID_STATE", await CodeOf(notRunning));

        await Post("start", id, own);
        var other = await CabToken("CAB-01", "DRV-01");
        var refused = await Post("end", id, other);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal(JourneyStatus.Running, (await StateOf(id)).Status);
    }

    // ---- what the public stream shows (T-07) ----

    [Fact]
    public async Task The_public_stream_announces_the_start_and_the_end_without_the_tablet_or_the_cab_session()
    {
        var id = await JourneyOf("DRV-02", "1004", Today);
        var token = await CabToken("CAB-02", "DRV-02");
        using var stream = await SseClient.ConnectAsync(api.Factory.CreateClient(), $"/api/v1/stream?journeyIds={id}", null, Ct);
        var snapshot = (await stream.NextEventAsync(TimeSpan.FromSeconds(5), Ct))!;
        Assert.Equal("Planned", snapshot.Json.GetProperty("status").GetString());

        await Post("start", id, token);
        await Post("end", id, token);

        var started = (await stream.NextEventAsync(TimeSpan.FromSeconds(5), Ct))!;
        Assert.Equal("JourneyStarted", started.Event);
        Assert.Empty(started.Json.GetProperty("payload").EnumerateObject());
        var ended = (await stream.NextEventAsync(TimeSpan.FromSeconds(5), Ct))!;
        Assert.Equal("JourneyEnded", ended.Event);
        Assert.Equal("Driver", ended.Json.GetProperty("payload").GetProperty("reason").GetString());
        var jwt = AuthTestHelpers.Read(token);
        foreach (var text in new[] { started.Data!, ended.Data! })
        {
            Assert.DoesNotContain(jwt.GetClaim("deviceId").Value, text);
            Assert.DoesNotContain(jwt.GetClaim("crewSessionId").Value, text);
            Assert.DoesNotContain("source", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ---- the remembered answers are forgotten after 48 hours ----

    [Fact]
    public async Task Remembered_crew_actions_are_deleted_after_48_hours_and_recent_ones_kept()
    {
        using var scope = api.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiveOperationsDbContext>();
        var now = api.Clock.GetUtcNow().UtcDateTime;
        var old = new ProcessedAction { ClientActionId = Guid.NewGuid(), JourneyId = Guid.NewGuid(), Kind = "Start", ResultJson = "{}", CreatedAt = now - TimeSpan.FromHours(49) };
        var recent = new ProcessedAction { ClientActionId = Guid.NewGuid(), JourneyId = Guid.NewGuid(), Kind = "Start", ResultJson = "{}", CreatedAt = now - TimeSpan.FromHours(47) };
        db.ProcessedActions.AddRange(old, recent);
        await db.SaveChangesAsync(Ct);

        await scope.ServiceProvider.GetRequiredService<OutboxPump>().DeleteOldAsync(db, Ct);

        var left = await db.ProcessedActions.AsNoTracking().Select(a => a.ClientActionId).ToListAsync(Ct);
        Assert.DoesNotContain(old.ClientActionId, left);
        Assert.Contains(recent.ClientActionId, left);
        db.ProcessedActions.Remove(recent);   // leave nothing behind
        await db.SaveChangesAsync(Ct);
    }
}
