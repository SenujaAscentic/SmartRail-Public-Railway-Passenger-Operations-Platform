using SmartRail.LiveOperations.Outbox;

namespace SmartRail.LiveOperations.Stream;

// The shapes of what `GET /stream` puts in the `data:` line, as typed records. Nothing sends them: the wire JSON is built by
// PublicProjection from the stored envelope. They exist so that openapi.yaml describes every event, and the apps' types are
// generated from the description instead of being written by hand (CLAUDE.md section 5, step 4). A test checks that the example
// files in shared/events fit these records exactly, so they cannot drift from the wire.
//
// `Type` is a plain string here; the Api's OpenAPI setup pins it to the one value of each event so the union can be told apart.

public sealed record StopStatusChangedEvent(
    Guid EventId, long Sequence, string Type, int Version, DateTime OccurredAt, Guid JourneyId, DateOnly ServiceDate,
    IReadOnlyList<Guid> StationIds, Guid LineId, StopStatusChangedPayload Payload);

public sealed record DelayChangedEvent(
    Guid EventId, long Sequence, string Type, int Version, DateTime OccurredAt, Guid JourneyId, DateOnly ServiceDate,
    IReadOnlyList<Guid> StationIds, Guid LineId, DelayChangedPayload Payload);

public sealed record DataFreshnessChangedEvent(
    Guid EventId, long Sequence, string Type, int Version, DateTime OccurredAt, Guid JourneyId, DateOnly ServiceDate,
    IReadOnlyList<Guid> StationIds, Guid LineId, DataFreshnessChangedPayload Payload);

// `event: resync`: the client cannot be caught up from the log and must start again from the snapshots that follow.
public sealed record StreamResync(string Reason);
