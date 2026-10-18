namespace SmartRail.LiveOperations.Domain;

// Facts the journey announces (domain model section 8.2). The journey only collects them in DomainEvents; the slice that
// saves the journey writes them to the outbox in the same transaction (architecture principles), then clears the list.
// Names and fields follow section 8.2 so the outbox maps them one to one. The envelope fields (eventId, sequence,
// stationIds, lineId, causedBy) and DelayChanged.stops[] are added by the slice, which knows the line and the actor.
public interface IDomainEvent
{
    Guid JourneyId { get; }
}

// The driver started the journey on the cab tablet. DeviceId and CrewSessionId are staff-only (T-07): the public stream sends
// no payload for this event.
public sealed record JourneyStarted(Guid JourneyId, DateTime At, Guid DeviceId, Guid CrewSessionId) : IDomainEvent;

public sealed record JourneyEnded(Guid JourneyId, DateTime At, JourneyEndReason Reason) : IDomainEvent;

// A stop changed status. Crew taps and GPS detections are the same event; Source says which (section 8.2).
public sealed record StopStatusChanged(
    Guid JourneyId, Guid StationId, int Sequence, StopStatus From, StopStatus To, DateTime At, Source Source) : IDomainEvent;

public sealed record DelayChanged(
    Guid JourneyId, int DelayMinutes, int PreviousDelayMinutes, Source Source, DateTime At) : IDomainEvent;

public sealed record DataFreshnessChanged(
    Guid JourneyId, TrackingStatus From, TrackingStatus To, DateTime? LastSeenAt, DateTime At) : IDomainEvent;
