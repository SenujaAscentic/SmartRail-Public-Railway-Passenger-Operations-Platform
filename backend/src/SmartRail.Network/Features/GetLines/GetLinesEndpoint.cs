using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SmartRail.Network.Persistence;

namespace SmartRail.Network.Features.GetLines;

// The lines with their routes, and each route's stations in order with the distance from its start (line view, line diagram).
public sealed record RouteStationResponse(Guid StationId, string Code, string Name, decimal DistanceKm, double Latitude, double Longitude);

public sealed record RouteResponse(Guid RouteId, string Code, string Direction, string Name, IReadOnlyList<RouteStationResponse> Stations);

public sealed record LineResponse(Guid LineId, string Code, string Name, IReadOnlyList<RouteResponse> Routes);

// GET /api/v1/lines   (contract 3.1, policy Public)
internal static class GetLinesEndpoint
{
    public static void Map(IEndpointRouteBuilder routes) =>
        routes.MapGet("/lines", HandleAsync)
            .AllowAnonymous()
            .WithName("GetLines")
            .WithTags("Network")
            .Produces<List<LineResponse>>();

    // The whole network is one line with two routes and about a dozen stations, so three plain queries are the simplest thing.
    private static async Task<IResult> HandleAsync(NetworkDbContext db, CancellationToken ct)
    {
        var stations = await db.Stations.AsNoTracking().ToDictionaryAsync(s => s.Id, ct);
        var lines = await db.Lines.AsNoTracking()
            .Include(l => l.Routes).ThenInclude(r => r.Stations)
            .OrderBy(l => l.Code)
            .ToListAsync(ct);

        var response = lines.Select(line => new LineResponse(
            line.Id, line.Code, line.Name,
            line.Routes.OrderBy(r => r.Code).Select(route => new RouteResponse(
                route.Id, route.Code, route.Direction.ToString(), route.Name,
                route.Stations.OrderBy(rs => rs.Sequence).Select(rs =>
                {
                    var station = stations[rs.StationId];
                    return new RouteStationResponse(station.Id, station.Code, station.Name, rs.DistanceKm, station.Latitude, station.Longitude);
                }).ToList())).ToList())).ToList();

        return Results.Ok(response);
    }
}
