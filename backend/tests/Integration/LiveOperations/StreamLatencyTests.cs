using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Persistence;
using Xunit;

namespace SmartRail.IntegrationTests.LiveOperations;

// Gate G2 / success measure SM-3: a change reaches a connected client in under 5 seconds (user story US-O4 AC5, US-B4 AC3).
//
// There is no application endpoint that changes a journey yet, and none is added for a test. So the test does what the future
// slice will do: it loads a real generated journey, changes it through the aggregate, and saves. Everything after that is the
// real application: the outbox row, the dispatcher, the hub and the open stream connection.
[Collection("Api")]
public class StreamLatencyTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_saved_change_reaches_a_connected_stream_client_in_under_5_seconds()
    {
        // The last generated journey: the other tests use others or make their own.
        Guid journeyId, stationId;
        DateTime scheduledArrival;
        using (var scope = api.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiveOperationsDbContext>();
            await WaitForJourneys(db);
            var journey = await db.Journeys.AsNoTracking().Include(j => j.Stops).OrderByDescending(j => j.Id).FirstAsync(Ct);
            var stop = journey.Stops.OrderBy(s => s.Sequence).First(s => s.ScheduledArrival is not null);
            (journeyId, stationId, scheduledArrival) = (journey.Id, stop.StationId, stop.ScheduledArrival!.Value);
        }

        using var client = await SseClient.ConnectAsync(
            api.Factory.CreateClient(), $"/api/v1/stream?journeyIds={journeyId}", lastEventId: null, Ct);
        Assert.Equal("snapshot", (await client.NextEventAsync(TimeSpan.FromSeconds(5), Ct))!.Event);   // connected and caught up

        using (var scope = api.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiveOperationsDbContext>();
            var journey = await db.Journeys.Include(j => j.Stops).SingleAsync(j => j.Id == journeyId, Ct);
            journey.RecordCrewTime(stationId, StopEventKind.Arrival, scheduledArrival.AddMinutes(7));   // 7 min late

            var timer = Stopwatch.StartNew();
            await db.SaveChangesAsync(Ct);

            SseFrame? frame;
            do
            {
                frame = await client.NextEventAsync(TimeSpan.FromSeconds(5), Ct);
                Assert.True(frame is not null, "No event reached the client within 5 s of the save.");
            }
            while (frame!.Event != "DelayChanged");
            timer.Stop();

            Assert.Equal(7, frame.Json.GetProperty("payload").GetProperty("delayMinutes").GetInt32());
            Assert.True(timer.Elapsed < TimeSpan.FromSeconds(5), $"The change took {timer.Elapsed.TotalSeconds:F2} s to reach the client (limit 5 s).");
        }
    }

    // The generation runs in the background after start-up: wait until journeys exist.
    private static async Task WaitForJourneys(LiveOperationsDbContext db)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!await db.Journeys.AnyAsync(Ct) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100, Ct);
        }
    }
}
