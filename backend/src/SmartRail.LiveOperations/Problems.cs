using Microsoft.AspNetCore.Http;

namespace SmartRail.LiveOperations;

// The LiveOperations module's errors, in the shape of contract section 6: Problem Details plus a stable "code" and the request id.
internal static class Problems
{
    public static IResult Validation(HttpContext http, Dictionary<string, string[]> errors) =>
        Build(http, StatusCodes.Status400BadRequest, "validation-failed", "The request has errors", "VALIDATION_FAILED", errors);

    public static IResult NotFound(HttpContext http) =>
        Build(http, StatusCodes.Status404NotFound, "not-found", "Not found", "NOT_FOUND");

    // Contract section 6: 403 OUT_OF_SCOPE, a crew member acting on a journey that is not theirs (R-17). The response says nothing
    // about the journey beyond that.
    public static IResult OutOfScope(HttpContext http) =>
        Build(http, StatusCodes.Status403Forbidden, "out-of-scope", "Not allowed", "OUT_OF_SCOPE");

    // 403 FORBIDDEN: the caller is known but the action is refused for a reason that is not about scope (for example R-07: positions
    // are accepted only while the journey is running).
    public static IResult Forbidden(HttpContext http, string detail) =>
        Build(http, StatusCodes.Status403Forbidden, "forbidden", "Not allowed", "FORBIDDEN", detail: detail);

    // 409 INVALID_STATE: the action is not allowed in the journey's current state (already running, already ended, ...).
    public static IResult InvalidState(HttpContext http, string detail) =>
        Build(http, StatusCodes.Status409Conflict, "invalid-state", "The request conflicts with the current state", "INVALID_STATE", detail: detail);

    // 422 RULE_VIOLATION: a business rule refuses the action (for example "Start" more than 60 minutes before departure).
    public static IResult RuleViolation(HttpContext http, string detail) =>
        Build(http, StatusCodes.Status422UnprocessableEntity, "rule-violation", "A rule refuses this action", "RULE_VIOLATION", detail: detail);

    // 409 VERSION_CONFLICT (R-18, contract section 6): the decision was based on an old version. `detail` names who changed the journey
    // (the one place a staff name may appear); the answer carries the current version and the journey as it is now.
    public static IResult VersionConflict(HttpContext http, string detail, int currentVersion, object journey) =>
        Build(http, StatusCodes.Status409Conflict, "version-conflict", "Journey was changed by someone else", "VERSION_CONFLICT", detail: detail,
            more: new Dictionary<string, object?> { ["currentVersion"] = currentVersion, ["journey"] = journey });

    private static IResult Build(
        HttpContext http, int status, string type, string title, string code, Dictionary<string, string[]>? errors = null, string? detail = null,
        Dictionary<string, object?>? more = null)
    {
        var extensions = new Dictionary<string, object?> { ["code"] = code, ["requestId"] = http.TraceIdentifier };
        foreach (var (key, value) in more ?? [])
        {
            extensions[key] = value;
        }

        if (errors is not null)
        {
            extensions["errors"] = errors;
        }

        return Results.Problem(type: "https://smartrail.example/errors/" + type, title: title, detail: detail, statusCode: status, extensions: extensions);
    }
}
