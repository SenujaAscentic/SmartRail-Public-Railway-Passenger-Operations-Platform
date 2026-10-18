using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Features.Idempotency;
using SmartRail.LiveOperations.Persistence;

namespace SmartRail.LiveOperations.Features.CrewJourney;

// The body of every crew action: the id the tablet generated, so that sending it again is harmless (contract section 1).
public sealed record CrewActionRequest(Guid? ClientActionId);

// The answer to Start and End: the journey, its new status and the time it happened.
public sealed record CrewJourneyResult(Guid JourneyId, string Status, DateTime At);

// POST /api/v1/crew/journeys/{id}/start and .../end   (contract 3.4, policy CrewCab: a driver with a cab session; a phone cannot)
internal static class CrewJourneyEndpoints
{
    // The names of Identity's policies are strings: a module may not reference Identity's code (see Identity/Authorization/Policies.cs).
    private const string CrewCab = "CrewCab";

    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/crew/journeys/{journeyId}/start", (string journeyId, CrewActionRequest? body, HttpContext http, CrewJourneyHandler handler, CancellationToken ct) =>
                handler.StartAsync(http, journeyId, body?.ClientActionId, ct))
            .RequireAuthorization(CrewCab)
            .WithName("StartJourney")
            .WithTags("LiveOperations")
            .WithSummary("The assigned driver starts the journey on the cab tablet, from 60 minutes before the scheduled departure.")
            .Produces<CrewJourneyResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        routes.MapPost("/crew/journeys/{journeyId}/end", (string journeyId, CrewActionRequest? body, HttpContext http, CrewJourneyHandler handler, CancellationToken ct) =>
                handler.EndAsync(http, journeyId, body?.ClientActionId, ct))
            .RequireAuthorization(CrewCab)
            .WithName("EndJourney")
            .WithTags("LiveOperations")
            .WithSummary("The assigned driver ends a running journey.")
            .Produces<CrewJourneyResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}

// Start and End. Same shape for both: find the journey, check it is the caller's (R-17), answer a repeat from the store, apply the
// Domain rule, save the change and the remembered answer together.
internal sealed class CrewJourneyHandler(
    LiveOperationsDbContext db, IdempotencyStore actions, TimeProvider clock, ILogger<CrewJourneyHandler> log)
{
    public Task<IResult> StartAsync(HttpContext http, string journeyId, Guid? clientActionId, CancellationToken ct) =>
        RunAsync(http, journeyId, clientActionId, "Start", ct, (journey, caller, now) => journey.Start(now, caller.DeviceId, caller.CrewSessionId) switch
        {
            StartResult.Started => null,
            StartResult.TooEarly => Problems.RuleViolation(http, "A journey can be started from 60 minutes before its scheduled departure."),
            _ => Problems.InvalidState(http, "The journey has already been started."),
        });

    public Task<IResult> EndAsync(HttpContext http, string journeyId, Guid? clientActionId, CancellationToken ct) =>
        RunAsync(http, journeyId, clientActionId, "End", ct, (journey, _, now) => journey.End(now) switch
        {
            EndResult.Ended => null,
            _ => Problems.InvalidState(http, "Only a running journey can be ended."),
        });

    // `apply` runs the Domain rule: null means it worked, otherwise it is the problem to answer.
    private async Task<IResult> RunAsync(
        HttpContext http, string journeyId, Guid? clientActionId, string kind, CancellationToken ct,
        Func<OperationalJourney, CrewCaller, DateTime, IResult?> apply)
    {
        if (clientActionId is null || clientActionId == Guid.Empty)
        {
            return Problems.Validation(http, new() { ["clientActionId"] = ["Send a clientActionId (a new UUID for each action)."] });
        }

        var caller = CrewCaller.From(http.User);
        if (caller is null)
        {
            return Problems.OutOfScope(http);   // a cab token always has these claims; anything else is not a cab session
        }

        try
        {
            return await OptimisticRetry.RunAsync(db, async () =>
            {
                var journey = Guid.TryParse(journeyId, out var id)
                    ? await db.Journeys.Include(j => j.Stops).SingleOrDefaultAsync(j => j.Id == id, ct)
                    : null;
                if (journey is null)
                {
                    return Problems.NotFound(http);
                }

                // R-17: a driver acts only on a journey assigned to them. The refusal is logged (ids only, US-O1).
                if (journey.DriverId != caller.DriverId)
                {
                    log.LogWarning("Refused {Kind}: driver account {DriverId} is not assigned to journey {JourneyId}.", kind, caller.DriverId, journey.Id);
                    return Problems.OutOfScope(http);
                }

                var previous = await actions.FindAsync(clientActionId.Value, ct);
                if (previous is not null)
                {
                    return previous.JourneyId == journey.Id && previous.Kind == kind
                        ? Results.Content(previous.ResultJson, "application/json")   // a repeat: the original answer, nothing changes
                        : Problems.InvalidState(http, "This clientActionId was already used for a different action.");
                }

                var now = clock.GetUtcNow().UtcDateTime;
                if (apply(journey, caller, now) is { } problem)
                {
                    return problem;
                }

                var result = new CrewJourneyResult(journey.Id, journey.Status.ToString(), now);
                actions.Add(clientActionId.Value, journey.Id, kind, result);
                await db.SaveChangesAsync(ct);
                return Results.Ok(result);
            });
        }
        catch (Exception ex) when (OptimisticRetry.IsCollision(ex))
        {
            // Three collisions in a row: the journey is being changed very busily. The driver can simply press the button again.
            return Problems.InvalidState(http, "The journey was changed at the same moment. Try again.");
        }
    }
}
