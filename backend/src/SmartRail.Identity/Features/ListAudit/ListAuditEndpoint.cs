using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SmartRail.Identity.Authorization;
using SmartRail.Identity.Persistence;

namespace SmartRail.Identity.Features.ListAudit;

// Who did it, as the controller reads the audit trail. Names are shown here, to an authorised caller only (the entries themselves keep ids).
public sealed record AuditActorResponse(Guid Id, string StaffNumber, string Name, string Role);

public sealed record AuditEntryResponse(
    Guid Id, DateTime At, AuditActorResponse? Actor, string Action, string TargetType, Guid TargetId, string? Reason, string Summary, string RequestId);

// GET /api/v1/ops/audit?journeyId=&staff=&from=&to=&limit=   (policy Controller, US-O10, domain model O-10)
// Read-only: the audit trail can be read here and appended to by decisions, and in no other way. Newest first.
// `staff` is a staff number ("CTRL-01"): it names an employee, not a person, and keeps the account id out of the URL.
internal static class ListAuditEndpoint
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    public static void Map(IEndpointRouteBuilder routes) =>
        routes.MapGet("/ops/audit", HandleAsync)
            .RequireAuthorization(Policies.Controller)
            .WithName("ListAudit")
            .WithTags("Identity")
            .WithSummary("The audit trail, newest first. Filters: journeyId, staff (staff number), from, to (UTC). Controller only.")
            .Produces<IReadOnlyList<AuditEntryResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

    private static async Task<IResult> HandleAsync(
        HttpContext http, IdentityModuleDbContext db, string? journeyId, string? staff, string? from, string? to, string? limit, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();

        Guid? journey = null;
        if (!string.IsNullOrWhiteSpace(journeyId))
        {
            if (Guid.TryParse(journeyId, out var parsed)) journey = parsed;
            else errors["journeyId"] = ["The journey id is not valid."];
        }

        var fromUtc = ParseTime(nameof(from), from, errors);
        var toUtc = ParseTime(nameof(to), to, errors);

        var max = DefaultLimit;
        if (!string.IsNullOrWhiteSpace(limit)
            && (!int.TryParse(limit.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out max) || max is < 1 or > MaxLimit))
        {
            errors[nameof(limit)] = [$"The limit is a whole number from 1 to {MaxLimit}."];
        }

        if (staff is { Length: > 50 })
        {
            errors[nameof(staff)] = ["The staff number is too long."];
        }

        if (errors.Count > 0)
        {
            return Problems.Validation(http, errors);
        }

        var entries = db.AuditEntries.AsNoTracking().AsQueryable();
        if (journey is { } id)
        {
            entries = entries.Where(e => e.TargetType == "Journey" && e.TargetId == id);
        }

        if (!string.IsNullOrWhiteSpace(staff))
        {
            var number = staff.Trim();
            var accountId = await db.Users.AsNoTracking().Where(u => u.UserName == number).Select(u => (Guid?)u.Id).SingleOrDefaultAsync(ct);
            if (accountId is null)
            {
                return Results.Ok(Array.Empty<AuditEntryResponse>());   // nobody has that number: nothing to show
            }

            entries = entries.Where(e => e.ActorId == accountId);
        }

        if (fromUtc is { } start)
        {
            entries = entries.Where(e => e.At >= start);
        }

        if (toUtc is { } end)
        {
            entries = entries.Where(e => e.At <= end);
        }

        var page = await entries.OrderByDescending(e => e.At).ThenByDescending(e => e.Id).Take(max).ToListAsync(ct);

        var actorIds = page.Select(e => e.ActorId).Distinct().ToList();
        var actors = await db.Users.AsNoTracking().Where(u => actorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new AuditActorResponse(u.Id, u.UserName!, u.Name, u.Role.ToString()), ct);

        return Results.Ok(page.Select(e => new AuditEntryResponse(
            e.Id, e.At, actors.GetValueOrDefault(e.ActorId), e.Action, e.TargetType, e.TargetId, e.Reason, e.Summary, e.RequestId)).ToList());
    }

    private static DateTime? ParseTime(string field, string? text, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        // UTC like everywhere in the API; a time without an offset is taken as UTC.
        if (DateTimeOffset.TryParse(text.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed.UtcDateTime;
        }

        errors[field] = ["Give the time as a UTC date and time, for example 2026-10-14T04:30:00Z."];
        return null;
    }
}
