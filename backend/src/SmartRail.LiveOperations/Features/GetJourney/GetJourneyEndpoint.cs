using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SmartRail.LiveOperations.Features.JourneyViews;
using SmartRail.LiveOperations.Persistence;

namespace SmartRail.LiveOperations.Features.GetJourney;

// GET /api/v1/journeys/{journeyId}   (contract 3.1, policy Public)
// Stops with scheduled / expected / actual times, today's train set, freshness and the last position. No staff fields (T-07).
internal static class GetJourneyEndpoint
{
    public static void Map(IEndpointRouteBuilder routes) =>
        routes.MapGet("/journeys/{journeyId}", HandleAsync)
            .AllowAnonymous()
            .WithName("GetJourney")
            .WithTags("LiveOperations")
            .Produces<JourneyDetailResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    // The id is read as text so that a malformed id gets the same 404 NOT_FOUND as an unknown one (contract section 6).
    private static async Task<IResult> HandleAsync(
        string journeyId, HttpContext http, LiveOperationsDbContext db, JourneyDetailReader reader, CancellationToken ct)
    {
        var journey = Guid.TryParse(journeyId, out var id)
            ? await db.Journeys.AsNoTracking().Include(j => j.Stops).SingleOrDefaultAsync(j => j.Id == id, ct)
            : null;
        if (journey is null)
        {
            return Problems.NotFound(http);
        }

        return Results.Ok(await reader.BuildAsync(journey, ct));
    }
}
