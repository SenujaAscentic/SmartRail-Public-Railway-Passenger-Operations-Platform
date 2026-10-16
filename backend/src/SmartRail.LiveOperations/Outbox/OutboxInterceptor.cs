using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmartRail.LiveOperations.Domain;

namespace SmartRail.LiveOperations.Outbox;

// The transactional outbox (architecture principles: announcing a fact = an event written in the SAME transaction as the
// state change). Every SaveChanges collects the DomainEvents of the journeys being saved and adds them as outbox rows, so
// the state change and its announcement succeed or fail together. The aggregate's list is emptied only after the save
// has succeeded, so a failed save loses nothing. A save that changes no journey adds no rows.
internal sealed class OutboxInterceptor(TimeProvider clock) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AddRows(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        AddRows(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        ClearEvents(eventData.Context);
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        ClearEvents(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void AddRows(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var createdAt = clock.GetUtcNow().UtcDateTime;
        foreach (var journey in JourneysWithEvents(context))
        {
            foreach (var domainEvent in journey.DomainEvents)
            {
                var envelope = EventMapper.Map(domainEvent, journey, Guid.NewGuid());
                context.Set<OutboxEvent>().Add(new OutboxEvent
                {
                    EventId = envelope.EventId,
                    Type = envelope.Type,
                    OccurredAt = envelope.OccurredAt,
                    CreatedAt = createdAt,
                    JourneyId = journey.Id,
                    Json = JsonSerializer.Serialize(envelope, EventJson.Options),
                    Status = OutboxStatus.Pending,
                });
            }
        }
    }

    private static void ClearEvents(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var journey in JourneysWithEvents(context))
        {
            journey.ClearDomainEvents();
        }
    }

    private static List<OperationalJourney> JourneysWithEvents(DbContext context) =>
        context.ChangeTracker.Entries<OperationalJourney>().Select(e => e.Entity).Where(j => j.DomainEvents.Count > 0).ToList();
}
