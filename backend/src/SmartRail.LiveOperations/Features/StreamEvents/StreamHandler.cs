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
    LiveOperationsDbContext db, StreamHub hub, SnapshotReader snapshots, StreamSettings settings, TimeProvider clock, ILogger<StreamHandler> log)
{
    // What a client is told when it cannot be caught up from the log (an example is in shared/events/resync.json).
    internal const string ResyncData = """{"reason":"The missed events are no longer kept."}""";

    // `staff`: the staff stream (/ops/stream) writes each event's `source` too. `endsAt`: the end of the caller's session; the stream
    // ends when it is reached, so a signed-out or expired session does not keep receiving (the client refreshes and reconnects).
    public async Task RunAsync(
        HttpContext http, StreamFilter filter, long? lastEventId, CancellationToken requestAborted, bool staff = false, DateTimeOffset? endsAt = null)
    {
        // The timer runs on the application's clock, so it follows the demo clock like the token it is waiting for.
        using var expiry = endsAt is { } end
            ? new CancellationTokenSource(end - clock.GetUtcNow() is { } left && left > TimeSpan.Zero ? left : TimeSpan.Zero, clock)
            : new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(requestAborted, expiry.Token);
        var cancellationToken = linked.Token;

        try
        {
            await RunStreamAsync(http, filter, lastEventId, staff, cancellationToken);
        }
        catch (OperationCanceledException) when (expiry.IsCancellationRequested && !requestAborted.IsCancellationRequested)
        {
            // The session ended: that is a normal end of the stream, not an error.
        }
    }

    private async Task RunStreamAsync(HttpContext http, StreamFilter filter, long? lastEventId, bool staff, CancellationToken cancellationToken)
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
                await ReplayAsync(response, filter, lastEventId!.Value, covered, staff, cancellationToken);
                break;

            case ResumeAction.Resync:
                // No id on this event: it must not change the client's last event id.
                await WriteAsync(response, SseFormat.Event(null, "resync", ResyncData), cancellationToken);
                await SendSnapshotsAsync(response, filter, covered, staff, cancellationToken);
                break;

            default:
                await SendSnapshotsAsync(response, filter, covered, staff, cancellationToken);
                break;
        }

        await SendLiveAsync(response, subscription, covered, staff, cancellationToken);
    }

    private async Task SendSnapshotsAsync(HttpResponse response, StreamFilter filter, long sequence, bool staff, CancellationToken ct)
    {
        foreach (var snapshot in await snapshots.ReadAsync(filter, sequence, ct))
        {
            var json = JsonSerializer.SerializeToNode(snapshot, EventJson.Options)!.AsObject();
            if (staff)
            {
                // Staff only (US-O3 star criterion): who supplied the delay and each stop's actual times.
                json["delaySource"] = snapshot.DelaySource;
                if (snapshot.DelaySetAt is { } setAt) json["delaySetAt"] = setAt.ToUniversalTime().ToString("O");
                var stops = json["stops"]!.AsArray();
                for (var i = 0; i < snapshot.Stops.Count; i++)
                {
                    if (snapshot.Stops[i].ActualArrivalSource is { } arrival) stops[i]!["actualArrivalSource"] = arrival;
                    if (snapshot.Stops[i].ActualDepartureSource is { } departure) stops[i]!["actualDepartureSource"] = departure;
                }
            }

            await WriteAsync(response, SseFormat.Event(sequence, "snapshot", json.ToJsonString(EventJson.Options)), ct);
        }
    }

    // The log keeps 24 h of dispatched events. Everything after what the client has, up to what is covered now, in order.
    private async Task ReplayAsync(HttpResponse response, StreamFilter filter, long after, long upTo, bool staff, CancellationToken ct)
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
                await WriteEventAsync(response, publicEvent, staff, ct);
            }
        }
    }

    private async Task SendLiveAsync(HttpResponse response, Subscription subscription, long covered, bool staff, CancellationToken ct)
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
                    await WriteEventAsync(response, publicEvent, staff, ct);
                    lastSent = publicEvent.Sequence;
                }
            }
        }
    }

    // The public stream sends the event as PublicEvent serialises it (no source). The staff stream adds `source` to the same object.
    private static Task WriteEventAsync(HttpResponse response, PublicEvent publicEvent, bool staff, CancellationToken ct)
    {
        var json = JsonSerializer.SerializeToNode(publicEvent, EventJson.Options)!.AsObject();
        if (staff && publicEvent.Source is { } source)
        {
            json["source"] = source;
        }

        return WriteAsync(response, SseFormat.Event(publicEvent.Sequence, publicEvent.Type, json.ToJsonString(EventJson.Options)), ct);
    }

    private static async Task WriteAsync(HttpResponse response, string text, CancellationToken ct)
    {
        await response.WriteAsync(text, ct);
        await response.Body.FlushAsync(ct);
    }
}
