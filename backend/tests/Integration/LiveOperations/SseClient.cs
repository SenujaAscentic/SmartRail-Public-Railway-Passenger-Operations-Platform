using System.Net;
using System.Text.Json;

namespace SmartRail.IntegrationTests.LiveOperations;

// One frame of a Server-Sent Events stream: its id, event name and data lines, or a comment line (the heartbeat).
internal sealed record SseFrame(string? Id, string? Event, string? Data, string? Comment)
{
    public bool IsComment => Comment is not null;
    public long SequenceId => long.Parse(Id!);
    public JsonElement Json => JsonDocument.Parse(Data!).RootElement;
}

// A minimal client for GET /stream, written the way the real clients read it: one line at a time, a blank line ends a frame.
internal sealed class SseClient : IDisposable
{
    private readonly HttpResponseMessage _response;
    private readonly StreamReader? _reader;

    public HttpStatusCode Status => _response.StatusCode;
    public string? ContentType => _response.Content.Headers.ContentType?.MediaType;
    public HttpResponseMessage Response => _response;

    private SseClient(HttpResponseMessage response, StreamReader? reader)
    {
        _response = response;
        _reader = reader;
    }

    public static async Task<SseClient> ConnectAsync(HttpClient http, string pathAndQuery, string? lastEventId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, pathAndQuery);
        if (lastEventId is not null)
        {
            request.Headers.Add("Last-Event-ID", lastEventId);
        }

        var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        var streaming = response.IsSuccessStatusCode && response.Content.Headers.ContentType?.MediaType == "text/event-stream";
        return new SseClient(response, streaming ? new StreamReader(await response.Content.ReadAsStreamAsync(ct)) : null);
    }

    // The next frame (a heartbeat comment counts), or null when nothing arrives within the timeout.
    public async Task<SseFrame?> NextAsync(TimeSpan timeout, CancellationToken ct)
    {
        string? id = null, name = null, comment = null;
        var data = new List<string>();
        using var timer = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timer.CancelAfter(timeout);

        try
        {
            while (true)
            {
                var line = await _reader!.ReadLineAsync(timer.Token);
                if (line is null)
                {
                    return null;   // the server closed the stream
                }

                if (line.Length == 0)
                {
                    if (id is null && name is null && data.Count == 0 && comment is null)
                    {
                        continue;
                    }

                    return new SseFrame(id, name, data.Count > 0 ? string.Join("\n", data) : null, comment);
                }

                if (line.StartsWith(':'))
                {
                    comment = line[1..].Trim();
                }
                else if (line.StartsWith("id:"))
                {
                    id = line[3..].Trim();
                }
                else if (line.StartsWith("event:"))
                {
                    name = line[6..].Trim();
                }
                else if (line.StartsWith("data:"))
                {
                    data.Add(line[5..].TrimStart());
                }
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && (ex is OperationCanceledException || ex.InnerException is OperationCanceledException))
        {
            return null;   // the test host reports a cancelled read as an IOException around the cancellation
        }
    }

    // The next real event: heartbeats are skipped. Null when none arrives in time.
    public async Task<SseFrame?> NextEventAsync(TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var frame = await NextAsync(deadline - DateTime.UtcNow, ct);
            if (frame is null)
            {
                return null;
            }

            if (!frame.IsComment)
            {
                return frame;
            }
        }

        return null;
    }

    public void Dispose()
    {
        _reader?.Dispose();
        _response.Dispose();
    }
}
