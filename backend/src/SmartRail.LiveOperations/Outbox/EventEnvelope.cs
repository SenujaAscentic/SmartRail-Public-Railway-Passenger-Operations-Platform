using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SmartRail.LiveOperations.Domain;

namespace SmartRail.LiveOperations.Outbox;

// Domain model 8.1: the fields every internal event carries. `sequence` is not here: it is given at dispatch (see OutboxEvent).
// `causedBy` is left out until the decision endpoints exist (nothing here knows an actor or a request id).
// Payload is a JSON object so the public projection can keep only the fields it allows (T-07).
internal sealed record EventEnvelope(
    Guid EventId,
    string Type,
    int Version,
    DateTime OccurredAt,
    Guid JourneyId,
    DateOnly ServiceDate,
    IReadOnlyList<Guid> StationIds,
    Guid LineId,
    Source Source,
    JsonObject Payload);

// Payloads, version 1 (domain model 8.2). Public, because the API description (openapi.yaml) is generated from them.
public sealed record StopStatusChangedPayload(Guid StationId, int Sequence, StopStatus From, StopStatus To, DateTime At);

// JourneyStarted: what the staff side keeps (which tablet, which cab session). NEVER public (T-07): the public stream sends no payload.
internal sealed record JourneyStartedPayload(Guid DeviceId, Guid CrewSessionId);

// What a public event with nothing to say beyond its type carries: an empty object.
public sealed record EmptyPayload;

public sealed record JourneyEndedPayload(JourneyEndReason Reason);

// JourneyHeld: a hold was set (Released false, with its minutes and reason) or ended (Released true: the controller released it or the train left).
public sealed record JourneyHeldPayload(Guid StationId, bool Released, int? Minutes, JourneyReason? Reason);

public sealed record JourneyCancelledPayload(JourneyReason Reason);

// PlatformChanged: LeadTimeMinutes is for the staff and the audit; it is not in the public stream (PublicProjection). HoldSet is true only
// when a hold was set with the change: apps may say "the train will wait" only then.
public sealed record PlatformChangedPayload(Guid StationId, string Platform, string PreviousPlatform, LeadTimeLevel LeadTimeLevel, int LeadTimeMinutes, bool HoldSet);

public sealed record GeoLocation(double Latitude, double Longitude);

public sealed record TrainPositionUpdatedPayload(GeoLocation Location, double SpeedKmh, Guid LastStationId, Guid NextStationId, double FractionToNext);

public sealed record RemainingStop(Guid StationId, DateTime? ExpectedArrival, DateTime? ExpectedDeparture);

// Reason is present only when the controller set the delay: the code and the plain-text note passengers see.
public sealed record DelayChangedPayload(int DelayMinutes, int PreviousDelayMinutes, IReadOnlyList<RemainingStop> Stops, JourneyReason? Reason = null);

public sealed record DataFreshnessChangedPayload(TrackingStatus From, TrackingStatus To, DateTime? LastSeenAt, Guid? LastStationId);

// One set of JSON rules for the outbox, the stream and the shared/events examples: camelCase, enums as their names,
// and fields with no value left out.
internal static class EventJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };
}
