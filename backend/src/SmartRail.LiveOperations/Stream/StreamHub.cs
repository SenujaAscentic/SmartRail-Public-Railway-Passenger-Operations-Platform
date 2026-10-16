using System.Collections.Concurrent;
using System.Threading.Channels;

namespace SmartRail.LiveOperations.Stream;

// Which events one client wants (contract section 4). A client lists what it cares about, and an event is sent when it
// matches ANY of the lists: one connection can follow its saved trips and its home station.
internal sealed record StreamFilter(
    IReadOnlySet<Guid> JourneyIds,
    IReadOnlySet<Guid> StationIds,
    IReadOnlySet<Guid> LineIds)
{
    public bool IsEmpty => JourneyIds.Count == 0 && StationIds.Count == 0 && LineIds.Count == 0;

    public bool Matches(PublicEvent publicEvent) =>
        JourneyIds.Contains(publicEvent.JourneyId)
        || LineIds.Contains(publicEvent.LineId)
        || publicEvent.StationIds.Any(StationIds.Contains);
}

// Thrown to a client's reader when it fell too far behind; the connection is closed and the client resumes with Last-Event-ID.
internal sealed class SubscriberTooSlowException : Exception
{
    public SubscriberTooSlowException() : base("The stream client did not keep up.") { }
}

internal sealed class Subscription(StreamFilter filter, int bufferSize, Action<Subscription> onDispose) : IDisposable
{
    private readonly Channel<PublicEvent> _channel =
        Channel.CreateBounded<PublicEvent>(new BoundedChannelOptions(bufferSize) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    public StreamFilter Filter { get; } = filter;
    public ChannelReader<PublicEvent> Reader => _channel.Reader;

    // Called by the hub for every event. A full buffer means the client is not reading: cut it off (it resumes with
    // Last-Event-ID) rather than block the dispatcher or keep growing memory.
    internal void Deliver(PublicEvent publicEvent)
    {
        if (Filter.Matches(publicEvent) && !_channel.Writer.TryWrite(publicEvent))
        {
            _channel.Writer.TryComplete(new SubscriberTooSlowException());
        }
    }

    public void Dispose() => onDispose(this);
    internal void Complete() => _channel.Writer.TryComplete();
}

// The in-process fan-out from the dispatcher to the connected clients. One instance for the whole application (singleton).
internal sealed class StreamHub(int bufferSize = StreamHub.DefaultBufferSize) : IEventPublisher
{
    public const int DefaultBufferSize = 1000;

    private readonly ConcurrentDictionary<Subscription, byte> _subscriptions = new();

    public Subscription Subscribe(StreamFilter filter)
    {
        var subscription = new Subscription(filter, bufferSize, s =>
        {
            _subscriptions.TryRemove(s, out _);
            s.Complete();
        });
        _subscriptions[subscription] = 0;
        return subscription;
    }

    public void Publish(PublicEvent publicEvent)
    {
        foreach (var subscription in _subscriptions.Keys)
        {
            subscription.Deliver(publicEvent);
        }
    }
}
