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

// Where the train is, as the stream shows it: the latest accepted fix of a batch and its place along the route. The route is not
// known to the Domain (it lives in Network), so the slice works out the two stations and the fraction and passes them in.
public sealed record TrainPositionUpdated(
    Guid JourneyId, DateTime At, GeoPoint Location, double SpeedKmh, Guid LastStationId, Guid NextStationId, double FractionToNext) : IDomainEvent;

public sealed record JourneyEnded(Guid JourneyId, DateTime At, JourneyEndReason Reason) : IDomainEvent;

// A stop changed status. Crew taps and GPS detections are the same event; Source says which (section 8.2).
public sealed record StopStatusChanged(
    Guid JourneyId, Guid StationId, int Sequence, StopStatus From, StopStatus To, DateTime At, Source Source) : IDomainEvent;

// Reason is set only when the controller set the delay (or cleared it: then it is null); the passenger app shows why.
public sealed record DelayChanged(
    Guid JourneyId, int DelayMinutes, int PreviousDelayMinutes, Source Source, DateTime At, JourneyReason? Reason = null) : IDomainEvent;

// The controller put a hold on the train at a station, or released it (also: the train left, which ends the hold). `Source` is
// Controller for a decision and the source of the departure otherwise. Minutes and Reason are given when it is set.
public sealed record JourneyHeld(
    Guid JourneyId, Guid StationId, bool Released, int? Minutes, JourneyReason? Reason, Source Source, DateTime At) : IDomainEvent;

// The controller cancelled the journey (R-14): every stop still ahead is skipped.
public sealed record JourneyCancelled(Guid JourneyId, JourneyReason Reason, DateTime At) : IDomainEvent;

// The controller reinstated a cancelled journey before its scheduled start: it runs again (US-O4 AC4).
public sealed record JourneyReinstated(Guid JourneyId, DateTime At) : IDomainEvent;

// The controller moved a stop to another platform (R-11). HoldSet is true only when a hold was really set with it: apps may say
// "the train will wait" only then. LeadTimeMinutes is recorded for the audit and the staff (US-O5 AC2); it is not public.
public sealed record PlatformChanged(
    Guid JourneyId, Guid StationId, string Platform, string PreviousPlatform, LeadTimeLevel Level, int LeadTimeMinutes, bool HoldSet, DateTime At) : IDomainEvent;

// The controller ended the journey early at a station ahead of the train (R-14); the stops after it are skipped.
public sealed record JourneyTerminatedEarly(Guid JourneyId, Guid AtStationId, JourneyReason Reason, DateTime At) : IDomainEvent;

// The controller skipped a stop: the train will not call there (R-15).
public sealed record StopSkipped(Guid JourneyId, Guid StationId, int Sequence, DateTime At) : IDomainEvent;

public sealed record DataFreshnessChanged(
    Guid JourneyId, TrackingStatus From, TrackingStatus To, DateTime? LastSeenAt, DateTime At) : IDomainEvent;
