using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Outbox;
using SmartRail.LiveOperations.Persistence;
using Xunit;

namespace SmartRail.IntegrationTests.LiveOperations;

// GET /api/v1/stream (contract section 4): the public event stream. Snapshot on connect, live events, Last-Event-ID resume,
// heartbeat, and the public projection (threat T-07).
[Collection("Api")]
public class StreamTests(ApiFixture api) : IAsyncLifetime
{
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    // The journeys this class made are removed again (see JourneyCleanup).
    public async ValueTask DisposeAsync() => await JourneyCleanup.RemoveAllAsync(api.ConnectionString);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    private IServiceProvider Services => api.Factory.Services;

    private Task<SseClient> Connect(string query, string? lastEventId = null) =>
        SseClient.ConnectAsync(api.Factory.CreateClient(), "/api/v1/stream?" + query, lastEventId, Ct);

    // Reads events until the wanted type has arrived n times (heartbeats are skipped); returns everything read.
    private static async Task<List<SseFrame>> ReadUntil(SseClient client, string eventName, int count = 1)
    {
        var frames = new List<SseFrame>();
        while (frames.Count(f => f.Event == eventName) < count)
        {
            var frame = await client.NextEventAsync(Wait, Ct);
            Assert.True(frame is not null, $"Waiting for {count} x {eventName}: got {string.Join(", ", frames.Select(f => f.Event))}.");
            frames.Add(frame!);
        }

        return frames;
    }

    private async Task WaitDispatched(Guid journeyId, int atLeast)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LiveOperationsDbContext>();
            var rows = await db.OutboxEvents.AsNoTracking().Where(e => e.JourneyId == journeyId).ToListAsync(Ct);
            if (rows.Count >= atLeast && rows.All(r => r.Status == OutboxStatus.Dispatched))
            {
                return;
            }

            await Task.Delay(50, Ct);
        }

        Assert.Fail("The events were not dispatched in time.");
    }

    // ---- refused requests ----

    [Theory]
    [InlineData("")]
    [InlineData("journeyIds=")]
    [InlineData("journeyIds=not-a-guid")]
    public async Task A_request_without_a_usable_filter_is_a_400_with_field_errors(string query)
    {
        using var client = await Connect(query);

        Assert.Equal(HttpStatusCode.BadRequest, client.Status);
        var body = await client.Response.Content.ReadAsStringAsync(Ct);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("VALIDATION_FAILED", json.RootElement.GetProperty("code").GetString());
        Assert.True(json.RootElement.TryGetProperty("errors", out _));
        Assert.True(json.RootElement.TryGetProperty("requestId", out _));
    }

    // ---- connect: headers and snapshot ----

    [Fact]
    public async Task Connecting_answers_200_as_an_event_stream_that_is_not_cached()
    {
        var journey = await TestJourney.CreateAsync(Services);
        using var client = await Connect($"journeyIds={journey.Id}");

        Assert.Equal(HttpStatusCode.OK, client.Status);
        Assert.Equal("text/event-stream", client.ContentType);
        Assert.Contains("no-store", client.Response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task A_new_connection_first_receives_a_snapshot_of_each_journey_it_asked_for()
    {
        var journey = await TestJourney.CreateAsync(Services);
        using var client = await Connect($"journeyIds={journey.Id}");

        var frame = await client.NextEventAsync(Wait, Ct);

        Assert.Equal("snapshot", frame!.Event);
        var data = frame.Json;
        Assert.Equal(journey.Id, data.GetProperty("journeyId").GetGuid());
        Assert.Equal("2026-10-14", data.GetProperty("serviceDate").GetString());
        Assert.Equal(journey.LineId, data.GetProperty("lineId").GetGuid());
        Assert.Equal(0, data.GetProperty("delayMinutes").GetInt32());
        Assert.Equal("NotStarted", data.GetProperty("tracking").GetString());
        Assert.Equal(frame.SequenceId, data.GetProperty("sequence").GetInt64());   // the sequence the snapshot is current to
        var stops = data.GetProperty("stops").EnumerateArray().ToList();
        Assert.Equal(3, stops.Count);
        Assert.Equal("Scheduled", stops[1].GetProperty("status").GetString());
        Assert.Equal(journey.MiddleScheduledArrival, stops[1].GetProperty("expectedArrival").GetDateTime());
        Assert.Equal(journey.Middle, stops[1].GetProperty("stationId").GetGuid());
    }

    [Fact]
    public async Task The_snapshot_reflects_changes_made_before_connecting()
    {
        var journey = await TestJourney.CreateAsync(Services);
        await journey.ArriveLateAtMiddleAsync(Services, 4, Ct);
        using var client = await Connect($"journeyIds={journey.Id}");

        var data = (await client.NextEventAsync(Wait, Ct))!.Json;

        Assert.Equal(4, data.GetProperty("delayMinutes").GetInt32());
        var stops = data.GetProperty("stops").EnumerateArray().ToList();
        Assert.Equal("Departed", stops[0].GetProperty("status").GetString());   // inferred
        Assert.Equal("Arrived", stops[1].GetProperty("status").GetString());
        Assert.Equal(journey.MiddleScheduledArrival.AddMinutes(4), stops[1].GetProperty("expectedArrival").GetDateTime());
        Assert.Equal(journey.MiddleScheduledArrival.AddMinutes(6), stops[1].GetProperty("expectedDeparture").GetDateTime());   // 06:42 + 4 min
    }

    [Fact]
    public async Task A_station_filter_snapshots_the_journeys_calling_there_and_a_line_filter_those_on_the_line()
    {
        var journey = await TestJourney.CreateAsync(Services);

        using (var byStation = await Connect($"stationIds={journey.Terminus}"))
        {
            Assert.Equal(journey.Id, (await byStation.NextEventAsync(Wait, Ct))!.Json.GetProperty("journeyId").GetGuid());
        }

        using var byLine = await Connect($"lineIds={journey.LineId}");
        Assert.Equal(journey.Id, (await byLine.NextEventAsync(Wait, Ct))!.Json.GetProperty("journeyId").GetGuid());
    }

    // ---- live events ----

    [Fact]
    public async Task A_change_after_connecting_arrives_as_live_events_with_increasing_ids()
    {
        var journey = await TestJourney.CreateAsync(Services);
        using var client = await Connect($"journeyIds={journey.Id}");
        var snapshot = (await client.NextEventAsync(Wait, Ct))!;

        await journey.ArriveLateAtMiddleAsync(Services, 4, Ct);

        var frames = await ReadUntil(client, "DelayChanged");
        Assert.Equal(["StopStatusChanged", "StopStatusChanged", "DelayChanged"], frames.Select(f => f.Event));
        var ids = frames.Select(f => f.SequenceId).ToList();
        Assert.Equal(ids.Order(), ids);
        Assert.All(ids, id => Assert.True(id > snapshot.SequenceId));
        var delay = frames[2].Json;
        Assert.Equal(4, delay.GetProperty("payload").GetProperty("delayMinutes").GetInt32());
        Assert.Equal(journey.Id, delay.GetProperty("journeyId").GetGuid());
        Assert.Equal(frames[2].SequenceId, delay.GetProperty("sequence").GetInt64());
    }

    // T-07: whatever the staff-side event knows, the public stream shows no source and no staff or session data.
    [Fact]
    public async Task Public_events_never_contain_source_staff_or_session_fields()
    {
        var journey = await TestJourney.CreateAsync(Services);
        using var client = await Connect($"journeyIds={journey.Id}");
        await client.NextEventAsync(Wait, Ct);   // snapshot

        await journey.ArriveLateAtMiddleAsync(Services, 4, Ct);
        var frames = await ReadUntil(client, "DelayChanged");

        foreach (var text in frames.Select(f => f.Data!))
        {
            Assert.DoesNotContain("source", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("crew", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("actor", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("causedBy", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("incident", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task A_journey_filter_does_not_deliver_other_journeys_events()
    {
        var mine = await TestJourney.CreateAsync(Services);
        var other = await TestJourney.CreateAsync(Services);
        using var client = await Connect($"journeyIds={mine.Id}");
        await client.NextEventAsync(Wait, Ct);   // snapshot

        await other.ArriveLateAtMiddleAsync(Services, 6, Ct);   // not mine: must not arrive
        await mine.ArriveLateAtMiddleAsync(Services, 2, Ct);

        var frames = await ReadUntil(client, "DelayChanged");
        Assert.All(frames, f => Assert.Equal(mine.Id, f.Json.GetProperty("journeyId").GetGuid()));
        Assert.Equal(2, frames.Last().Json.GetProperty("payload").GetProperty("delayMinutes").GetInt32());
    }

    // A display at the terminus needs the delay (it will show the later arrival), not the status of stops before it.
    [Fact]
    public async Task A_station_filter_delivers_events_that_name_that_station_only()
    {
        var journey = await TestJourney.CreateAsync(Services);
        using var client = await Connect($"stationIds={journey.Terminus}");
        await client.NextEventAsync(Wait, Ct);   // snapshot

        await journey.ArriveLateAtMiddleAsync(Services, 4, Ct);

        var first = (await client.NextEventAsync(Wait, Ct))!;
        Assert.Equal("DelayChanged", first.Event);   // the stop-status events name stations 1 and 2, not the terminus
        Assert.Contains(journey.Terminus, first.Json.GetProperty("stationIds").EnumerateArray().Select(e => e.GetGuid()));
    }

    // CLAUDE.md section 5, step 5: the example files in shared/events are what client authors build from, so what the stream
    // really sends must carry exactly their fields (the shape check in the unit tests covers the nested detail).
    [Fact]
    public async Task Frames_on_the_wire_carry_the_fields_of_the_shared_examples()
    {
        var folder = Path.GetFullPath(Path.Combine(global::SmartRail.Network.Seed.SeedFolder.Find(configuredPath: null), "..", "events"));
        static List<string> Names(JsonElement element) => element.EnumerateObject().Select(p => p.Name).Order().ToList();
        JsonElement Example(string name) => JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, name + ".json"))).RootElement;

        var journey = await TestJourney.CreateAsync(Services);
        using var client = await Connect($"journeyIds={journey.Id}");
        var snapshot = (await client.NextEventAsync(Wait, Ct))!;
        await journey.ArriveLateAtMiddleAsync(Services, 4, Ct);
        var frames = await ReadUntil(client, "DelayChanged");

        // A fresh journey has no position yet, so the snapshot lacks lastSeenAt and lastPosition: those two are optional.
        var optional = new[] { "lastSeenAt", "lastPosition" };
        Assert.Equal(Names(Example("snapshot")).Except(optional), Names(snapshot.Json));
        foreach (var type in new[] { "StopStatusChanged", "DelayChanged" })
        {
            var frame = frames.First(f => f.Event == type).Json;
            Assert.Equal(Names(Example(type)), Names(frame));
            // `reason` is in the example but only present when the controller set the delay; this delay came from a stop event.
            Assert.Equal(Names(Example(type).GetProperty("payload")).Except(["reason"]), Names(frame.GetProperty("payload")));
        }
    }

    // ---- resume ----

    [Fact]
    public async Task Reconnecting_with_last_event_id_replays_the_missed_events_and_sends_no_snapshot()
    {
        var journey = await TestJourney.CreateAsync(Services);
        long lastSeen;
        using (var first = await Connect($"journeyIds={journey.Id}"))
        {
            lastSeen = (await first.NextEventAsync(Wait, Ct))!.SequenceId;   // the snapshot's id
        }

        // The client is away while the journey changes.
        await journey.ArriveLateAtMiddleAsync(Services, 4, Ct);
        await WaitDispatched(journey.Id, atLeast: 3);

        using var second = await Connect($"journeyIds={journey.Id}", lastEventId: lastSeen.ToString());
        var frames = await ReadUntil(second, "DelayChanged");

        Assert.Equal(["StopStatusChanged", "StopStatusChanged", "DelayChanged"], frames.Select(f => f.Event));
        Assert.DoesNotContain(frames, f => f.Event is "snapshot" or "resync");
        Assert.All(frames, f => Assert.True(f.SequenceId > lastSeen));
        Assert.Equal(frames.Select(f => f.SequenceId).Order(), frames.Select(f => f.SequenceId));
    }

    [Fact]
    public async Task Resuming_repeats_nothing_the_client_already_has()
    {
        var journey = await TestJourney.CreateAsync(Services);
        await journey.ArriveLateAtMiddleAsync(Services, 4, Ct);
        await WaitDispatched(journey.Id, atLeast: 3);
        long newest;
        using (var all = await Connect($"journeyIds={journey.Id}"))
        {
            newest = (await all.NextEventAsync(Wait, Ct))!.SequenceId;   // the snapshot is current to the newest event
        }

        using var again = await Connect($"journeyIds={journey.Id}", lastEventId: newest.ToString());
        await journey.TapAsync(Services, journey.Terminus, StopEventKind.Arrival, new DateTime(2026, 10, 14, 7, 36, 0, DateTimeKind.Utc), Ct);

        var next = (await again.NextEventAsync(Wait, Ct))!;
        Assert.True(next.SequenceId > newest);   // the first thing it gets is new
    }

    [Fact]
    public async Task A_last_event_id_the_server_cannot_resume_from_gets_resync_then_a_snapshot()
    {
        var journey = await TestJourney.CreateAsync(Services);

        using var client = await Connect($"journeyIds={journey.Id}", lastEventId: "9000000000");

        var resync = await client.NextEventAsync(Wait, Ct);
        Assert.Equal("resync", resync!.Event);
        Assert.Null(resync.Id);   // it must not move the client's last event id
        var snapshot = await client.NextEventAsync(Wait, Ct);
        Assert.Equal("snapshot", snapshot!.Event);
        Assert.Equal(journey.Id, snapshot.Json.GetProperty("journeyId").GetGuid());
    }

    // ---- heartbeat ----

    [Fact]
    public async Task A_quiet_stream_sends_a_heartbeat_comment()
    {
        var journey = await TestJourney.CreateAsync(Services);
        using var client = await Connect($"journeyIds={journey.Id}");
        await client.NextEventAsync(Wait, Ct);   // snapshot

        SseFrame? heartbeat = null;
        for (var i = 0; i < 5 && heartbeat is null; i++)
        {
            var frame = await client.NextAsync(TimeSpan.FromSeconds(3), Ct);
            heartbeat = frame is { IsComment: true } ? frame : null;
        }

        Assert.NotNull(heartbeat);
    }
}
