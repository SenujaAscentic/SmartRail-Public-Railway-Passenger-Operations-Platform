using SmartRail.LiveOperations.Domain;
using SmartRail.Network;

namespace SmartRail.LiveOperations.Features.JourneyViews;

// Turns a journey (and the names Network knows) into the public responses. Pure: it reads nothing, so it is unit-tested.
// Every time shown is computed by the Domain (R-01 to R-03, R-23); nothing is calculated here (CLAUDE.md section 3).
internal static class JourneyViewBuilder
{
    // Never "OnTime" for a train nobody has reported on: no position, no tap and no controller delay means NotStarted
    // (the apps show "Live information unavailable", US-P4 AC1 star).
    public static JourneyStatusResponse Status(OperationalJourney journey)
    {
        if (journey.Status == JourneyStatus.Completed)
        {
            return new JourneyStatusResponse("Completed", journey.DelayMinutes);
        }

        if (journey.Tracking == TrackingStatus.NotStarted && journey.DelaySource == Source.Schedule)
        {
            return new JourneyStatusResponse("NotStarted", null);
        }

        var minutes = journey.DelayMinutes;
        return new JourneyStatusResponse(minutes > 0 ? "Late" : minutes < 0 ? "Early" : "OnTime", minutes);
    }

    // The timetable has no names for services ("Kandy Express" in the contract's example), so the name is the terminus
    // and the train type: simple, and it matches the example.
    public static string Name(string terminusName, string trainType) => $"{terminusName} {trainType}";

    public static JourneySummaryResponse Summary(
        OperationalJourney journey, JourneyStop from, JourneyStop to,
        IReadOnlyDictionary<Guid, string> names, ServiceDetails service, string name) =>
        new(journey.Id, service.Number, name, service.TrainType, journey.ServiceDate, Status(journey),
            From(journey, from, names), To(journey, to, names),
            new JourneyFreshnessResponse(journey.Tracking.ToString(), journey.LastPositionAt),
            Hold: null, Reason(journey), journey.ControlVersion);

    public static JourneyDetailResponse Detail(
        OperationalJourney journey, IReadOnlyDictionary<Guid, string> names, ServiceDetails service, TrainSetDetails? trainSet)
    {
        var stops = journey.Stops.OrderBy(s => s.Sequence).ToList();
        var delay = journey.CurrentDelay.Value;
        var position = journey.LastLatitude is { } lat && journey.LastLongitude is { } lon
            && journey.LastSpeedKmh is { } speed && journey.LastStationId is { } last && journey.NextStationId is { } next && journey.FractionToNext is { } fraction
            ? new PositionResponse(lat, lon, speed, last, next, fraction)
            : null;

        return new JourneyDetailResponse(
            journey.Id, service.Number, Name(names[stops[^1].StationId], service.TrainType), service.TrainType, journey.ServiceDate,
            Status(journey), From(journey, stops[0], names), To(journey, stops[^1], names),
            new JourneyDetailFreshnessResponse(journey.Tracking.ToString(), journey.LastPositionAt, position),
            Hold: null, Reason(journey), journey.ControlVersion,
            stops.Select(s => new JourneyStopResponse(
                s.Sequence, s.StationId, names[s.StationId], s.CallType.ToString(), s.Status.ToString(), IsAdded: false,
                s.ScheduledArrival, s.ExpectedArrival(delay), s.ActualArrival,
                s.ScheduledDeparture, s.ExpectedDeparture(delay), s.ActualDeparture,
                s.PlannedPlatform, s.PlannedPlatform, PreviousPlatform: null)).ToList(),
            trainSet is null ? null : new TrainSetResponse(
                trainSet.Code, trainSet.Traction, trainSet.Coaches.Select(c => new CoachResponse(c.Position, c.Class, c.Seating)).ToList()));
    }

    private static JourneyReasonResponse? Reason(OperationalJourney journey) =>
        journey.Reason is { } reason ? new JourneyReasonResponse(reason.Code, reason.Note) : null;

    // `platform` is the planned platform until a platform change exists (Phase 3).
    private static JourneyFromResponse From(OperationalJourney journey, JourneyStop stop, IReadOnlyDictionary<Guid, string> names) =>
        new(stop.StationId, names[stop.StationId], stop.ScheduledDeparture, stop.ExpectedDeparture(journey.CurrentDelay.Value),
            stop.PlannedPlatform, PreviousPlatform: null);

    private static JourneyToResponse To(OperationalJourney journey, JourneyStop stop, IReadOnlyDictionary<Guid, string> names) =>
        new(stop.StationId, names[stop.StationId], stop.ScheduledArrival, stop.ExpectedArrival(journey.CurrentDelay.Value));
}
