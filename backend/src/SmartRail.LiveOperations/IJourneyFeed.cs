using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Outbox;
using SmartRail.LiveOperations.Persistence;
using SmartRail.LiveOperations.Stream;

namespace SmartRail.LiveOperations;

// LiveOperations' public door for modules that react to journey events (architecture Q3): the CateringIntegration module reads the
// events through it, in the order of the stream, keeping its own place (a cursor) so that no event is lost when the application
// stops. No tables are shared and no message broker is added: this reads the same outbox the public stream is built on.

// One event as the stream sends it publicly: the same fields, none of the staff fields (T-07).
public sealed record JourneyFeedEvent(
    long Sequence, Guid EventId, string Type, DateTime OccurredAt, Guid JourneyId, DateOnly ServiceDate, JsonElement Payload);

// What a stop of a journey is now. `ExpectedArrival` is what the system expects (the actual time once it has happened).
public sealed record JourneyCall(Guid StationId, int Sequence, bool Stops, string Status, DateTime? ScheduledArrival, DateTime? ExpectedArrival);

// A journey as it is now: its status, the reason the controller gave (code and note), and every call.
public sealed record JourneyState(
    Guid JourneyId, Guid ServiceId, DateOnly ServiceDate, string Status, string? ReasonCode, string? ReasonNote, IReadOnlyList<JourneyCall> Calls);

public interface IJourneyFeed
{
    // The newest sequence that has been dispatched (0 if none): where a reader that starts today begins, so it does not replay history.
    Task<long> GetLatestSequenceAsync(CancellationToken cancellationToken);

    // The dispatched events after `afterSequence`, oldest first, at most `limit`.
    Task<IReadOnlyList<JourneyFeedEvent>> ReadAfterAsync(long afterSequence, int limit, CancellationToken cancellationToken);

    // The journey as it is now, or null if there is none.
    Task<JourneyState?> GetStateAsync(Guid journeyId, CancellationToken cancellationToken);
}

internal sealed class JourneyFeed(LiveOperationsDbContext db) : IJourneyFeed
{
    public async Task<long> GetLatestSequenceAsync(CancellationToken cancellationToken) =>
        await db.OutboxEvents.AsNoTracking().Where(e => e.Status == OutboxStatus.Dispatched && e.Sequence != null).MaxAsync(e => e.Sequence, cancellationToken) ?? 0;

    public async Task<IReadOnlyList<JourneyFeedEvent>> ReadAfterAsync(long afterSequence, int limit, CancellationToken cancellationToken)
    {
        var rows = await db.OutboxEvents.AsNoTracking()
            .Where(e => e.Status == OutboxStatus.Dispatched && e.Sequence > afterSequence)
            .OrderBy(e => e.Sequence)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var events = new List<JourneyFeedEvent>();
        foreach (var row in rows)
        {
            var envelope = JsonSerializer.Deserialize<EventEnvelope>(row.Json, EventJson.Options);
            // The PUBLIC projection: an event type or field that is not public is not given to another module either.
            var publicEvent = envelope is null ? null : PublicProjection.Of(envelope, row.Sequence!.Value);
            if (publicEvent is not null)
            {
                events.Add(new JourneyFeedEvent(
                    publicEvent.Sequence, publicEvent.EventId, publicEvent.Type, publicEvent.OccurredAt, publicEvent.JourneyId, publicEvent.ServiceDate,
                    JsonSerializer.SerializeToElement(publicEvent.Payload, EventJson.Options)));
            }
        }

        return events;
    }

    public async Task<JourneyState?> GetStateAsync(Guid journeyId, CancellationToken cancellationToken)
    {
        var journey = await db.Journeys.AsNoTracking().Include(j => j.Stops).SingleOrDefaultAsync(j => j.Id == journeyId, cancellationToken);
        if (journey is null)
        {
            return null;
        }

        var delay = journey.CurrentDelay.Value;
        return new JourneyState(
            journey.Id, journey.ServiceId, journey.ServiceDate, journey.Status.ToString(), journey.ReasonCode, journey.ReasonNote,
            journey.Stops.OrderBy(s => s.Sequence)
                .Select(s => new JourneyCall(s.StationId, s.Sequence, s.CallType == CallType.Stop, s.Status.ToString(), s.ScheduledArrival, s.ExpectedArrival(delay)))
                .ToList());
    }
}
