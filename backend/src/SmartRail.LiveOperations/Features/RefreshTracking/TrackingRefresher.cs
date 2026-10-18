using Microsoft.EntityFrameworkCore;
using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Persistence;

namespace SmartRail.LiveOperations.Features.RefreshTracking;

// One round of "has any train gone quiet?" (R-06) and "is any train overdue at its next stop?" (R-23).
// Positions keep a train Live by themselves; nothing arrives when a train goes silent, so the clock has to ask.
internal sealed class TrackingRefresher(LiveOperationsDbContext db, TimeProvider clock)
{
    // Only journeys that are being tracked: NotStarted has no data to age, and Ended is over (domain model 4.4).
    private static readonly TrackingStatus[] Tracked = [TrackingStatus.Live, TrackingStatus.Stale, TrackingStatus.Lost];

    // Returns how many journeys were looked at. Saving writes the events the journeys raised to the outbox (same SaveChanges),
    // so the stream hears Live -> Stale -> Lost and a growing overdue delay without any other code.
    // A collision with a position that was saved at the same moment is retried from a fresh load (R-18).
    public Task<int> RunOnceAsync(CancellationToken cancellationToken) => OptimisticRetry.RunAsync(db, async () =>
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var journeys = await db.Journeys.Include(j => j.Stops).Where(j => Tracked.Contains(j.Tracking)).ToListAsync(cancellationToken);

        foreach (var journey in journeys)
        {
            journey.RefreshTracking(now);
        }

        await db.SaveChangesAsync(cancellationToken);
        return journeys.Count;
    });
}
