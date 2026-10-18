using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartRail.LiveOperations.Persistence;

namespace SmartRail.LiveOperations.Features.Idempotency;

// Remembers what a crew action answered. Add() only queues the row: it is saved in the SAME SaveChanges as the journey change, so
// "done" and "remembered" cannot differ. Two identical requests at the same instant: the second one's insert breaks the primary key,
// OptimisticRetry reloads, and the repeat is then answered from here.
internal sealed class IdempotencyStore(LiveOperationsDbContext db, TimeProvider clock)
{
    public static readonly TimeSpan Retention = TimeSpan.FromHours(48);

    public Task<ProcessedAction?> FindAsync(Guid clientActionId, CancellationToken cancellationToken) =>
        db.ProcessedActions.AsNoTracking().SingleOrDefaultAsync(a => a.ClientActionId == clientActionId, cancellationToken);

    public void Add(Guid clientActionId, Guid journeyId, string kind, object result) =>
        db.ProcessedActions.Add(new ProcessedAction
        {
            ClientActionId = clientActionId,
            JourneyId = journeyId,
            Kind = kind,
            ResultJson = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        });

    public async Task DeleteOldAsync(CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow().UtcDateTime - Retention;
        await db.ProcessedActions.Where(a => a.CreatedAt < cutoff).ExecuteDeleteAsync(cancellationToken);
    }
}
