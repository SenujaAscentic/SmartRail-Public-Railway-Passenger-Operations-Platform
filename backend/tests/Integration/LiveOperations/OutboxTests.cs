using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Outbox;
using SmartRail.LiveOperations.Persistence;
using Xunit;

namespace SmartRail.IntegrationTests.LiveOperations;

// The outbox against a real PostgreSQL: events are written in the SAME SaveChanges as the state change (architecture
// principles, "announcing a fact"), and the aggregate's list is emptied only once the save has succeeded.
[Collection("Api")]
public class OutboxTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IServiceScope Scope() => api.Factory.Services.CreateScope();

    private static LiveOperationsDbContext Db(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<LiveOperationsDbContext>();

    // Any generated journey (the generation runs in the background after start-up), loaded with its stops.
    private async Task<OperationalJourney> LoadJourney(LiveOperationsDbContext db, int skip = 0)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!await db.Journeys.AnyAsync(Ct) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100, Ct);
        }

        return await db.Journeys.Include(j => j.Stops).OrderBy(j => j.Id).Skip(skip).FirstAsync(Ct);
    }

    // The second stop of the journey: a Stop with an arrival time (the first stop has none).
    private static JourneyStop SecondStop(OperationalJourney journey) => journey.Stops.OrderBy(s => s.Sequence).Skip(1).First();

    private static async Task<List<OutboxEvent>> RowsFor(LiveOperationsDbContext db, Guid journeyId) =>
        await db.OutboxEvents.AsNoTracking().Where(e => e.JourneyId == journeyId).OrderBy(e => e.Id).ToListAsync(Ct);

    [Fact]
    public async Task Saving_a_changed_journey_writes_its_events_as_outbox_rows_in_the_ops_schema()
    {
        using var scope = Scope();
        var db = Db(scope);
        var journey = await LoadJourney(db, skip: 1);
        var stop = SecondStop(journey);
        var late = stop.ScheduledArrival!.Value.AddMinutes(3);

        journey.RecordCrewTime(stop.StationId, StopEventKind.Arrival, late);
        var expected = journey.DomainEvents.Count;
        await db.SaveChangesAsync(Ct);

        var rows = await RowsFor(db, journey.Id);
        Assert.Equal(expected, rows.Count);
        Assert.Contains(rows, r => r.Type == "StopStatusChanged");
        Assert.Contains(rows, r => r.Type == "DelayChanged");
        Assert.All(rows, r =>
        {
            Assert.Equal(api.Clock.GetUtcNow().UtcDateTime, r.CreatedAt);   // TimeProvider, not the system clock
            Assert.NotEqual(Guid.Empty, r.EventId);
        });
        Assert.Equal(rows.Count, rows.Select(r => r.EventId).Distinct().Count());
        Assert.Equal(late, rows.Single(r => r.Type == "DelayChanged").OccurredAt);   // the corrected event time

        Assert.Equal(1, await ScalarAsync("""SELECT count(*) FROM information_schema.tables WHERE table_schema = 'ops' AND table_name = 'OutboxEvents'"""));
    }

    [Fact]
    public async Task The_aggregates_event_list_is_emptied_after_a_successful_save()
    {
        using var scope = Scope();
        var db = Db(scope);
        var journey = await LoadJourney(db, skip: 2);
        var stop = SecondStop(journey);

        journey.RecordCrewTime(stop.StationId, StopEventKind.Arrival, stop.ScheduledArrival!.Value.AddMinutes(2));
        Assert.NotEmpty(journey.DomainEvents);
        await db.SaveChangesAsync(Ct);

        Assert.Empty(journey.DomainEvents);
        var afterFirstSave = (await RowsFor(db, journey.Id)).Count;
        Assert.True(afterFirstSave > 0);

        await db.SaveChangesAsync(Ct);   // a second save must not write the same events again
        Assert.Equal(afterFirstSave, (await RowsFor(db, journey.Id)).Count);
    }

    // The point of an outbox: if the state change cannot be saved, nothing is announced, and the events are not lost.
    [Fact]
    public async Task A_failed_save_writes_no_outbox_rows_and_keeps_the_events()
    {
        using var scope = Scope();
        var db = Db(scope);
        var journey = await LoadJourney(db, skip: 3);
        var stop = SecondStop(journey);
        var rowsBefore = await db.OutboxEvents.CountAsync(Ct);

        journey.RecordCrewTime(stop.StationId, StopEventKind.Arrival, stop.ScheduledArrival!.Value.AddMinutes(1));
        var events = journey.DomainEvents.Count;

        // A second journey for the same service and date breaks the unique index, so this one save must fail as a whole.
        db.Journeys.Add(OperationalJourney.Plan(Guid.NewGuid(), journey.ServiceId, journey.ServiceDate, journey.TrainSetId, [], TimeZoneInfo.Utc, journey.LineId));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));

        Assert.Equal(events, journey.DomainEvents.Count);
        using var other = Scope();
        Assert.Equal(rowsBefore, await Db(other).OutboxEvents.CountAsync(Ct));
    }

    private async Task<long> ScalarAsync(string sql)
    {
        await using var connection = new Npgsql.NpgsqlConnection(api.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new Npgsql.NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync(Ct))!;
    }
}
