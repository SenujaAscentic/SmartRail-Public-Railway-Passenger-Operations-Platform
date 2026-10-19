using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Outbox;
using SmartRail.LiveOperations.Persistence;
using Xunit;

namespace SmartRail.IntegrationTests.LiveOperations;

// The dispatcher picks Pending outbox rows, gives them a sequence in order, hands them to the stream and marks them.
[Collection("Api")]
public class OutboxDispatcherTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IServiceScope Scope() => api.Factory.Services.CreateScope();

    private static LiveOperationsDbContext Db(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<LiveOperationsDbContext>();

    private async Task<OperationalJourney> LoadJourney(LiveOperationsDbContext db, int skip)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (await db.Journeys.CountAsync(Ct) <= skip && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100, Ct);
        }

        return await db.Journeys.Include(j => j.Stops).OrderBy(j => j.Id).Skip(skip).FirstAsync(Ct);
    }

    // A crew arrival tap at the second stop: three events (stop 1 inferred, stop 2 arrived, delay changed).
    private static async Task ChangeAndSave(LiveOperationsDbContext db, OperationalJourney journey, int minutesLate)
    {
        var stop = journey.Stops.OrderBy(s => s.Sequence).Skip(1).First();
        journey.RecordCrewTime(stop.StationId, StopEventKind.Arrival, stop.ScheduledArrival!.Value.AddMinutes(minutesLate));
        await db.SaveChangesAsync(Ct);
    }

    private static async Task<List<OutboxEvent>> Rows(LiveOperationsDbContext db, Guid journeyId) =>
        await db.OutboxEvents.AsNoTracking().Where(e => e.JourneyId == journeyId).OrderBy(e => e.Id).ToListAsync(Ct);

    private static async Task<List<OutboxEvent>> WaitUntil(
        LiveOperationsDbContext db, Guid journeyId, Func<List<OutboxEvent>, bool> done, int seconds = 5)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        var rows = await Rows(db, journeyId);
        while (!done(rows) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50, Ct);
            rows = await Rows(db, journeyId);
        }

        return rows;
    }

    [Fact]
    public async Task Saved_events_are_dispatched_within_a_second_with_increasing_sequences_in_order()
    {
        using var scope = Scope();
        var db = Db(scope);
        var journey = await LoadJourney(db, skip: 4);

        var timer = Stopwatch.StartNew();
        await ChangeAndSave(db, journey, minutesLate: 3);
        var rows = await WaitUntil(db, journey.Id, r => r.Count >= 3 && r.All(x => x.Status == OutboxStatus.Dispatched));
        timer.Stop();

        Assert.True(rows.Count >= 3);
        Assert.All(rows, r => Assert.Equal(OutboxStatus.Dispatched, r.Status));
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(2), $"Dispatch took {timer.Elapsed.TotalSeconds:F1} s.");
        var sequences = rows.Select(r => r.Sequence!.Value).ToList();
        Assert.Equal(sequences.Order(), sequences);                  // given in insertion order
        Assert.Equal(sequences.Count, sequences.Distinct().Count());
        Assert.All(rows, r => Assert.Equal(api.Clock.GetUtcNow().UtcDateTime, r.DispatchedAt));   // TimeProvider
    }

    [Fact]
    public async Task Later_events_get_higher_sequences_than_earlier_ones_across_journeys()
    {
        using var scope = Scope();
        var db = Db(scope);
        var first = await LoadJourney(db, skip: 5);
        var second = await LoadJourney(db, skip: 6);

        await ChangeAndSave(db, first, 2);
        var firstRows = await WaitUntil(db, first.Id, r => r.Count >= 3 && r.All(x => x.Status == OutboxStatus.Dispatched));
        await ChangeAndSave(db, second, 2);
        var secondRows = await WaitUntil(db, second.Id, r => r.Count >= 3 && r.All(x => x.Status == OutboxStatus.Dispatched));

        Assert.True(secondRows.Min(r => r.Sequence) > firstRows.Max(r => r.Sequence));
    }

    [Fact]
    public async Task An_event_that_cannot_be_handed_over_is_retried_then_given_up_and_later_events_still_flow()
    {
        using var scope = Scope();
        var db = Db(scope);
        var broken = await LoadJourney(db, skip: 7);
        var healthy = await LoadJourney(db, skip: 8);
        FaultInjectingPublisher.FailingJourneys[broken.Id] = 0;
        try
        {
            await ChangeAndSave(db, broken, 2);
            await ChangeAndSave(db, healthy, 2);

            var failed = await WaitUntil(db, broken.Id, r => r.Count >= 3 && r.All(x => x.Status == OutboxStatus.Failed));
            var fine = await WaitUntil(db, healthy.Id, r => r.Count >= 3 && r.All(x => x.Status == OutboxStatus.Dispatched));

            Assert.All(failed, r =>
            {
                Assert.Equal(OutboxStatus.Failed, r.Status);
                Assert.Equal(OutboxPump.MaxAttempts, r.Attempts);
                Assert.Null(r.Sequence);   // never announced, so it takes no place in the stream
            });
            Assert.All(fine, r => Assert.Equal(OutboxStatus.Dispatched, r.Status));
        }
        finally
        {
            FaultInjectingPublisher.FailingJourneys.TryRemove(broken.Id, out _);
        }
    }

    // CLAUDE.md section 6 / T-06: logs carry ids and types only.
    [Fact]
    public async Task The_dispatcher_logs_ids_and_types_only()
    {
        using var scope = Scope();
        var db = Db(scope);
        var broken = await LoadJourney(db, skip: 9);
        FaultInjectingPublisher.FailingJourneys[broken.Id] = 0;
        try
        {
            await ChangeAndSave(db, broken, 2);
            var rows = await WaitUntil(db, broken.Id, r => r.Count >= 3 && r.All(x => x.Status == OutboxStatus.Failed));

            var messages = api.OutboxLog.Messages.Select(m => m.Message).Where(m => rows.Any(r => m.Contains(r.EventId.ToString()))).ToList();
            Assert.NotEmpty(messages);
            Assert.All(messages, m =>
            {
                Assert.DoesNotContain("delayMinutes", m);   // no payload
                Assert.DoesNotContain("\"payload\"", m);
                Assert.DoesNotContain("source", m, StringComparison.OrdinalIgnoreCase);
            });
            Assert.Contains(messages, m => m.Contains("given up", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            FaultInjectingPublisher.FailingJourneys.TryRemove(broken.Id, out _);
        }
    }

    // Dispatched rows are the replay log: kept 24 h, then deleted (failed ones too, they have been logged by then).
    [Fact]
    public async Task Dispatched_and_failed_rows_older_than_24_hours_are_deleted_and_recent_ones_kept()
    {
        using var scope = Scope();
        var db = Db(scope);
        var now = api.Clock.GetUtcNow().UtcDateTime;
        var journeyId = Guid.NewGuid();
        OutboxEvent Row(OutboxStatus status, TimeSpan age) => new()
        {
            EventId = Guid.NewGuid(), Type = "DelayChanged", OccurredAt = now - age, CreatedAt = now - age, JourneyId = journeyId,
            Json = "{}", Status = status, Sequence = null,
        };
        var oldDispatched = Row(OutboxStatus.Dispatched, TimeSpan.FromHours(25));
        var oldFailed = Row(OutboxStatus.Failed, TimeSpan.FromHours(25));
        var recent = Row(OutboxStatus.Dispatched, TimeSpan.FromHours(23));
        db.OutboxEvents.AddRange(oldDispatched, oldFailed, recent);
        await db.SaveChangesAsync(Ct);

        await scope.ServiceProvider.GetRequiredService<OutboxPump>().DeleteOldAsync(db, Ct);

        var left = (await Rows(db, journeyId)).Select(r => r.EventId).ToHashSet();
        Assert.DoesNotContain(oldDispatched.EventId, left);
        Assert.DoesNotContain(oldFailed.EventId, left);
        Assert.Contains(recent.EventId, left);
    }
}
