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

// Payloads, version 1 (domain model 8.2).
internal sealed record StopStatusChangedPayload(Guid StationId, int Sequence, StopStatus From, StopStatus To, DateTime At);

internal sealed record RemainingStop(Guid StationId, DateTime? ExpectedArrival, DateTime? ExpectedDeparture);

internal sealed record DelayChangedPayload(int DelayMinutes, int PreviousDelayMinutes, IReadOnlyList<RemainingStop> Stops);

internal sealed record DataFreshnessChangedPayload(TrackingStatus From, TrackingStatus To, DateTime? LastSeenAt, Guid? LastStationId);

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
