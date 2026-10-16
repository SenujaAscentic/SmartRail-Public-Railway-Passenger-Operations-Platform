using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartRail.LiveOperations.Persistence;
using SmartRail.LiveOperations.Stream;

namespace SmartRail.LiveOperations.Outbox;

// The work of one dispatcher round, kept apart from the timer loop so it can be called directly.
// Logs carry event ids, types and counts only, never the payload (CLAUDE.md section 6).
internal sealed class OutboxPump(IEventPublisher publisher, TimeProvider clock, ILogger<OutboxPump> log)
{
    public const int BatchSize = 100;
    public const int MaxAttempts = 5;
    public static readonly TimeSpan Retention = TimeSpan.FromHours(24);   // contract section 4: resume works for 24 h

    // Sends the oldest Pending rows. Returns how many it looked at (BatchSize means "there may be more").
    //
    // The sequence is given HERE, one row at a time, and not when the row is written: two transactions can commit in the
    // opposite order to their start, and a client must never see sequence 10 before 9. One dispatcher gives them in the
    // order it sends them. (A second application instance would need a lock; see the code guide.)
    //
    // A crash between sending and marking sends the row again on restart under a NEW sequence: at-least-once delivery.
    // That is why every event carries an eventId which consumers ignore a second time (domain model 8.1).
    public async Task<int> DispatchBatchAsync(LiveOperationsDbContext db, CancellationToken cancellationToken)
    {
        var batch = await db.OutboxEvents
            .Where(e => e.Status == OutboxStatus.Pending)
            .OrderBy(e => e.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var row in batch)
        {
            try
            {
                var envelope = JsonSerializer.Deserialize<EventEnvelope>(row.Json, EventJson.Options)
                    ?? throw new InvalidDataException("The outbox row has no event.");

                row.Sequence = await NextSequenceAsync(db, cancellationToken);
                var publicEvent = PublicProjection.Of(envelope, row.Sequence.Value);
                if (publicEvent is not null)
                {
                    publisher.Publish(publicEvent);
                }

                row.Status = OutboxStatus.Dispatched;
                row.DispatchedAt = clock.GetUtcNow().UtcDateTime;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                row.Sequence = null;   // it was never announced, so it takes no place in the stream
                row.Attempts++;

                // The exception TYPE is logged, not its message: a message could quote data from the event.
                if (row.Attempts >= MaxAttempts)
                {
                    row.Status = OutboxStatus.Failed;
                    log.LogError("Outbox event {EventId} ({Type}) given up after {Attempts} attempts: {Reason}",
                        row.EventId, row.Type, row.Attempts, ex.GetType().Name);
                }
                else
                {
                    log.LogWarning("Outbox event {EventId} ({Type}) failed attempt {Attempts} of {MaxAttempts}: {Reason}",
                        row.EventId, row.Type, row.Attempts, MaxAttempts, ex.GetType().Name);
                }
            }

            await db.SaveChangesAsync(cancellationToken);   // one row at a time, so a crash can repeat at most one
        }

        return batch.Count;
    }

    // Dispatched and failed rows older than the retention are no longer needed for resume. Pending rows are never deleted.
    public async Task DeleteOldAsync(LiveOperationsDbContext db, CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow().UtcDateTime - Retention;
        await db.OutboxEvents
            .Where(e => e.Status != OutboxStatus.Pending && e.CreatedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    // A constant SQL text with no input in it (CLAUDE.md section 6: no string-built SQL from outside values).
    private static Task<long> NextSequenceAsync(LiveOperationsDbContext db, CancellationToken cancellationToken) =>
        db.Database.SqlQueryRaw<long>("SELECT nextval('ops.\"EventSequence\"') AS \"Value\"").SingleAsync(cancellationToken);
}
