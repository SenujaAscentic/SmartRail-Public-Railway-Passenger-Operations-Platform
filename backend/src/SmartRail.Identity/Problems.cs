using Microsoft.AspNetCore.Http;

namespace SmartRail.Identity;

// The Identity module's errors, in the shape of contract section 6: Problem Details plus a stable "code" and the request id.
internal static class Problems
{
    public static IResult Validation(HttpContext http, Dictionary<string, string[]> errors) =>
        Build(http, StatusCodes.Status400BadRequest, "validation-failed", "The request has errors", "VALIDATION_FAILED",
            detail: null, errors);

    // Missing, expired or invalid token.
    public static IResult Unauthenticated(HttpContext http) =>
        Build(http, StatusCodes.Status401Unauthorized, "unauthenticated", "Sign in required", "UNAUTHENTICATED",
            detail: "You are not signed in, or your session has ended.");

    // Contract section 6: every sign-in failure gives this one answer, whatever the reason (unknown number, wrong
    // password, deactivated account, wrong kind of account). Nothing here may reveal which one it was.
    public static IResult SignInFailed(HttpContext http) =>
        Build(http, StatusCodes.Status401Unauthorized, "unauthenticated", "Sign-in failed", "UNAUTHENTICATED",
            detail: "The staff number or password is not correct.");

    // Contract section 6: 409 INVALID_STATE, here for two cab sign-ins racing each other.
    public static IResult Conflict(HttpContext http, string detail) =>
        Build(http, StatusCodes.Status409Conflict, "invalid-state", "The request conflicts with the current state", "INVALID_STATE", detail);

    // Signed in, but the role is not allowed to do this.
    public static IResult Forbidden(HttpContext http) =>
        Build(http, StatusCodes.Status403Forbidden, "forbidden", "Not allowed", "FORBIDDEN",
            detail: "Your role is not allowed to do this.");

    public static IResult SignInLocked(HttpContext http, TimeSpan retryAfter)
    {
        http.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        return Build(http, StatusCodes.Status429TooManyRequests, "sign-in-locked", "Sign-in is locked", "SIGN_IN_LOCKED",
            detail: "Too many failed attempts. Try again later.");
    }

    private static IResult Build(HttpContext http, int status, string type, string title, string code, string? detail,
        Dictionary<string, string[]>? errors = null)
    {
        var extensions = new Dictionary<string, object?> { ["code"] = code, ["requestId"] = http.TraceIdentifier };
        if (errors is not null)
        {
            extensions["errors"] = errors;
        }

        return Results.Problem(
            type: "https://smartrail.example/errors/" + type,
            title: title,
            detail: detail,
            statusCode: status,
            extensions: extensions);
    }
}
