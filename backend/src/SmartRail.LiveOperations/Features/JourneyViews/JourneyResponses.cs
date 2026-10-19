using System.Text.Json.Serialization;

namespace SmartRail.LiveOperations.Features.JourneyViews;

// The PUBLIC view of a journey (contract section 5, threat T-07): no source, no staff and no crew fields. The same records
// feed the generated API description, so the apps' types come from them.
//
// `Hold`, `Reason` and `PreviousPlatform` are always present and null until the controller decisions exist, and `Version` is 0
// until then: the apps' generated types must not change when those decisions arrive (contract 3.1).

// kind: NotStarted, OnTime, Late, Early (and Cancelled, TerminatedEarly, Held, Completed once those states exist).
// DelayMinutes is signed (negative = early); null for NotStarted, where there is nothing to say.
public sealed record JourneyStatusResponse(string Kind, [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? DelayMinutes);

public sealed record JourneyFromResponse(
    Guid StationId, string Name, [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTime? ScheduledDeparture, [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTime? ExpectedDeparture, string Platform, [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? PreviousPlatform);

public sealed record JourneyToResponse(Guid StationId, string Name, [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTime? ScheduledArrival, [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTime? ExpectedArrival);

// How fresh the position data is (R-06). UpdatedAt is when the last accepted position was recorded; null before any.
public sealed record JourneyFreshnessResponse(string Tracking, [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTime? UpdatedAt);

// The last announced position: the same fields as the TrainPositionUpdated event (the apps compute nothing).
public sealed record PositionResponse(
    double Latitude, double Longitude, double SpeedKmh, Guid LastStationId, Guid NextStationId, double FractionToNext);

// UpdatedAt is the time of the last ACCEPTED GPS position and nothing else (a controller decision does not touch it). LastStationName is
// the station that position was just past ("Last seen 07:38 near Kelaniya", S-15); both are null before any position.
public sealed record JourneyDetailFreshnessResponse(
    string Tracking, [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTime? UpdatedAt, [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? LastStationName, [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] PositionResponse? LastPosition);

// A hold the controller set (D-4); minutes is the planned duration, if one was given; Reason is the same {code, note} as everywhere.
public sealed record JourneyHoldResponse(Guid StationId, [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? Minutes, [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] JourneyReasonResponse? Reason);

// The reason the controller gave, for passengers ("signal fault near Ragama"): a code and an optional plain-text note.
public sealed record JourneyReasonResponse(string Code, [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Note);

// A station by id and name (where a journey ends, an end of a board line).
public sealed record JourneyStationResponse(Guid StationId, string Name);

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
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] JourneyHoldResponse? Hold,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] JourneyReasonResponse? Reason,
    int Version);

public sealed record JourneyStopResponse(
    int Sequence,
    Guid StationId,
    string Name,
    string CallType,
    string Status,
    bool IsAdded,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTime? ScheduledArrival,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTime? ExpectedArrival,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTime? ActualArrival,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTime? ScheduledDeparture,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTime? ExpectedDeparture,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTime? ActualDeparture,
    string PlannedPlatform,
    string Platform,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? PreviousPlatform);

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
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] JourneyHoldResponse? Hold,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] JourneyReasonResponse? Reason,
    int Version,
    IReadOnlyList<JourneyStopResponse> Stops,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] TrainSetResponse? TrainSet,
    // Where the journey now ends after an early termination (R-14); null for every other journey (19 Oct).
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] JourneyStationResponse? TerminatesAt = null);
