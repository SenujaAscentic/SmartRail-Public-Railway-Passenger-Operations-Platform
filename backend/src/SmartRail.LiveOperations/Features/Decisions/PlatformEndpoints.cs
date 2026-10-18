using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Features.JourneyViews;
using SmartRail.LiveOperations.Persistence;
using SmartRail.Network;

namespace SmartRail.LiveOperations.Features.Decisions;

public sealed record PlatformPreviewRequest(string? Platform);

// What the controller sees before confirming (C-5): the notice the passengers would get, whether a hold is recommended, and the conflicts.
public sealed record PlatformPreviewResponse(int LeadTimeMinutes, string LeadTimeLevel, bool HoldRecommended, IReadOnlyList<PlatformConflictResponse> Conflicts);

// SetHold: also hold the train (then HoldMinutes is required). ConflictsAcknowledged: the controller has seen the conflicts and goes on.
public sealed record PlatformChangeRequest(string? Platform, bool? SetHold, int? HoldMinutes, bool? ConflictsAcknowledged, int? ExpectedVersion);

// POST /api/v1/ops/journeys/{id}/stops/{stationId}/platform/preview   and   .../platform   (contract 3.5, policy Controller, US-O5, R-11 to R-13)
internal static class PlatformEndpoints
{
    public const int MaxPlatformLength = 10;

    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/ops/journeys/{journeyId}/stops/{stationId}/platform/preview", PreviewAsync)
            .RequireAuthorization(DelayEndpoints.Controller)
            .WithName("PreviewPlatformChange").WithTags("LiveOperations")
            .WithSummary("What a platform change would do (lead time, hold recommended, conflicts) without changing anything.")
            .Produces<PlatformPreviewResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        routes.MapPost("/ops/journeys/{journeyId}/stops/{stationId}/platform", ChangeAsync)
            .RequireAuthorization(DelayEndpoints.Controller)
            .WithName("ChangePlatform").WithTags("LiveOperations")
            .WithSummary("The controller moves a stop to another platform. Under 5 minutes of notice is a warning, not a block; a hold is optional.")
            .Produces<JourneyDetailResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static async Task<IResult> PreviewAsync(
        string journeyId, string stationId, PlatformPreviewRequest? body, HttpContext http,
        LiveOperationsDbContext db, INetworkCatalog catalog, PlatformConflictFinder conflicts, TimeProvider clock, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        CheckPlatform(body?.Platform, errors);
        if (errors.Count > 0)
        {
            return Problems.Validation(http, errors);
        }

        var journey = Guid.TryParse(journeyId, out var id) ? await db.Journeys.AsNoTracking().Include(j => j.Stops).SingleOrDefaultAsync(j => j.Id == id, ct) : null;
        if (journey is null || !Guid.TryParse(stationId, out var station) || journey.Stops.All(s => s.StationId != station))
        {
            return Problems.NotFound(http);
        }

        var platform = body!.Platform!.Trim();
        if (!await IsInService(catalog, station, platform, ct))
        {
            return Problems.RuleViolation(http, "The station has no such platform in service.");
        }

        var leadTime = journey.LeadTimeAt(station, clock.GetUtcNow().UtcDateTime)!;
        return Results.Ok(new PlatformPreviewResponse(
            leadTime.Minutes, leadTime.Level.ToString(), leadTime.HoldRecommended, await conflicts.FindAsync(journey, station, platform, ct)));
    }

    private static async Task<IResult> ChangeAsync(
        string journeyId, string stationId, PlatformChangeRequest? body, HttpContext http,
        LiveOperationsDbContext db, INetworkCatalog catalog, PlatformConflictFinder conflicts, DecisionHandler handler, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        CheckPlatform(body?.Platform, errors);
        if (body?.SetHold == true && body.HoldMinutes is null or < 1 or > HoldEndpoints.MaxHoldMinutes)
        {
            errors["holdMinutes"] = [$"To hold the train, give the planned length of the hold in whole minutes, from 1 to {HoldEndpoints.MaxHoldMinutes}."];
        }

        if (errors.Count > 0)
        {
            return Problems.Validation(http, errors);
        }

        if (!Guid.TryParse(stationId, out var station))
        {
            return Problems.NotFound(http);
        }

        var platform = body!.Platform!.Trim();
        var inService = (await catalog.GetPlatformsAsync(station, ct)).Where(p => p.InService).Select(p => p.Number).ToList();
        int? holdMinutes = body.SetHold == true ? body.HoldMinutes : null;
        PlatformChangeOutcome? outcome = null;

        return await handler.RunAsync(
            http, journeyId, body.ExpectedVersion,
            () => new DecisionAudit(
                "PlatformChanged", null,
                $"Platform at station {station} changed to {platform}; notice {outcome!.LeadTime!.Minutes} min ({outcome.LeadTime.Level}); hold {(outcome.HoldSet ? "set" : "not set")}"),
            "A platform can be changed only for a stop the train has not left, on a journey that is not completed or cancelled (and a hold only if none is set).",
            (journey, actor, now) =>
            {
                outcome = journey.ChangePlatformByController(body.ExpectedVersion!.Value, station, platform, inService, holdMinutes, actor, now);
                return outcome.Result;
            },
            ct,
            // R-12: a conflict must be acknowledged. Checked only for a current version, and only for a stop that is in the journey.
            precheck: journey => ConflictRefusalAsync(http, journey, station, platform, body.ConflictsAcknowledged == true, conflicts, ct));
    }

    private static async Task<IResult?> ConflictRefusalAsync(
        HttpContext http, OperationalJourney journey, Guid station, string platform, bool acknowledged, PlatformConflictFinder finder, CancellationToken ct)
    {
        if (journey.Stops.All(s => s.StationId != station))
        {
            return Problems.NotFound(http);   // the station is in the path, so a station that is not a stop of this journey is a 404
        }

        if (acknowledged)
        {
            return null;
        }

        var found = await finder.FindAsync(journey, station, platform, ct);
        return found.Count > 0 ? Problems.ConflictNotAcknowledged(http, found) : null;
    }

    private static async Task<bool> IsInService(INetworkCatalog catalog, Guid station, string platform, CancellationToken ct) =>
        (await catalog.GetPlatformsAsync(station, ct)).Any(p => p.InService && p.Number == platform);

    private static void CheckPlatform(string? platform, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(platform) || platform.Trim().Length > MaxPlatformLength)
        {
            errors["platform"] = [$"Give the platform number (at most {MaxPlatformLength} characters)."];
        }
    }
}
