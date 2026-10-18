using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Features.JourneyViews;

namespace SmartRail.LiveOperations.Features.Decisions;

// The body of a cancellation (contract 3.5): a reason code is required, the note is optional. (incidentId is not accepted until
// incidents exist: agreed 18 Oct.)
public sealed record CancelRequest(string? ReasonCode, string? Note, int? ExpectedVersion);

// POST /api/v1/ops/journeys/{id}/cancel   and   POST /api/v1/ops/journeys/{id}/reinstate   (contract 3.5, policy Controller, US-O4, R-14)
internal static class CancelEndpoints
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/ops/journeys/{journeyId}/cancel", CancelAsync)
            .RequireAuthorization(DelayEndpoints.Controller)
            .WithName("CancelJourney").WithTags("LiveOperations")
            .WithSummary("The controller cancels a planned or running journey; its remaining stops are skipped (R-14).")
            .Produces<JourneyDetailResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);

        routes.MapPost("/ops/journeys/{journeyId}/reinstate", ReinstateAsync)
            .RequireAuthorization(DelayEndpoints.Controller)
            .WithName("ReinstateJourney").WithTags("LiveOperations")
            .WithSummary("The controller reinstates a cancelled journey, only before its scheduled start (R-14).")
            .Produces<JourneyDetailResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static Task<IResult> CancelAsync(string journeyId, CancelRequest? body, HttpContext http, DecisionHandler handler, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        ReasonCodes.Check(body?.ReasonCode, body?.Note, required: true, errors);
        if (errors.Count > 0)
        {
            return Task.FromResult(Problems.Validation(http, errors));
        }

        var reason = new JourneyReason(body!.ReasonCode!, string.IsNullOrWhiteSpace(body.Note) ? null : body.Note.Trim());
        return handler.RunAsync(
            http, journeyId, body.ExpectedVersion,
            new DecisionAudit("JourneyCancelled", reason.Code, "Journey cancelled"),
            "Only a planned or running journey can be cancelled.",
            (journey, actor, now) => journey.CancelByController(body.ExpectedVersion!.Value, reason, actor, now), ct);
    }

    private static Task<IResult> ReinstateAsync(string journeyId, VersionRequest? body, HttpContext http, DecisionHandler handler, CancellationToken ct) =>
        handler.RunAsync(
            http, journeyId, body?.ExpectedVersion,
            new DecisionAudit("JourneyReinstated", null, "Journey reinstated"),
            "Only a cancelled journey can be reinstated, and only before its scheduled start.",
            (journey, actor, now) => journey.ReinstateByController(body!.ExpectedVersion!.Value, actor, now), ct);
}
