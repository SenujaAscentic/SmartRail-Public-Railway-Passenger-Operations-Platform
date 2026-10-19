using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartRail.CateringIntegration.Persistence;
using SmartRail.LiveOperations;
using SmartRail.Network;

namespace SmartRail.CateringIntegration.Relay;

public sealed class CateringOptions
{
    public string? ProviderUrl { get; set; }       // Catering:ProviderUrl, e.g. http://localhost:5100
    public string? SharedSecret { get; set; }      // Catering:SharedSecret: signs the webhooks (user-secrets, never in the repo)
    public string? ProviderApiKey { get; set; }    // Catering:ProviderApiKey: sent when SmartRail pulls menus
    public bool RelayEnabled { get; set; } = true; // Catering:RelayEnabled: tests switch the background loop off and call RunOnceAsync
}

// Turns journey events into signed webhooks to the provider (US-F2), and delivers them with retries.
// Two steps in one loop: (1) read new events from LiveOperations' feed and store the webhooks they cause (with the cursor, one
// transaction); (2) send the webhooks that are due. Storing before sending means a provider outage loses nothing.
internal sealed class RelayService(
    IServiceScopeFactory scopes, IHttpClientFactory httpClients, IOptions<CateringOptions> options, TimeProvider time, ILogger<RelayService> logger)
    : BackgroundService
{
    // The wait between attempts: 10 s, 30 s, 1 min, 5 min, 15 min, then the delivery is marked Failed (US-F2 AC3).
    public static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15)];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.RelayEnabled || string.IsNullOrWhiteSpace(options.Value.ProviderUrl) || string.IsNullOrWhiteSpace(options.Value.SharedSecret))
        {
            logger.LogInformation("Catering relay is off (no Catering:ProviderUrl / Catering:SharedSecret, or switched off).");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // Never takes the application down; the next round tries again. Only the type is logged (no data).
                logger.LogWarning("Catering relay round failed: {ErrorType}", e.GetType().Name);
            }

            // A real timer on purpose: this is scheduling, not business time (stored timestamps still come from TimeProvider).
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    // One round. Public to the tests so they do not depend on a timer.
    internal async Task RunOnceAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        await ReadNewEventsAsync(scope.ServiceProvider, ct);
        await DeliverDueAsync(scope.ServiceProvider, ct);
    }

    private async Task ReadNewEventsAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<CateringDbContext>();
        var feed = services.GetRequiredService<IJourneyFeed>();
        var network = services.GetRequiredService<INetworkCatalog>();

        var cursor = await db.Cursor.SingleOrDefaultAsync(ct);
        if (cursor is null)
        {
            // First start: begin at today's newest event, so history is not replayed to the provider.
            cursor = new RelayCursor { LastSequence = await feed.GetLatestSequenceAsync(ct) };
            db.Cursor.Add(cursor);
            await db.SaveChangesAsync(ct);
        }

        var events = await feed.ReadAfterAsync(cursor.LastSequence, 100, ct);
        if (events.Count == 0)
        {
            return;
        }

        var cateringRefs = (await network.GetCateringStationsAsync(ct)).ToDictionary(s => s.StationId, s => s.Code);
        var now = time.GetUtcNow().UtcDateTime;

        foreach (var source in events)
        {
            cursor.LastSequence = source.Sequence;
            if (!ProviderEventRules.IsRelevant(source.Type))
            {
                continue;
            }

            var state = await feed.GetStateAsync(source.JourneyId, ct);
            var service = state is null ? null : (await network.GetServicesAsync([state.ServiceId], ct)).GetValueOrDefault(state.ServiceId);
            if (state is null || service is null)
            {
                continue;
            }

            var sent = await db.SentEtas.Where(x => x.JourneyId == source.JourneyId).ToListAsync(ct);
            var made = ProviderEventRules.Create(source, state, service.Number, cateringRefs, sent.ToDictionary(x => x.StationRef, x => x.ExpectedArrival), Guid.NewGuid);

            foreach (var providerEvent in made)
            {
                db.Deliveries.Add(new Delivery
                {
                    Id = providerEvent.EventId,
                    Type = providerEvent.Type,
                    BodyJson = JsonSerializer.Serialize(providerEvent, BodyOptions),
                    Status = DeliveryStatus.Pending,
                    CreatedAt = now,
                    NextAttemptAt = now,
                });

                if (providerEvent is { Type: ProviderEvent.EtaUpdated, ExpectedArrival: { } expected })
                {
                    var row = sent.SingleOrDefault(x => x.StationRef == providerEvent.StationRef);
                    if (row is null)
                    {
                        db.SentEtas.Add(new SentEta { JourneyId = source.JourneyId, StationRef = providerEvent.StationRef, ExpectedArrival = expected });
                    }
                    else
                    {
                        row.ExpectedArrival = expected;
                    }
                }
            }
        }

        await db.SaveChangesAsync(ct);   // cursor + deliveries + last-sent ETAs together
    }

    private async Task DeliverDueAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<CateringDbContext>();
        var now = time.GetUtcNow().UtcDateTime;
        var due = await db.Deliveries
            .Where(d => d.Status == DeliveryStatus.Pending && d.NextAttemptAt <= now)
            .OrderBy(d => d.CreatedAt).Take(20).ToListAsync(ct);

        foreach (var delivery in due)
        {
            var error = await SendAsync(delivery, now, ct);
            delivery.Attempts++;
            if (error is null)
            {
                delivery.Status = DeliveryStatus.Delivered;
                delivery.DeliveredAt = now;
                delivery.LastError = null;
            }
            else
            {
                delivery.LastError = error;
                if (delivery.Attempts > RetryDelays.Length)
                {
                    delivery.Status = DeliveryStatus.Failed;
                    logger.LogWarning("Catering webhook {EventId} failed for good after {Attempts} attempts ({Error})", delivery.Id, delivery.Attempts, error);
                }
                else
                {
                    delivery.NextAttemptAt = now + RetryDelays[delivery.Attempts - 1];
                }
            }

            await db.SaveChangesAsync(ct);
        }
    }

    // Returns null when the provider accepted it (any 2xx), otherwise a short code. The body and the response are never logged or stored.
    private async Task<string?> SendAsync(Delivery delivery, DateTime now, CancellationToken ct)
    {
        var timestamp = now.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var secret = options.Value.SharedSecret!;
        var signature = "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(timestamp + delivery.BodyJson)));

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(options.Value.ProviderUrl!), "/provider-api/v1/webhooks/smartrail"))
        {
            Content = new StringContent(delivery.BodyJson, new UTF8Encoding(false), new MediaTypeHeaderValue("application/json")),
        };
        request.Headers.Add("X-SmartRail-Event-Id", delivery.Id.ToString());
        request.Headers.Add("X-SmartRail-Timestamp", timestamp);
        request.Headers.Add("X-SmartRail-Signature", signature);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            using var response = await httpClients.CreateClient("railbites").SendAsync(request, timeout.Token);
            return response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}";
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return "Timeout";
        }
        catch (HttpRequestException)
        {
            return "Unreachable";
        }
    }

    // camelCase like every other JSON in the system; the provider reads these names (domain model 8.3).
    private static readonly JsonSerializerOptions BodyOptions = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
}
