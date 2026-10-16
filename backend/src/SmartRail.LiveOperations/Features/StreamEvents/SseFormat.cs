namespace SmartRail.LiveOperations.Features.StreamEvents;

// The text of a Server-Sent Events stream: `id`, `event` and `data` lines, then a blank line (contract section 4).
internal static class SseFormat
{
    // A comment line: proxies keep the connection open and clients can notice silence. Clients ignore it.
    public const string Heartbeat = ": keep-alive\n\n";

    // Where the events come from: the event JSON is one line, so a new line in the type or data can only be a mistake or
    // an attack (it would end the frame and let content pose as another field), and is refused.
    public static string Event(long? id, string type, string json)
    {
        if (ContainsLineBreak(type) || ContainsLineBreak(json))
        {
            throw new ArgumentException("An event type or its data must not contain a line break.");
        }

        return (id is null ? "" : $"id: {id}\n") + $"event: {type}\ndata: {json}\n\n";
    }

    private static bool ContainsLineBreak(string text) => text.Contains('\n') || text.Contains('\r');
}
