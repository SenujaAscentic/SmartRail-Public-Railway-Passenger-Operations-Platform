using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SmartRail.LiveOperations.Features.JourneyViews;
using SmartRail.LiveOperations.Persistence;
using SmartRail.Network;

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
        string journeyId, HttpContext http, LiveOperationsDbContext db, INetworkCatalog catalog, CancellationToken ct)
    {
        var journey = Guid.TryParse(journeyId, out var id)
            ? await db.Journeys.AsNoTracking().Include(j => j.Stops).SingleOrDefaultAsync(j => j.Id == id, ct)
            : null;
        if (journey is null)
        {
            return Problems.NotFound(http);
        }

        var names = await catalog.GetStationNamesAsync(journey.Stops.Select(s => s.StationId).ToList(), ct);
        var service = (await catalog.GetServicesAsync([journey.ServiceId], ct)).GetValueOrDefault(journey.ServiceId);
        if (service is null || journey.Stops.Any(s => !names.ContainsKey(s.StationId)))
        {
            // The journey points at a service or station that Network no longer has: a data fault, not something to guess around.
            throw new InvalidOperationException($"Journey {journey.Id} refers to a service or station that is not in the network.");
        }

        var trainSet = await catalog.GetTrainSetAsync(journey.TrainSetId, ct);
        return Results.Ok(JourneyViewBuilder.Detail(journey, names, service, trainSet));
    }
}
