using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SmartRail.Network.Persistence;

namespace SmartRail.Network.Features.GetStation;

// A station with its platforms and whether it is a catering station (US-P1, P-8).
public sealed record PlatformResponse(string Number, bool InService);

public sealed record StationResponse(
    Guid StationId, string Code, string Name, double Latitude, double Longitude, bool CateringStation,
    IReadOnlyList<PlatformResponse> Platforms);

// GET /api/v1/stations/{stationId}   (contract 3.1, policy Public)
internal static class GetStationEndpoint
{
    public static void Map(IEndpointRouteBuilder routes) =>
        routes.MapGet("/stations/{stationId}", HandleAsync)
            .AllowAnonymous()
            .WithName("GetStation")
            .WithTags("Network")
            .Produces<StationResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    // The id is read as text so that a malformed id gets the same 404 NOT_FOUND as an unknown one (contract section 6).
    private static async Task<IResult> HandleAsync(string stationId, NetworkDbContext db, HttpContext http, CancellationToken ct)
    {
        var station = Guid.TryParse(stationId, out var id)
            ? await db.Stations.AsNoTracking().Include(s => s.Platforms).SingleOrDefaultAsync(s => s.Id == id, ct)
            : null;

        if (station is null)
        {
            return Results.Problem(
                type: "https://smartrail.example/errors/not-found",
                title: "Not found",
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?> { ["code"] = "NOT_FOUND", ["requestId"] = http.TraceIdentifier });
        }

        // "10" must come after "9", and "1A" after "1": shorter numbers first, then alphabetical.
        var platforms = station.Platforms
            .OrderBy(p => p.Number.Length).ThenBy(p => p.Number, StringComparer.Ordinal)
            .Select(p => new PlatformResponse(p.Number, p.InService))
            .ToList();

        return Results.Ok(new StationResponse(
            station.Id, station.Code, station.Name, station.Latitude, station.Longitude, station.IsCateringStation, platforms));
    }
}
