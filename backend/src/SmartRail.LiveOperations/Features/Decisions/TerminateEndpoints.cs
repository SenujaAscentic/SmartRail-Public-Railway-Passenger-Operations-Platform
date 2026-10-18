using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Features.JourneyViews;

namespace SmartRail.LiveOperations.Features.Decisions;

public sealed record TerminateRequest(Guid? AtStationId, string? ReasonCode, string? Note, int? ExpectedVersion);

// POST /api/v1/ops/journeys/{id}/terminate   (contract 3.3, policy Controller, US-O4, R-14)
internal static class TerminateEndpoints
{
    public static void Map(IEndpointRouteBuilder routes) =>
        routes.MapPost("/ops/journeys/{journeyId}/terminate", TerminateAsync)
            .RequireAuthorization(DelayEndpoints.Controller)
            .WithName("TerminateJourney").WithTags("LiveOperations")
            .WithSummary("The controller ends a running journey early at a station ahead of the train; the stops after it are skipped (R-14).")
            .Produces<JourneyDetailResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);

    private static Task<IResult> TerminateAsync(string journeyId, TerminateRequest? body, HttpContext http, DecisionHandler handler, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (body?.AtStationId is null || body.AtStationId == Guid.Empty)
        {
            errors["atStationId"] = ["Give the station where the journey ends."];
        }

        ReasonCodes.Check(body?.ReasonCode, body?.Note, required: true, errors);
        if (errors.Count > 0)
        {
            return Task.FromResult(Problems.Validation(http, errors));
        }

        var station = body!.AtStationId!.Value;
        var reason = new JourneyReason(body.ReasonCode!, string.IsNullOrWhiteSpace(body.Note) ? null : body.Note.Trim());
        return handler.RunAsync(
            http, journeyId, body.ExpectedVersion,
            () => new DecisionAudit("JourneyTerminatedEarly", reason.Code, $"Journey terminated early at station {station}"),
            "Only a running journey can be terminated, at a station the train has not left.",
            (journey, actor, now) => journey.TerminateByController(body.ExpectedVersion!.Value, station, reason, actor, now), ct);
    }
}
