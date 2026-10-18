using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartRail.Identity;
using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Features.JourneyViews;
using SmartRail.LiveOperations.Persistence;

namespace SmartRail.LiveOperations.Features.Decisions;

// What goes into the audit trail for one decision: the action, the reason CODE (never the note) and a sentence made of ids and numbers.
internal sealed record DecisionAudit(string Action, string? Reason, string Summary);

// The part every controller decision shares (R-18, US-O4, US-O10): find the journey, let the Domain check the version and the
// state and make the change, save (the outbox rows go in the same save), write the audit entry, and answer with the new journey.
// Each decision's own slice only says what the Domain should do.
internal sealed class DecisionHandler(
    LiveOperationsDbContext db, JourneyDetailReader reader, IStaffDirectory staff, IAuditLog auditLog, TimeProvider clock,
    ILogger<DecisionHandler> log)
{
    // `apply` calls the Domain method (it gets the journey, the actor's account id and the time). `refused` is the 409 text for a
    // decision the journey's state does not allow. `audit` is asked only after the decision was made, so it can describe what happened.
    // `precheck` may refuse before the Domain is asked, but only when the version is current (a stale decision is always a 409 first).
    public async Task<IResult> RunAsync(
        HttpContext http, string journeyId, int? expectedVersion, Func<DecisionAudit> audit, string refused,
        Func<OperationalJourney, Guid, DateTime, DecisionResult> apply, CancellationToken ct, Func<OperationalJourney, Task<IResult?>>? precheck = null,
        bool unknownStopIsNotFound = false)
    {
        if (expectedVersion is null or < 0)
        {
            return Problems.Validation(http, new() { ["expectedVersion"] = ["Send the journey's version (expectedVersion) that you were looking at."] });
        }

        if (!Guid.TryParse(http.User.FindFirstValue("sub"), out var actor))
        {
            return Problems.OutOfScope(http);   // a staff token always has `sub`
        }

        try
        {
            var outcome = await OptimisticRetry.RunAsync(db, async () =>
            {
                var journey = Guid.TryParse(journeyId, out var id)
                    ? await db.Journeys.Include(j => j.Stops).SingleOrDefaultAsync(j => j.Id == id, ct)
                    : null;
                if (journey is null)
                {
                    return new Outcome(Problems.NotFound(http), null);
                }

                var now = clock.GetUtcNow().UtcDateTime;
                if (journey.ControlVersion == expectedVersion && precheck is not null && await precheck(journey) is { } refusal)
                {
                    return new Outcome(refusal, null);
                }

                switch (apply(journey, actor, now))
                {
                    case DecisionResult.StaleVersion:
                        return new Outcome(await VersionConflictAsync(http, journey, now, ct), null);
                    case DecisionResult.UnknownStop:
                        return new Outcome(
                            unknownStopIsNotFound
                                ? Problems.NotFound(http)
                                : Problems.Validation(http, new() { ["stationId"] = ["The station is not a stop of this journey."] }),
                            null);
                    case DecisionResult.PlatformUnavailable:
                        return new Outcome(Problems.RuleViolation(http, "The station has no such platform in service."), null);
                    case DecisionResult.SamePlatform:
                        return new Outcome(Problems.RuleViolation(http, "The stop is already at that platform."), null);
                    case DecisionResult.InvalidState:
                        return new Outcome(Problems.InvalidState(http, refused), null);
                }

                await db.SaveChangesAsync(ct);   // the journey and its outbox events, in one transaction
                return new Outcome(null, journey);
            });

            if (outcome.Problem is not null)
            {
                return outcome.Problem;
            }

            await RecordAuditAsync(http, actor, outcome.Journey!, audit(), ct);
            return Results.Ok(await reader.BuildAsync(outcome.Journey!, ct));
        }
        catch (Exception ex) when (OptimisticRetry.IsCollision(ex))
        {
            return Problems.InvalidState(http, "The journey was changed at the same moment. Look at it again and retry.");
        }
    }

    // Written right after the journey is saved, in a separate save: a crash between the two can lose this one audit entry
    // (accepted risk, agreed 18 Oct). A failure here is logged with ids only and does not undo or hide the decision that was made.
    private async Task RecordAuditAsync(HttpContext http, Guid actor, OperationalJourney journey, DecisionAudit audit, CancellationToken ct)
    {
        try
        {
            await auditLog.AppendAsync(
                new AuditRecord(
                    clock.GetUtcNow().UtcDateTime, actor, http.User.FindFirstValue("role") ?? "Controller", audit.Action,
                    "Journey", journey.Id, audit.Reason, audit.Summary, http.TraceIdentifier),
                ct);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Audit entry {Action} for journey {JourneyId} by account {ActorId} could not be written.", audit.Action, journey.Id, actor);
        }
    }

    // 409 VERSION_CONFLICT (US-O4 AC6): who got there first and how long ago, and the journey as it is now. The name is read for this
    // answer only; it is not logged.
    private async Task<IResult> VersionConflictAsync(HttpContext http, OperationalJourney journey, DateTime now, CancellationToken ct)
    {
        var name = journey.LastDecisionBy is { } by ? await staff.GetDisplayNameAsync(by, ct) : null;
        var age = journey.LastDecisionAt is { } at ? Age(now - at) : "a moment";
        return Problems.VersionConflict(
            http, $"Changed by {name ?? "someone else"} {age} ago", journey.ControlVersion, await reader.BuildAsync(journey, ct));
    }

    private static string Age(TimeSpan span) => span.TotalSeconds < 60 ? $"{Math.Max(0, (int)span.TotalSeconds)} s" : $"{(int)span.TotalMinutes} min";

    private sealed record Outcome(IResult? Problem, OperationalJourney? Journey);
}
