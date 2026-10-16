using System.Text.Json.Nodes;

namespace SmartRail.LiveOperations.Stream;

// An event as the PUBLIC stream sends it (contract section 4, threat T-07): the envelope of domain model 8.1 without the
// staff-only fields (source, causedBy), plus the stream sequence. Built only by PublicProjection, from an allow-list.
internal sealed record PublicEvent(
    Guid EventId,
    long Sequence,
    string Type,
    int Version,
    DateTime OccurredAt,
    Guid JourneyId,
    DateOnly ServiceDate,
    IReadOnlyList<Guid> StationIds,
    Guid LineId,
    JsonObject Payload);

// Where the dispatcher hands events to. The stream hub is the real one; tests put a failing one in front of it.
internal interface IEventPublisher
{
    void Publish(PublicEvent publicEvent);
}
