using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SmartRail.IntegrationTests.Identity;
using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Outbox;
using SmartRail.LiveOperations.Persistence;
using Xunit;

namespace SmartRail.IntegrationTests.LiveOperations;

// The controller's decisions over HTTP (contract 3.5, US-O4, R-18): delay, and the version check every decision shares.
// The journey is service 1004 on a date of the test's own, so nothing else touches it.
[Collection("Api")]
public class ControllerDecisionTests(ApiFixture api) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateOnly Day = new(2027, 3, 3);

    private readonly HttpClient _client = api.Factory.NewClient();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await JourneyCleanup.RemoveAllAsync(api.ConnectionString);

    // ---- helpers ----

    private Task<Guid> NewJourney(Action<OperationalJourney>? change = null) => RealJourneys.CreateAsync(api.Factory.Services, "1004", Day, change);

    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body, string? token = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Authorization", "Bearer " + (token ?? await api.TokenForAsync("Controller", Ct)));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _client.SendAsync(request, Ct);
    }

    private Task<HttpResponseMessage> SetDelay(Guid id, int expectedVersion, int minutes = 12, string reason = "SignalFailure", string? note = "Signal fault near Ragama") =>
        Send(HttpMethod.Post, $"/api/v1/ops/journeys/{id}/delay", new { minutes, reasonCode = reason, note, expectedVersion });

    private async Task<JsonElement> Detail(Guid id) => await _client.GetFromJsonAsync<JsonElement>($"/api/v1/journeys/{id}", Ct);

    private static async Task<string?> CodeOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();

    private async Task<List<(string Type, JsonElement Payload)>> EventsOf(Guid journeyId)
    {
        using var scope = api.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiveOperationsDbContext>();
        var rows = await db.OutboxEvents.AsNoTracking().Where(e => e.JourneyId == journeyId).OrderBy(e => e.Id).ToListAsync(Ct);
        return rows.Select(e => (e.Type, JsonDocument.Parse(e.Json).RootElement.GetProperty("payload").Clone())).ToList();
    }

    // ---- delay ----

    [Fact]
    public async Task The_controller_sets_a_delay_with_a_reason_and_the_answer_is_the_journey_with_its_new_version()
    {
        var id = await NewJourney();

        var response = await SetDelay(id, expectedVersion: 0, minutes: 12);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(1, body.GetProperty("version").GetInt32());
        Assert.Equal("Late", body.GetProperty("status").GetProperty("kind").GetString());
        Assert.Equal(12, body.GetProperty("status").GetProperty("delayMinutes").GetInt32());
        Assert.Equal("SignalFailure", body.GetProperty("reason").GetProperty("code").GetString());
        Assert.Equal("Signal fault near Ragama", body.GetProperty("reason").GetProperty("note").GetString());
        Assert.Equal(body.GetRawText(), (await Detail(id)).GetRawText());   // the same detail the passengers get
    }

    [Fact]
    public async Task The_delay_is_announced_with_its_reason_and_the_public_event_names_no_staff_member()
    {
        var id = await NewJourney();
        using var stream = await SseClient.ConnectAsync(api.Factory.CreateClient(), $"/api/v1/stream?journeyIds={id}", null, Ct);
        await stream.NextEventAsync(TimeSpan.FromSeconds(5), Ct);   // the snapshot

        await SetDelay(id, 0, minutes: 7, reason: "Weather", note: null);

        SseFrame? frame;
        do
        {
            frame = await stream.NextEventAsync(TimeSpan.FromSeconds(5), Ct);
            Assert.NotNull(frame);
        }
        while (frame!.Event != "DelayChanged");
        var payload = frame.Json.GetProperty("payload");
        Assert.Equal(7, payload.GetProperty("delayMinutes").GetInt32());
        Assert.Equal("Weather", payload.GetProperty("reason").GetProperty("code").GetString());
        Assert.DoesNotContain("source", frame.Data!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Demo Controller", frame.Data!);
    }

    [Fact]
    public async Task Clearing_the_delay_removes_the_reason_and_the_next_clear_is_refused()
    {
        var id = await NewJourney();
        await SetDelay(id, 0);

        var cleared = await Send(HttpMethod.Delete, $"/api/v1/ops/journeys/{id}/delay", new { expectedVersion = 1 });

        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        var body = await cleared.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(2, body.GetProperty("version").GetInt32());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("reason").ValueKind);
        Assert.Equal("NotStarted", body.GetProperty("status").GetProperty("kind").GetString());   // nobody has reported on the train

        var again = await Send(HttpMethod.Delete, $"/api/v1/ops/journeys/{id}/delay", new { expectedVersion = 2 });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("INVALID_STATE", await CodeOf(again));
    }

    // ---- R-18 / US-O4 AC6 ----

    [Fact]
    public async Task A_decision_based_on_an_old_version_is_refused_with_409_naming_who_changed_it_and_giving_the_journey()
    {
        var id = await NewJourney();
        await SetDelay(id, 0, minutes: 10);

        var stale = await SetDelay(id, expectedVersion: 0, minutes: 30);

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var body = await stale.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("VERSION_CONFLICT", body.GetProperty("code").GetString());
        Assert.Equal(1, body.GetProperty("currentVersion").GetInt32());
        Assert.StartsWith("Changed by Demo Controller ", body.GetProperty("detail").GetString());
        Assert.EndsWith(" ago", body.GetProperty("detail").GetString());
        Assert.Equal(10, body.GetProperty("journey").GetProperty("status").GetProperty("delayMinutes").GetInt32());   // the latest state
        Assert.Equal(10, (await Detail(id)).GetProperty("status").GetProperty("delayMinutes").GetInt32());            // and nothing changed
    }

    [Fact]
    public async Task A_stale_decision_writes_no_event_and_no_audit_entry()
    {
        var id = await NewJourney();
        await SetDelay(id, 0);
        var events = (await EventsOf(id)).Count;
        var audits = await AuditCount(id);

        await SetDelay(id, expectedVersion: 0, minutes: 40);

        Assert.Equal(events, (await EventsOf(id)).Count);
        Assert.Equal(audits, await AuditCount(id));
    }

    [Fact]
    public async Task Two_decisions_made_at_the_same_moment_on_the_same_version_have_exactly_one_winner()
    {
        var id = await NewJourney();

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(i => SetDelay(id, expectedVersion: 0, minutes: 10 + i)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.OK), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.Equal(1, (await Detail(id)).GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task A_position_and_a_crew_style_update_do_not_change_the_version_the_controller_sees()
    {
        var id = await NewJourney(j => j.RecordCrewTime(j.Stops[0].StationId, StopEventKind.Departure, new DateTime(2027, 3, 3, 5, 30, 0, DateTimeKind.Utc)));

        Assert.Equal(0, (await Detail(id)).GetProperty("version").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await SetDelay(id, 0)).StatusCode);
    }

    // ---- validation and state ----

    [Theory]
    [InlineData("noMinutes", "minutes")]
    [InlineData("negative", "minutes")]
    [InlineData("tooMany", "minutes")]
    [InlineData("noReason", "reasonCode")]
    [InlineData("badReason", "reasonCode")]
    [InlineData("longNote", "note")]
    [InlineData("noVersion", "expectedVersion")]
    public async Task A_bad_delay_request_is_a_400_naming_the_field(string kind, string field)
    {
        var id = await NewJourney();
        object body = kind switch
        {
            "noMinutes" => new { reasonCode = "Weather", expectedVersion = 0 },
            "negative" => new { minutes = -1, reasonCode = "Weather", expectedVersion = 0 },
            "tooMany" => new { minutes = 1441, reasonCode = "Weather", expectedVersion = 0 },
            "noReason" => new { minutes = 5, expectedVersion = 0 },
            "badReason" => new { minutes = 5, reasonCode = "Aliens", expectedVersion = 0 },
            "longNote" => new { minutes = 5, reasonCode = "Weather", note = new string('x', 301), expectedVersion = 0 },
            _ => new { minutes = 5, reasonCode = "Weather" },
        };

        var response = await Send(HttpMethod.Post, $"/api/v1/ops/journeys/{id}/delay", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors").TryGetProperty(field, out _));
        Assert.Equal(0, (await Detail(id)).GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task A_delay_on_an_unknown_journey_is_404_and_on_a_completed_one_409_invalid_state()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await SetDelay(Guid.NewGuid(), 0)).StatusCode);

        var done = await NewJourney(j =>
        {
            j.Start(j.Stops[0].ScheduledDeparture!.Value, Guid.NewGuid(), Guid.NewGuid());
            j.End(j.Stops[0].ScheduledDeparture!.Value.AddHours(3));
        });
        var response = await SetDelay(done, 0);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("INVALID_STATE", await CodeOf(response));
    }

    // ---- audit (US-O10) ----

    private async Task<int> AuditCount(Guid journeyId)
    {
        await using var connection = new NpgsqlConnection(api.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("""SELECT count(*) FROM identity."AuditEntries" WHERE "TargetId" = @id""", connection);
        command.Parameters.AddWithValue("id", journeyId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(Ct));
    }

    [Fact]
    public async Task Every_decision_writes_an_audit_entry_with_the_actor_id_the_reason_code_and_no_note_or_name()
    {
        var id = await NewJourney();
        await SetDelay(id, 0, minutes: 9, reason: "TrackWork", note: "Gang working near Veyangoda");
        await Send(HttpMethod.Delete, $"/api/v1/ops/journeys/{id}/delay", new { expectedVersion = 1 });

        await using var connection = new NpgsqlConnection(api.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(
            """SELECT "Action", "ActorRole", "TargetType", "Reason", "Summary", "RequestId" FROM identity."AuditEntries" WHERE "TargetId" = @id ORDER BY "At", "Id" """, connection);
        command.Parameters.AddWithValue("id", id);
        var rows = new List<(string Action, string Role, string Type, string? Reason, string Summary, string RequestId)>();
        await using (var reader = await command.ExecuteReaderAsync(Ct))
        {
            while (await reader.ReadAsync(Ct))
            {
                rows.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetString(4), reader.GetString(5)));
            }
        }

        Assert.Equal(["DelaySet", "DelayCleared"], rows.Select(r => r.Action));
        Assert.All(rows, r => { Assert.Equal("Controller", r.Role); Assert.Equal("Journey", r.Type); Assert.NotEmpty(r.RequestId); });
        Assert.Equal("TrackWork", rows[0].Reason);
        Assert.Equal("Delay set to 9 min", rows[0].Summary);
        Assert.DoesNotContain(rows, r => r.Summary.Contains("Veyangoda") || r.Summary.Contains("Demo Controller"));
    }
}
