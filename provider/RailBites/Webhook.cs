using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace RailBites;

// The signature of a SmartRail webhook (domain model 8.3): "sha256=" and the hex of HMAC-SHA256(shared secret, timestamp + body).
// The timestamp is part of what is signed, so an old request cannot be replayed with a fresh timestamp.
public static class WebhookSignature
{
    public static string Compute(string secret, string timestamp, string body)
    {
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(timestamp + body));
        return "sha256=" + Convert.ToHexString(mac).ToLowerInvariant();
    }

    // Compared in constant time, so the check does not leak how much of a guess was right.
    public static bool Matches(string secret, string timestamp, string body, string? signature) =>
        signature is not null
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Compute(secret, timestamp, body)), Encoding.UTF8.GetBytes(signature));
}

public sealed record WebhookBody(
    Guid? EventId, string? Type, DateTimeOffset? OccurredAt, string? ServiceNumber, DateOnly? ServiceDate, string? StationRef,
    DateTimeOffset? ScheduledArrival, DateTimeOffset? ExpectedArrival, string? Change, string? Reason);

// POST /provider-api/v1/webhooks/smartrail (contract 3.6, US-F2): rejects a bad signature or a request older than 5 minutes (401),
// treats an event it already has as done (200), stores a new one, and answers within the 5 seconds SmartRail waits.
internal static class WebhookEndpoint
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(5);
    private static readonly string[] Types = ["provider.eta-updated", "provider.journey-changed"];

    public static void Map(IEndpointRouteBuilder routes) =>
        routes.MapPost("/provider-api/v1/webhooks/smartrail", HandleAsync);

    private static async Task<IResult> HandleAsync(HttpRequest request, RailBitesDbContext db, TimeProvider clock, IConfiguration configuration, CancellationToken ct)
    {
        var secret = configuration["RailBites:SharedSecret"];
        if (string.IsNullOrEmpty(secret))
        {
            return Results.Problem("The shared secret is not set.", statusCode: 500);
        }

        // The body is read as text first: the signature is over exactly these characters.
        using var reader = new StreamReader(request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(ct);
        var timestamp = request.Headers["X-SmartRail-Timestamp"].ToString();
        var signature = request.Headers["X-SmartRail-Signature"].ToString();

        if (timestamp == "" || !WebhookSignature.Matches(secret, timestamp, body, signature == "" ? null : signature))
        {
            return Results.Problem("The signature is not valid.", statusCode: StatusCodes.Status401Unauthorized);
        }

        // Signed correctly, but too old (or from the future): a replay.
        if (!DateTimeOffset.TryParse(timestamp, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var sent)
            || (clock.GetUtcNow() - sent).Duration() > MaxAge)
        {
            return Results.Problem("The timestamp is too old.", statusCode: StatusCodes.Status401Unauthorized);
        }

        WebhookBody? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<WebhookBody>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException)
        {
            return Results.Problem("The body is not valid JSON.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (parsed is not { EventId: { } eventId, Type: { } type, OccurredAt: { } occurredAt, ServiceNumber: { Length: > 0 } number, ServiceDate: { } date, StationRef: { Length: > 0 } stationRef }
            || !Types.Contains(type)
            || !Guid.TryParse(request.Headers["X-SmartRail-Event-Id"], out var headerId) || headerId != eventId)
        {
            return Results.Problem("The event is incomplete, of an unknown type, or its id does not match the header.", statusCode: StatusCodes.Status400BadRequest);
        }

        // A repeat of an event we have is already processed: 200, nothing stored again.
        if (await db.Events.AnyAsync(e => e.EventId == eventId, ct))
        {
            return Results.Ok(new { status = "duplicate" });
        }

        db.Events.Add(new ReceivedEvent
        {
            EventId = eventId, Type = type, ReceivedAt = clock.GetUtcNow().UtcDateTime, OccurredAt = occurredAt.UtcDateTime,
            ServiceNumber = number, ServiceDate = date, StationRef = stationRef,
            ScheduledArrival = parsed.ScheduledArrival?.UtcDateTime, ExpectedArrival = parsed.ExpectedArrival?.UtcDateTime,
            Change = parsed.Change, Reason = parsed.Reason,
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Results.Ok(new { status = "duplicate" });   // the same event arrived twice at once: the key said no, so it is already here
        }

        return Results.Ok(new { status = "accepted" });
    }
}
