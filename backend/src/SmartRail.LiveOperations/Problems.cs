using Microsoft.AspNetCore.Http;

namespace SmartRail.LiveOperations;

// The LiveOperations module's errors, in the shape of contract section 6: Problem Details plus a stable "code" and the request id.
internal static class Problems
{
    public static IResult Validation(HttpContext http, Dictionary<string, string[]> errors) =>
        Build(http, StatusCodes.Status400BadRequest, "validation-failed", "The request has errors", "VALIDATION_FAILED", errors);

    public static IResult NotFound(HttpContext http) =>
        Build(http, StatusCodes.Status404NotFound, "not-found", "Not found", "NOT_FOUND");

    private static IResult Build(HttpContext http, int status, string type, string title, string code, Dictionary<string, string[]>? errors = null)
    {
        var extensions = new Dictionary<string, object?> { ["code"] = code, ["requestId"] = http.TraceIdentifier };
        if (errors is not null)
        {
            extensions["errors"] = errors;
        }

        return Results.Problem(type: "https://smartrail.example/errors/" + type, title: title, statusCode: status, extensions: extensions);
    }
}
