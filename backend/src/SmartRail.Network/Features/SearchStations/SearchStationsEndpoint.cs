using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SmartRail.Network.Persistence;

namespace SmartRail.Network.Features.SearchStations;

// GET /api/v1/stations?query=kan   (contract 3.1, policy Public, type-ahead, at most 10 rows)
internal static class SearchStationsEndpoint
{
    public static void Map(IEndpointRouteBuilder routes) =>
        routes.MapGet("/stations", HandleAsync)
            .AllowAnonymous()
            .WithName("SearchStations")
            .WithTags("Network")
            .Produces<List<StationSummary>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

    private static async Task<IResult> HandleAsync([Required] string? query, NetworkDbContext db, HttpContext http, CancellationToken ct)
    {
        var problem = SearchStationsValidator.Validate(query);
        if (problem is not null)
        {
            // Problem Details with a stable "code" and field errors, as in contract section 6.
            return Results.Problem(
                type: "https://smartrail.example/errors/validation-failed",
                title: "The request has errors",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "VALIDATION_FAILED",
                    ["errors"] = new Dictionary<string, string[]> { ["query"] = [problem] },
                    ["requestId"] = http.TraceIdentifier,
                });
        }

        return Results.Ok(await SearchStationsHandler.HandleAsync(db, query!, ct));
    }
}
