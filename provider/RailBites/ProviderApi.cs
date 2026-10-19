using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace RailBites;

public sealed record OpeningHoursResponse(string Opens, string Closes);
public sealed record StationResponse(string Ref, string Name, string Hotline, OpeningHoursResponse OpeningHours, bool Available);
public sealed record MenuItemResponse(int Id, string Name, int PriceLkr, bool Available);
public sealed record MenuResponse(string StationRef, IReadOnlyList<MenuItemResponse> Items);

// What SmartRail pulls every 5 minutes (contract 3.6, US-F3): the catering stations (with hotline and opening hours) and their menus.
// Protected by SmartRail's API key at the provider, sent in the header X-Api-Key.
internal static class ProviderApi
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/provider-api/v1").AddEndpointFilter(ApiKeyFilter);

        group.MapGet("/stations", async (RailBitesDbContext db, CancellationToken ct) =>
            (await db.Stations.AsNoTracking().OrderBy(s => s.Name).ToListAsync(ct))
                .Select(s => new StationResponse(s.Ref, s.Name, s.Hotline, new OpeningHoursResponse(s.Opens.ToString("HH:mm"), s.Closes.ToString("HH:mm")), s.Available)));

        group.MapGet("/stations/{stationRef}/menu", async (string stationRef, RailBitesDbContext db, CancellationToken ct) =>
        {
            if (!await db.Stations.AnyAsync(s => s.Ref == stationRef, ct))
            {
                return Results.NotFound();
            }

            var items = await db.MenuItems.AsNoTracking().Where(i => i.StationRef == stationRef).OrderBy(i => i.Id).ToListAsync(ct);
            return Results.Ok(new MenuResponse(stationRef, items.Select(i => new MenuItemResponse(i.Id, i.Name, i.PriceLkr, i.Available)).ToList()));
        });
    }

    // 401 without the right key. The comparison takes the same time whatever part of the key is right.
    private static async ValueTask<object?> ApiKeyFilter(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var expected = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>()["RailBites:ApiKey"];
        var given = context.HttpContext.Request.Headers["X-Api-Key"].ToString();
        var ok = !string.IsNullOrEmpty(expected) && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(given));
        return ok ? await next(context) : Results.Problem("The API key is missing or wrong.", statusCode: (int)HttpStatusCode.Unauthorized);
    }
}

// The event log page (US-F1 AC3): every event received from SmartRail, newest first. Plain text only: whatever an event contains
// (a reason is free text) is HTML-encoded and can never become markup. A JSON copy is at /events.json for the tests.
internal static class EventsPage
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/events.json", async (RailBitesDbContext db, CancellationToken ct) =>
            await db.Events.AsNoTracking().OrderByDescending(e => e.ReceivedAt).Take(500).ToListAsync(ct));

        routes.MapGet("/events", async (RailBitesDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var events = await db.Events.AsNoTracking().OrderByDescending(e => e.ReceivedAt).ThenByDescending(e => e.OccurredAt).Take(200).ToListAsync(ct);
            var colombo = TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo");
            string Local(DateTime? utc) => utc is null ? "—" : TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc), colombo).ToString("dd MMM HH:mm:ss");
            string E(string? text) => WebUtility.HtmlEncode(text ?? "—");

            var page = new StringBuilder("""
                <!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
                <title>RailBites: events from SmartRail</title>
                <style>body{font-family:system-ui,sans-serif;margin:2rem;color:#14202B}table{border-collapse:collapse;width:100%}th,td{text-align:left;padding:.4rem .6rem;border-bottom:1px solid #C9D1D9}
                th{color:#4A5560}.eta{font-weight:700}.change{color:#A8201A;font-weight:700}code{font-family:ui-monospace,monospace}</style></head><body>
                <h1>RailBites &middot; events from SmartRail</h1>
                """);
            page.Append($"<p>{events.Count} event(s), newest first. Times are Sri Lanka time. Received events are never changed.</p>");
            if (events.Count == 0)
            {
                page.Append("<p>No events received yet.</p>");
            }
            else
            {
                page.Append("<table><thead><tr><th>Received</th><th>Event</th><th>Train</th><th>Station</th><th>Scheduled</th><th>New ETA</th><th>Change</th><th>Reason</th><th>Event id</th></tr></thead><tbody>");
                foreach (var e in events)
                {
                    var kind = e.Type == "provider.eta-updated" ? "<span class=\"eta\">ETA updated</span>" : "<span class=\"change\">Journey changed</span>";
                    page.Append($"<tr><td>{E(Local(e.ReceivedAt))}</td><td>{kind}</td><td>{E(e.ServiceNumber)} &middot; {e.ServiceDate:yyyy-MM-dd}</td><td>{E(e.StationRef)}</td>" +
                                $"<td>{E(Local(e.ScheduledArrival))}</td><td>{E(Local(e.ExpectedArrival))}</td><td>{E(e.Change)}</td><td>{E(e.Reason)}</td><td><code>{e.EventId}</code></td></tr>");
                }

                page.Append("</tbody></table>");
            }

            page.Append("</body></html>");
            return Results.Content(page.ToString(), "text/html; charset=utf-8");
        });
    }
}
