using System.Text.Json.Nodes;
using SmartRail.LiveOperations.Outbox;

namespace SmartRail.LiveOperations.Stream;

// What the PUBLIC stream may show (threat T-07: no source, staff IDs, crewSessionId, crew reports or alerts).
// An allow-list, not a strip-list: an event type or a payload field that is not named here is never sent, so a new field
// added to an event later stays private until someone decides it is public.
internal static class PublicProjection
{
    private static readonly Dictionary<string, string[]> PublicPayloadFields = new()
    {
        ["StopStatusChanged"] = ["stationId", "sequence", "from", "to", "at"],
        ["DelayChanged"] = ["delayMinutes", "previousDelayMinutes", "stops", "reason"],
        ["DataFreshnessChanged"] = ["from", "to", "lastSeenAt", "lastStationId"],
        ["JourneyStarted"] = [],            // the stored payload (device, cab session) is staff-only: nothing of it is public
        ["JourneyEnded"] = ["reason"],
        ["JourneyHeld"] = ["stationId", "released", "minutes", "reason"],
        ["TrainPositionUpdated"] = ["location", "speedKmh", "lastStationId", "nextStationId", "fractionToNext"],
    };

    // The event types the public stream sends. Each has an example in shared/events (CLAUDE.md section 5, step 5).
    public static IEnumerable<string> PublicEventTypes => PublicPayloadFields.Keys;

    // Null means "not for the public stream".
    public static PublicEvent? Of(EventEnvelope envelope, long sequence)
    {
        if (!PublicPayloadFields.TryGetValue(envelope.Type, out var fields))
        {
            return null;
        }

        var payload = new JsonObject();
        foreach (var name in fields)
        {
            if (envelope.Payload.TryGetPropertyValue(name, out var value) && value is not null)
            {
                payload[name] = value.DeepClone();
            }
        }

        // `Source` is deliberately not copied.
        return new PublicEvent(
            envelope.EventId, sequence, envelope.Type, envelope.Version, envelope.OccurredAt,
            envelope.JourneyId, envelope.ServiceDate, envelope.StationIds, envelope.LineId, payload);
    }
}
