using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Features.CrewJourney;
using SmartRail.LiveOperations.Features.Idempotency;
using SmartRail.LiveOperations.Persistence;
using SmartRail.Network;

namespace SmartRail.LiveOperations.Features.CrewPositions;

// POST /api/v1/crew/journeys/{id}/positions   (contract 3.4, policy CrewCab, US-B3)
internal static class CrewPositionsEndpoint
{
    private const string CrewCab = "CrewCab";   // Identity's policy name (a module may not reference Identity's code)

    public static void Map(IEndpointRouteBuilder routes) =>
        routes.MapPost("/crew/journeys/{journeyId}/positions", (string journeyId, PositionBatchRequest? body, HttpContext http, CrewPositionsHandler handler, CancellationToken ct) =>
                handler.HandleAsync(http, journeyId, body, ct))
            .RequireAuthorization(CrewCab)
            .WithName("PostPositions")
            .WithTags("LiveOperations")
            .WithSummary("A batch of GPS fixes from the cab tablet of the assigned driver, for a running journey. Fixes that fail the checks (R-08) are listed, not stored.")
            .Produces<PositionBatchResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
}

// The R-07 checks, then the Domain judges each fix (R-08, R-09) and detects stops (R-04); the slice only measures (distance from the
// route, distances to the stations) and hands the numbers in, as the Domain asks.
internal sealed class CrewPositionsHandler(
    LiveOperationsDbContext db, INetworkCatalog network, IdempotencyStore actions, TimeProvider clock, ILogger<CrewPositionsHandler> log)
{
    public async Task<IResult> HandleAsync(HttpContext http, string journeyId, PositionBatchRequest? request, CancellationToken ct)
    {
        var (positions, errors) = PositionBatchValidator.Validate(request);
        if (positions is null)
        {
            return Problems.Validation(http, errors);
        }

        // The policy has already checked the token, the device and that the cab session is still active (Identity, on every request).
        var caller = CrewCaller.From(http.User);
        if (caller is null)
        {
            return Problems.OutOfScope(http);
        }

        var clientActionId = request!.ClientActionId!.Value;
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

                // R-07: only the journey's assigned driver. The refusal is logged with ids only (US-O1).
                if (journey.DriverId != caller.DriverId)
                {
                    log.LogWarning("Refused positions: driver account {DriverId} is not assigned to journey {JourneyId}.", caller.DriverId, journey.Id);
                    return Problems.OutOfScope(http);
                }

                // A repeated batch gets its original answer, even if the journey has meanwhile ended (C-7).
                var previous = await actions.FindAsync(clientActionId, ct);
                if (previous is not null)
                {
                    return previous.JourneyId == journey.Id && previous.Kind == "Positions"
                        ? Results.Content(previous.ResultJson, "application/json")
                        : Problems.InvalidState(http, "This clientActionId was already used for a different action.");
                }

                // R-07: only a Running journey (the spec says 403).
                if (journey.Status != JourneyStatus.Running)
                {
                    log.LogWarning("Refused positions: journey {JourneyId} is {Status}, not Running.", journey.Id, journey.Status);
                    return Problems.Forbidden(http, "Positions are accepted only while the journey is running.");
                }

                var route = new RouteGeometry(await network.GetRouteAsync(journey.ServiceId, ct));
                if (route.IsEmpty)
                {
                    throw new InvalidOperationException($"Journey {journey.Id} has a service with no route in the network.");
                }

                var result = Apply(journey, route, positions, caller);
                actions.Add(clientActionId, journey.Id, "Positions", result);
                await db.SaveChangesAsync(ct);

                if (result.Discarded.Count > 0)
                {
                    // R-08: discarded positions are counted and logged, not stored as live.
                    log.LogInformation("Journey {JourneyId}: {Discarded} of {Total} positions discarded ({Reasons}).",
                        journey.Id, result.Discarded.Count, positions.Count,
                        string.Join(", ", result.Discarded.GroupBy(d => d.Reason).Select(g => $"{g.Key}: {g.Count()}")));
                }

                return Results.Ok(result);
            });
        }
        catch (Exception ex) when (OptimisticRetry.IsCollision(ex))
        {
            return Problems.InvalidState(http, "The journey was changed at the same moment. Send the batch again.");
        }
    }

    // Oldest first: a tablet that was in a tunnel sends its fixes in one go, not necessarily in order, and R-08 throws away a fix that
    // is older than the last accepted one.
    private RouteDistances Measure(GeoPoint location, RouteGeometry route, OperationalJourney journey) =>
        new(route.DistanceFromRouteMetres(location),
            route.DistancesToStations(location, journey.Stops.Where(s => s.Zone != ZonePhase.Finished).Select(s => s.StationId)));

    private PositionBatchResult Apply(OperationalJourney journey, RouteGeometry route, List<ValidPosition> positions, CrewCaller caller)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var accepted = 0;
        var discarded = new List<DiscardedPosition>();
        ValidPosition? latest = null;   // the newest accepted fix: the one the stream announces

        foreach (var position in positions.OrderBy(p => ClockCorrection.EventTime(p.GpsTime, p.DeviceTime, caller.ClockOffset)))
        {
            var measured = Measure(position.Location, route, journey);
            var sample = new PositionSample(
                position.Location, position.SpeedKmh, position.GpsTime, position.DeviceTime, caller.ClockOffset,
                measured.FromRouteMetres, measured.ToStationsMetres);

            var verdict = journey.RecordPosition(sample, now);
            if (verdict == PositionVerdict.Accepted)
            {
                accepted++;
                latest = position;
            }
            else
            {
                discarded.Add(new DiscardedPosition(position.Index, verdict.ToString()));
            }
        }

        // One TrainPositionUpdated per batch that moved the train, not one per fix: a tablet that was in a tunnel sends a long batch.
        if (latest is not null)
        {
            var progress = route.ProgressOf(latest.Location);
            journey.AnnouncePosition(
                ClockCorrection.EventTime(latest.GpsTime, latest.DeviceTime, caller.ClockOffset), latest.Location, latest.SpeedKmh,
                progress.LastStationId, progress.NextStationId, progress.FractionToNext);
        }

        return new PositionBatchResult(accepted, discarded.OrderBy(d => d.Index).ToList());
    }

    private sealed record RouteDistances(double FromRouteMetres, IReadOnlyDictionary<Guid, double> ToStationsMetres);
}
