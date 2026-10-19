using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SmartRail.LiveOperations.Features.StreamEvents;

// GET /api/v1/stream?journeyIds=&stationIds=&lineIds=   (contract section 4, policy Public)
// Public on purpose: passengers and station displays read it without signing in. Default-deny is on for the whole
// application, so it says so explicitly (AllowAnonymous). What it may show is limited by PublicProjection (T-07).
internal static class StreamEndpoint
{
    public static void Map(IEndpointRouteBuilder routes) =>
        routes.MapGet("/stream", HandleAsync)
            .AllowAnonymous()
            .WithName("StreamEvents")
            .WithTags("LiveOperations")
            .WithSummary("Public event stream (Server-Sent Events). Give at least one of journeyIds, stationIds, lineIds (comma-separated GUIDs); send Last-Event-ID to resume.")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .ProducesProblem(StatusCodes.Status400BadRequest);

    private static async Task<IResult> HandleAsync(
        HttpContext http, StreamHandler handler, string? journeyIds, string? stationIds, string? lineIds, CancellationToken ct)
    {
        var (filter, errors) = StreamRequest.Parse(journeyIds, stationIds, lineIds);
        if (filter is null)
        {
            // Problem Details with a stable "code" and field errors, as in contract section 6.
            return Results.Problem(
                type: "https://smartrail.example/errors/validation-failed",
                title: "The request has errors",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "VALIDATION_FAILED",
                    ["errors"] = errors,
                    ["requestId"] = http.TraceIdentifier,
                });
        }

        try
        {
            await handler.RunAsync(http, filter, StreamRequest.ParseLastEventId(http.Request.Headers["Last-Event-ID"]), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The client went away. That is how every stream ends, not an error.
        }

        return Results.Empty;
    }
}
