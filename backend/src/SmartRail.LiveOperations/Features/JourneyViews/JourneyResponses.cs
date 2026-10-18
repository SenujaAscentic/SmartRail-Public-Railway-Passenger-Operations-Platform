namespace SmartRail.LiveOperations.Features.JourneyViews;

// The PUBLIC view of a journey (contract section 5, threat T-07): no source, no staff and no crew fields. The same records
// feed the generated API description, so the apps' types come from them.
//
// `Hold`, `Reason` and `PreviousPlatform` are always present and null until the controller decisions exist, and `Version` is 0
// until then: the apps' generated types must not change when those decisions arrive (contract 3.1).

// kind: NotStarted, OnTime, Late, Early (and Cancelled, TerminatedEarly, Held, Completed once those states exist).
// DelayMinutes is signed (negative = early); null for NotStarted, where there is nothing to say.
public sealed record JourneyStatusResponse(string Kind, int? DelayMinutes);

public sealed record JourneyFromResponse(
    Guid StationId, string Name, DateTime? ScheduledDeparture, DateTime? ExpectedDeparture, string Platform, string? PreviousPlatform);

public sealed record JourneyToResponse(Guid StationId, string Name, DateTime? ScheduledArrival, DateTime? ExpectedArrival);

// How fresh the position data is (R-06). UpdatedAt is when the last accepted position was recorded; null before any.
public sealed record JourneyFreshnessResponse(string Tracking, DateTime? UpdatedAt);

// The last announced position: the same fields as the TrainPositionUpdated event (the apps compute nothing).
public sealed record PositionResponse(
    double Latitude, double Longitude, double SpeedKmh, Guid LastStationId, Guid NextStationId, double FractionToNext);

public sealed record JourneyDetailFreshnessResponse(string Tracking, DateTime? UpdatedAt, PositionResponse? LastPosition);

// A hold the controller set (D-4); minutes is the planned duration, if one was given.
public sealed record JourneyHoldResponse(Guid StationId, int? Minutes);

// The reason the controller gave, for passengers ("signal fault near Ragama"): a code and an optional plain-text note.
public sealed record JourneyReasonResponse(string Code, string? Note);

// Search results, boards and saved trips.
public sealed record JourneySummaryResponse(
    Guid JourneyId,
    string ServiceNumber,
    string Name,
    string TrainType,
    DateOnly ServiceDate,
    JourneyStatusResponse Status,
    JourneyFromResponse From,
    JourneyToResponse To,
    JourneyFreshnessResponse Freshness,
    JourneyHoldResponse? Hold,
    JourneyReasonResponse? Reason,
    int Version);

public sealed record JourneyStopResponse(
    int Sequence,
    Guid StationId,
    string Name,
    string CallType,
    string Status,
    bool IsAdded,
    DateTime? ScheduledArrival,
    DateTime? ExpectedArrival,
    DateTime? ActualArrival,
    DateTime? ScheduledDeparture,
    DateTime? ExpectedDeparture,
    DateTime? ActualDeparture,
    string PlannedPlatform,
    string Platform,
    string? PreviousPlatform);

public sealed record CoachResponse(int Position, string Class, string Seating);

public sealed record TrainSetResponse(string Code, string Traction, IReadOnlyList<CoachResponse> Coaches);

// `GET /journeys/{id}`: the summary fields plus the stops, today's train set and the last known position.
public sealed record JourneyDetailResponse(
    Guid JourneyId,
    string ServiceNumber,
    string Name,
    string TrainType,
    DateOnly ServiceDate,
    JourneyStatusResponse Status,
    JourneyFromResponse From,
    JourneyToResponse To,
    JourneyDetailFreshnessResponse Freshness,
    JourneyHoldResponse? Hold,
    JourneyReasonResponse? Reason,
    int Version,
    IReadOnlyList<JourneyStopResponse> Stops,
    TrainSetResponse? TrainSet);
