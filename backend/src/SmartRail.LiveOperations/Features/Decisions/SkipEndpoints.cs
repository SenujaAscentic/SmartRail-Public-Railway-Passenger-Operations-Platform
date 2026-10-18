using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SmartRail.LiveOperations.Features.JourneyViews;

namespace SmartRail.LiveOperations.Features.Decisions;

// POST /api/v1/ops/journeys/{id}/stops/{stationId}/skip   (contract 3.3, policy Controller, US-O4, R-15, R-21)
internal static class SkipEndpoints
{
    public static void Map(IEndpointRouteBuilder routes) =>
        routes.MapPost("/ops/journeys/{journeyId}/stops/{stationId}/skip", SkipAsync)
            .RequireAuthorization(DelayEndpoints.Controller)
            .WithName("SkipStop").WithTags("LiveOperations")
            .WithSummary("The controller skips a stop the train has not reached; later stops gain its scheduled dwell, never below on time (R-21).")
            .Produces<JourneyDetailResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);

    // The station is in the path, so one that is not a stop of the journey is a 404 (like the platform endpoints).
    private static Task<IResult> SkipAsync(string journeyId, string stationId, VersionRequest? body, HttpContext http, DecisionHandler handler, CancellationToken ct)
    {
        if (!Guid.TryParse(stationId, out var station))
        {
            return Task.FromResult(Problems.NotFound(http));
        }

        return handler.RunAsync(
            http, journeyId, body?.ExpectedVersion,
            () => new DecisionAudit("StopSkipped", null, $"Stop {station} skipped"),
            "Only a stop the train has not reached can be skipped: not the origin, not the terminus, not a stop where it only passes, and not on a closed journey.",
            (journey, actor, now) => journey.SkipStopByController(body!.ExpectedVersion!.Value, station, actor, now), ct,
            unknownStopIsNotFound: true);
    }
}
