using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartRail.LiveOperations.Outbox;
using SmartRail.LiveOperations.Persistence;
using SmartRail.LiveOperations.Stream;

namespace SmartRail.LiveOperations.Features.StreamEvents;

// How often an idle connection gets a comment line. Contract section 4: 15 s.
internal sealed record StreamSettings(TimeSpan Heartbeat);

// One client's connection to the public stream (contract section 4).
//
// Order matters, and is what makes "no event is lost between the snapshot and the live part":
//   1. subscribe to the hub FIRST, so everything dispatched from now on is waiting in this client's buffer;
//   2. read the newest dispatched sequence, then the snapshot or the replay (so the state read is at least that new);
//   3. send live events, skipping any whose sequence the snapshot or replay already covers.
// An event that is both in the snapshot and live is harmless: events say "the value is now X", they do not add up.
//
// The heartbeat wait is a REAL timer (see OutboxDispatcher for why TimeProvider is not used for waiting).
internal sealed class StreamHandler(
    LiveOperationsDbContext db, StreamHub hub, SnapshotReader snapshots, StreamSettings settings, ILogger<StreamHandler> log)
{
    // What a client is told when it cannot be caught up from the log (an example is in shared/events/resync.json).
    internal const string ResyncData = """{"reason":"The missed events are no longer kept."}""";

    public async Task RunAsync(HttpContext http, StreamFilter filter, long? lastEventId, CancellationToken cancellationToken)
    {
        var response = http.Response;
        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-store";
        response.Headers["X-Accel-Buffering"] = "no";   // a reverse proxy must not hold the events back
        http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        await response.StartAsync(cancellationToken);   // the client sees "200" now, not at the first event

        using var subscription = hub.Subscribe(filter);

        var dispatched = db.OutboxEvents.AsNoTracking().Where(e => e.Status == OutboxStatus.Dispatched && e.Sequence != null);
        var newest = await dispatched.MaxAsync(e => e.Sequence, cancellationToken);
        var oldest = await dispatched.MinAsync(e => e.Sequence, cancellationToken);
        var covered = newest ?? 0;   // live events up to here are already in the snapshot or the replay

        switch (ResumeDecision.Of(lastEventId, oldest, newest))
        {
            case ResumeAction.Resume:
                await ReplayAsync(response, filter, lastEventId!.Value, covered, cancellationToken);
                break;

            case ResumeAction.Resync:
                // No id on this event: it must not change the client's last event id.
                await WriteAsync(response, SseFormat.Event(null, "resync", ResyncData), cancellationToken);
                await SendSnapshotsAsync(response, filter, covered, cancellationToken);
                break;

            default:
                await SendSnapshotsAsync(response, filter, covered, cancellationToken);
                break;
        }

        await SendLiveAsync(response, subscription, covered, cancellationToken);
    }

    private async Task SendSnapshotsAsync(HttpResponse response, StreamFilter filter, long sequence, CancellationToken ct)
    {
        foreach (var snapshot in await snapshots.ReadAsync(filter, sequence, ct))
        {
            await WriteAsync(response, SseFormat.Event(sequence, "snapshot", JsonSerializer.Serialize(snapshot, EventJson.Options)), ct);
        }
    }

    // The log keeps 24 h of dispatched events. Everything after what the client has, up to what is covered now, in order.
    private async Task ReplayAsync(HttpResponse response, StreamFilter filter, long after, long upTo, CancellationToken ct)
    {
        var rows = await db.OutboxEvents.AsNoTracking()
            .Where(e => e.Status == OutboxStatus.Dispatched && e.Sequence > after && e.Sequence <= upTo)
            .OrderBy(e => e.Sequence)
            .ToListAsync(ct);

        foreach (var row in rows)
        {
            var envelope = JsonSerializer.Deserialize<EventEnvelope>(row.Json, EventJson.Options);
            var publicEvent = envelope is null ? null : PublicProjection.Of(envelope, row.Sequence!.Value);
            if (publicEvent is not null && filter.Matches(publicEvent))
            {
                await WriteEventAsync(response, publicEvent, ct);
            }
        }
    }

    private async Task SendLiveAsync(HttpResponse response, Subscription subscription, long covered, CancellationToken ct)
    {
        var lastSent = covered;
        while (!ct.IsCancellationRequested)
        {
            using var wait = CancellationTokenSource.CreateLinkedTokenSource(ct);
            wait.CancelAfter(settings.Heartbeat);

            bool more;
            try
            {
                more = await subscription.Reader.WaitToReadAsync(wait.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                await WriteAsync(response, SseFormat.Heartbeat, ct);   // quiet for a while: show the connection is alive
                continue;
            }
            catch (SubscriberTooSlowException)
            {
                log.LogInformation("A stream client fell too far behind and was disconnected; it can resume with Last-Event-ID.");
                return;
            }

            if (!more)
            {
                return;   // the subscription was disposed
            }

            while (subscription.Reader.TryRead(out var publicEvent))
            {
                if (publicEvent.Sequence > lastSent)
                {
                    await WriteEventAsync(response, publicEvent, ct);
                    lastSent = publicEvent.Sequence;
                }
            }
        }
    }

    private static Task WriteEventAsync(HttpResponse response, PublicEvent publicEvent, CancellationToken ct) =>
        WriteAsync(response, SseFormat.Event(publicEvent.Sequence, publicEvent.Type, JsonSerializer.Serialize(publicEvent, EventJson.Options)), ct);

    private static async Task WriteAsync(HttpResponse response, string text, CancellationToken ct)
    {
        await response.WriteAsync(text, ct);
        await response.Body.FlushAsync(ct);
    }
}
