using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SmartRail.LiveOperations.Stream;

namespace SmartRail.LiveOperations.Features.StreamEvents;

// GET /api/v1/ops/stream?journeyIds=&stationIds=&lineIds=   (contract section 4, policy Staff: Controller and StationManager)
// The same events as the public stream, plus `source` on each (who supplied the value: US-O3 star criterion). Not yet: causedBy, alerts.
// A station manager sees only their own station (R-17): whatever they ask for, the filter is their station. The stream ENDS when the
// access token expires, so a session that ended stops receiving; the client refreshes its token and reconnects with Last-Event-ID.
internal static class OpsStreamEndpoint
{
    // Identity's policy name, as a string (a module may not reference Identity's internals).
    private const string Staff = "Staff";

    public static void Map(IEndpointRouteBuilder routes) =>
        routes.MapGet("/ops/stream", HandleAsync)
            .RequireAuthorization(Staff)
            .WithName("OpsStreamEvents")
            .WithTags("LiveOperations")
            .WithSummary("Staff event stream (Server-Sent Events): the public events plus `source`. Ends when the access token expires. A station manager sees only their own station.")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .ProducesProblem(StatusCodes.Status400BadRequest);

    private static async Task<IResult> HandleAsync(
        HttpContext http, StreamHandler handler, string? journeyIds, string? stationIds, string? lineIds, CancellationToken ct)
    {
        StreamFilter? filter;
        if (http.User.FindFirstValue("role") == "StationManager")
        {
            if (!Guid.TryParse(http.User.FindFirstValue("stationId"), out var station))
            {
                return Problems.OutOfScope(http);   // a station manager's token always names the station
            }

            filter = new StreamFilter(new HashSet<Guid>(), new HashSet<Guid> { station }, new HashSet<Guid>());
        }
        else
        {
            var (parsed, errors) = StreamRequest.Parse(journeyIds, stationIds, lineIds);
            if (parsed is null)
            {
                return Problems.Validation(http, errors);
            }

            filter = parsed;
        }

        DateTimeOffset? endsAt = long.TryParse(http.User.FindFirstValue("exp"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var exp)
            ? DateTimeOffset.FromUnixTimeSeconds(exp)
            : null;

        try
        {
            await handler.RunAsync(http, filter, StreamRequest.ParseLastEventId(http.Request.Headers["Last-Event-ID"]), ct, staff: true, endsAt);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The client went away: how every stream ends.
        }

        return Results.Empty;
    }
}
