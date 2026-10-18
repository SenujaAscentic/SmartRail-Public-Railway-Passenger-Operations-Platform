using SmartRail.LiveOperations.Domain;
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

public sealed record JourneyStartedEvent(
    Guid EventId, long Sequence, string Type, int Version, DateTime OccurredAt, Guid JourneyId, DateOnly ServiceDate,
    IReadOnlyList<Guid> StationIds, Guid LineId, EmptyPayload Payload);

public sealed record JourneyEndedEvent(
    Guid EventId, long Sequence, string Type, int Version, DateTime OccurredAt, Guid JourneyId, DateOnly ServiceDate,
    IReadOnlyList<Guid> StationIds, Guid LineId, JourneyEndedPayload Payload);

public sealed record JourneyHeldEvent(
    Guid EventId, long Sequence, string Type, int Version, DateTime OccurredAt, Guid JourneyId, DateOnly ServiceDate,
    IReadOnlyList<Guid> StationIds, Guid LineId, JourneyHeldPayload Payload);

// The public PlatformChanged: the payload without leadTimeMinutes, which only staff and the audit see.
public sealed record PlatformChangedPublicPayload(Guid StationId, string Platform, string PreviousPlatform, LeadTimeLevel LeadTimeLevel, bool HoldSet);

public sealed record PlatformChangedEvent(
    Guid EventId, long Sequence, string Type, int Version, DateTime OccurredAt, Guid JourneyId, DateOnly ServiceDate,
    IReadOnlyList<Guid> StationIds, Guid LineId, PlatformChangedPublicPayload Payload);

public sealed record JourneyCancelledEvent(
    Guid EventId, long Sequence, string Type, int Version, DateTime OccurredAt, Guid JourneyId, DateOnly ServiceDate,
    IReadOnlyList<Guid> StationIds, Guid LineId, JourneyCancelledPayload Payload);

public sealed record JourneyTerminatedEarlyEvent(
    Guid EventId, long Sequence, string Type, int Version, DateTime OccurredAt, Guid JourneyId, DateOnly ServiceDate,
    IReadOnlyList<Guid> StationIds, Guid LineId, JourneyTerminatedEarlyPayload Payload);

public sealed record StopSkippedEvent(
    Guid EventId, long Sequence, string Type, int Version, DateTime OccurredAt, Guid JourneyId, DateOnly ServiceDate,
    IReadOnlyList<Guid> StationIds, Guid LineId, StopSkippedPayload Payload);

public sealed record JourneyReinstatedEvent(
    Guid EventId, long Sequence, string Type, int Version, DateTime OccurredAt, Guid JourneyId, DateOnly ServiceDate,
    IReadOnlyList<Guid> StationIds, Guid LineId, EmptyPayload Payload);

public sealed record TrainPositionUpdatedEvent(
    Guid EventId, long Sequence, string Type, int Version, DateTime OccurredAt, Guid JourneyId, DateOnly ServiceDate,
    IReadOnlyList<Guid> StationIds, Guid LineId, TrainPositionUpdatedPayload Payload);

// `event: resync`: the client cannot be caught up from the log and must start again from the snapshots that follow.
public sealed record StreamResync(string Reason);
