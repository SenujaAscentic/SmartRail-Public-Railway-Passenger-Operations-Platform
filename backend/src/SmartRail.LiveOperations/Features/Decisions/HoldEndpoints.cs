using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Features.JourneyViews;

namespace SmartRail.LiveOperations.Features.Decisions;

// The body of a hold (contract 3.5). The reason is optional, like the note, and uses the same codes as a delay.
public sealed record HoldRequest(Guid? StationId, int? Minutes, string? ReasonCode, string? Note, int? ExpectedVersion);

// POST /api/v1/ops/journeys/{id}/hold   and   POST /api/v1/ops/journeys/{id}/hold/release   (contract 3.5, policy Controller, US-O4, R-22)
internal static class HoldEndpoints
{
    public const int MaxHoldMinutes = 240;

    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/ops/journeys/{journeyId}/hold", SetAsync)
            .RequireAuthorization(DelayEndpoints.Controller)
            .WithName("HoldJourney").WithTags("LiveOperations")
            .WithSummary("The controller holds the train at a station. Its expected departure follows the clock until it is released (R-22).")
            .Produces<JourneyDetailResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);

        routes.MapPost("/ops/journeys/{journeyId}/hold/release", ReleaseAsync)
            .RequireAuthorization(DelayEndpoints.Controller)
            .WithName("ReleaseHold").WithTags("LiveOperations")
            .WithSummary("The controller lets a held train go.")
            .Produces<JourneyDetailResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static Task<IResult> SetAsync(string journeyId, HoldRequest? body, HttpContext http, DecisionHandler handler, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (body?.StationId is null || body.StationId == Guid.Empty)
        {
            errors["stationId"] = ["Give the station where the train is held."];
        }

        if (body?.Minutes is null or < 1 or > MaxHoldMinutes)
        {
            errors["minutes"] = [$"Give the planned length of the hold in whole minutes, from 1 to {MaxHoldMinutes}."];
        }

        ReasonCodes.Check(body?.ReasonCode, body?.Note, required: false, errors);
        if (errors.Count > 0)
        {
            return Task.FromResult(Problems.Validation(http, errors));
        }

        var station = body!.StationId!.Value;
        var minutes = body.Minutes!.Value;
        JourneyReason? reason = string.IsNullOrWhiteSpace(body.ReasonCode)
            ? null
            : new JourneyReason(body.ReasonCode, string.IsNullOrWhiteSpace(body.Note) ? null : body.Note.Trim());
        return handler.RunAsync(
            http, journeyId, body.ExpectedVersion,
            () => new DecisionAudit("HoldSet", reason?.Code, $"Held at station {station} for {minutes} min"),
            "A train can be held only at a station it has not left, on a journey that is not completed or cancelled, and only one hold at a time.",
            (journey, actor, now) => journey.SetHoldByController(body.ExpectedVersion!.Value, station, minutes, reason, actor, now), ct);
    }

    private static Task<IResult> ReleaseAsync(string journeyId, VersionRequest? body, HttpContext http, DecisionHandler handler, CancellationToken ct) =>
        handler.RunAsync(
            http, journeyId, body?.ExpectedVersion,
            () => new DecisionAudit("HoldReleased", null, "Hold released"),
            "There is no hold to release on this journey.",
            (journey, actor, now) => journey.ReleaseHoldByController(body!.ExpectedVersion!.Value, actor, now), ct);
}
