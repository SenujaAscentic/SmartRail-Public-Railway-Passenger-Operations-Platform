using System.Text.Json;
using SmartRail.LiveOperations.Domain;

namespace SmartRail.LiveOperations.Outbox;

// Turns a domain event of the aggregate into the envelope and payload of domain model 8.1 / 8.2. It reads the journey as it
// is when the save happens: the aggregate knows the line, the date and every stop, which the bare event does not.
internal static class EventMapper
{
    public static EventEnvelope Map(IDomainEvent domainEvent, OperationalJourney journey, Guid eventId) => domainEvent switch
    {
        StopStatusChanged e => Envelope(
            journey, eventId, "StopStatusChanged", e.At, e.Source, [e.StationId],
            new StopStatusChangedPayload(e.StationId, e.Sequence, e.From, e.To, e.At)),

        // The stops still ahead (or at the platform): every display of those stations needs the new delay.
        DelayChanged e => Envelope(
            journey, eventId, "DelayChanged", e.At, e.Source, RemainingStations(journey),
            new DelayChangedPayload(e.DelayMinutes, e.PreviousDelayMinutes, RemainingStops(journey), e.Reason)),

        // The source is Gps: freshness is the age of the GPS data, even when the clock is what notices that it got old.
        DataFreshnessChanged e => Envelope(
            journey, eventId, "DataFreshnessChanged", e.At, Source.Gps, RemainingStations(journey),
            new DataFreshnessChangedPayload(e.From, e.To, e.LastSeenAt, LastKnownStation(journey))),

        // Only the two stations around the train care where it is; a map follows the journey id. Source Gps: it is a GPS fix.
        TrainPositionUpdated e => Envelope(
            journey, eventId, "TrainPositionUpdated", e.At, Source.Gps, [e.LastStationId, e.NextStationId],
            new TrainPositionUpdatedPayload(
                new GeoLocation(e.Location.Latitude, e.Location.Longitude), e.SpeedKmh, e.LastStationId, e.NextStationId, e.FractionToNext)),

        // The held station's display and every station after it need to hear about the hold (the station may already be behind the
        // train when a release comes from the train leaving, so it is named explicitly).
        JourneyHeld e => Envelope(
            journey, eventId, "JourneyHeld", e.At, e.Source, RemainingStations(journey).Union([e.StationId]).ToList(),
            new JourneyHeldPayload(e.StationId, e.Released, e.Minutes, e.Reason)),

        // Only that station's displays and the trains' passengers there care which platform it is.
        PlatformChanged e => Envelope(
            journey, eventId, "PlatformChanged", e.At, Source.Controller, [e.StationId],
            new PlatformChangedPayload(e.StationId, e.Platform, e.PreviousPlatform, e.Level, e.LeadTimeMinutes, e.HoldSet)),

        // A cancelled or reinstated train concerns every station on its route.
        JourneyCancelled e => Envelope(
            journey, eventId, "JourneyCancelled", e.At, Source.Controller, AllStations(journey), new JourneyCancelledPayload(e.Reason)),

        JourneyReinstated e => Envelope(
            journey, eventId, "JourneyReinstated", e.At, Source.Controller, AllStations(journey), new EmptyPayload()),

        // Every stop's display shows a running or finished train, so both events name all the stops.
        JourneyStarted e => Envelope(
            journey, eventId, "JourneyStarted", e.At, Source.Crew, AllStations(journey),
            new JourneyStartedPayload(e.DeviceId, e.CrewSessionId)),

        JourneyEnded e => Envelope(
            journey, eventId, "JourneyEnded", e.At, e.Reason == JourneyEndReason.Controller ? Source.Controller : Source.Crew,
            AllStations(journey), new JourneyEndedPayload(e.Reason)),

        _ => throw new NotSupportedException($"No stream mapping for the domain event {domainEvent.GetType().Name}."),
    };

    private static EventEnvelope Envelope<T>(
        OperationalJourney journey, Guid eventId, string type, DateTime occurredAt, Source source,
        IReadOnlyList<Guid> stationIds, T payload) =>
        new(eventId, type, Version: 1, occurredAt, journey.Id, journey.ServiceDate, stationIds, journey.LineId, source,
            JsonSerializer.SerializeToNode(payload, EventJson.Options)!.AsObject());

    private static List<JourneyStop> Remaining(OperationalJourney journey) =>
        journey.Stops.Where(s => s.Status is StopStatus.Scheduled or StopStatus.Arrived).OrderBy(s => s.Sequence).ToList();

    private static IReadOnlyList<Guid> AllStations(OperationalJourney journey) =>
        journey.Stops.OrderBy(s => s.Sequence).Select(s => s.StationId).ToList();

    private static IReadOnlyList<Guid> RemainingStations(OperationalJourney journey) =>
        Remaining(journey).Select(s => s.StationId).ToList();

    private static IReadOnlyList<RemainingStop> RemainingStops(OperationalJourney journey)
    {
        var delay = journey.CurrentDelay.Value;
        return Remaining(journey).Select(s => new RemainingStop(s.StationId, s.ExpectedArrival(delay), s.ExpectedDeparture(delay))).ToList();
    }

    // The furthest stop the train is known to have reached; none while every stop is still Scheduled.
    private static Guid? LastKnownStation(OperationalJourney journey) =>
        journey.Stops.Where(s => s.Status != StopStatus.Scheduled).OrderByDescending(s => s.Sequence).FirstOrDefault()?.StationId;
}
