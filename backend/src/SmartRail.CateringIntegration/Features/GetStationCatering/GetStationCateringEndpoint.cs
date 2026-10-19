using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SmartRail.CateringIntegration.Menus;
using SmartRail.CateringIntegration.Persistence;
using SmartRail.Network;

namespace SmartRail.CateringIntegration.Features.GetStationCatering;

// Opening hours are a Colombo time of day ("HH:mm"), not an instant: the one deliberate exception to "UTC everywhere" (contract 3.6).
public sealed record OpeningHoursResponse(string Opens, string Closes);

// Prices are whole rupees (LKR).
public sealed record MenuItemResponse(string Name, int Price, bool Available);

// `Available` false means "Menu unavailable": no usable menu now (nothing cached, too old, the provider says it is closed for orders, or
// the station has no catering). Then the menu is empty; the hotline and hours are given when known, else "" and null.
public sealed record StationCateringResponse(bool Available, bool OpenNow, string Hotline, OpeningHoursResponse? OpeningHours, IReadOnlyList<MenuItemResponse> Menu);

// GET /api/v1/stations/{stationId}/catering   (contract 3.1 and 3.6, policy Public, US-P8, US-F3)
// Always answers from the cache: the provider being down never makes this, or any train information, fail.
internal static class GetStationCateringEndpoint
{
    public static void Map(IEndpointRouteBuilder routes) =>
        routes.MapGet("/stations/{stationId}/catering", HandleAsync)
            .AllowAnonymous()
            .WithName("GetStationCatering")
            .WithTags("CateringIntegration")
            .Produces<StationCateringResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<IResult> HandleAsync(
        string stationId, INetworkCatalog network, CateringDbContext db, TimeProvider time, HttpContext http, CancellationToken ct)
    {
        var known = Guid.TryParse(stationId, out var id) && (await network.GetStationNamesAsync([id], ct)).ContainsKey(id);
        if (!known)
        {
            return Results.Problem(
                type: "https://smartrail.example/errors/not-found", title: "Not found", statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?> { ["code"] = "NOT_FOUND", ["requestId"] = http.TraceIdentifier });
        }

        var code = (await network.GetCateringStationsAsync(ct)).FirstOrDefault(c => c.StationId == id)?.Code;
        var cached = code is null ? null : await db.CachedStations.AsNoTracking().SingleOrDefaultAsync(c => c.StationRef == code, ct);
        if (cached is null)
        {
            return Results.Ok(new StationCateringResponse(false, false, "", null, []));
        }

        var now = time.GetUtcNow();
        var available = CateringRules.IsAvailable(cached.FetchedAt, cached.ProviderAvailable, now);
        var menu = available
            ? (JsonSerializer.Deserialize<List<MenuEntry>>(cached.MenuJson) ?? []).Select(m => new MenuItemResponse(m.Name, m.Price, m.Available)).ToList()
            : [];

        return Results.Ok(new StationCateringResponse(
            available, CateringRules.IsOpenNow(now, cached.Opens, cached.Closes), cached.Hotline, new OpeningHoursResponse(cached.Opens, cached.Closes), menu));
    }
}
