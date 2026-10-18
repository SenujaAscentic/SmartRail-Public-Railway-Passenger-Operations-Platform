using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Features.JourneyViews;

namespace SmartRail.LiveOperations.Features.Decisions;

// POST /api/v1/ops/journeys/{id}/delay   and   DELETE /api/v1/ops/journeys/{id}/delay   (contract 3.5, policy Controller, US-O4)
internal static class DelayEndpoints
{
    // Identity's policy name, as a string: a module may not reference Identity's internals (see Policies.cs there).
    internal const string Controller = "Controller";

    public const int MaxDelayMinutes = 1440;

    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/ops/journeys/{journeyId}/delay", SetAsync)
            .RequireAuthorization(Controller)
            .WithName("SetDelay").WithTags("LiveOperations")
            .WithSummary("The controller sets the delay of a journey, with a reason. It stays until cleared (R-05).")
            .Produces<JourneyDetailResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);

        routes.MapDelete("/ops/journeys/{journeyId}/delay", ClearAsync)
            .RequireAuthorization(Controller)
            .WithName("ClearDelay").WithTags("LiveOperations")
            .WithSummary("The controller removes the delay they set; the delay of the latest stop event applies again.")
            .Produces<JourneyDetailResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static Task<IResult> SetAsync(string journeyId, DelayRequest? body, HttpContext http, DecisionHandler handler, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (body?.Minutes is null or < 0 or > MaxDelayMinutes)
        {
            errors["minutes"] = [$"Give the delay in whole minutes, from 0 to {MaxDelayMinutes}."];
        }

        ReasonCodes.Check(body?.ReasonCode, body?.Note, required: true, errors);
        if (errors.Count > 0)
        {
            return Task.FromResult(Problems.Validation(http, errors));
        }

        var minutes = body!.Minutes!.Value;
        var reason = new JourneyReason(body.ReasonCode!, string.IsNullOrWhiteSpace(body.Note) ? null : body.Note.Trim());
        return handler.RunAsync(
            http, journeyId, body.ExpectedVersion,
            () => new DecisionAudit("DelaySet", reason.Code, $"Delay set to {minutes} min"),
            "A delay can be set only on a journey that has not been completed or cancelled.",
            (journey, actor, now) => journey.SetDelayByController(body.ExpectedVersion!.Value, new Delay(minutes), reason, actor, now), ct);
    }

    private static Task<IResult> ClearAsync(string journeyId, [FromBody] VersionRequest? body, HttpContext http, DecisionHandler handler, CancellationToken ct) =>
        handler.RunAsync(
            http, journeyId, body?.ExpectedVersion,
            () => new DecisionAudit("DelayCleared", null, "Controller delay cleared"),
            "There is no controller delay to clear on this journey.",
            (journey, actor, now) => journey.ClearDelayByController(body!.ExpectedVersion!.Value, actor, now), ct);
}
