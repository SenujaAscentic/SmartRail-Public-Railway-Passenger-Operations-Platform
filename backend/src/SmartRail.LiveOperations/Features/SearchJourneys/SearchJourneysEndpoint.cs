using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Features.JourneyViews;
using SmartRail.LiveOperations.Persistence;
using SmartRail.Network;

namespace SmartRail.LiveOperations.Features.SearchJourneys;

// GET /api/v1/journeys/search?from=&to=&date=&after=&limit=   (contract 3.1, policy Public)
// Direct journeys that STOP at both stations, origin first, running on the date, sorted by departure.
internal static class SearchJourneysEndpoint
{
    private static readonly TimeZoneInfo Colombo = TimeZoneInfo.FindSystemTimeZoneById(LiveOperationsModule.TimeZoneId);

    public static void Map(IEndpointRouteBuilder routes) =>
        routes.MapGet("/journeys/search", HandleAsync)
            .AllowAnonymous()
            .WithName("SearchJourneys")
            .WithTags("LiveOperations")
            .WithSummary("Direct journeys between two stations on a date. Past departures are hidden; cancelled journeys are included with their status.")
            .Produces<List<JourneySummaryResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<IResult> HandleAsync(
        HttpContext http, LiveOperationsDbContext db, INetworkCatalog catalog, TimeProvider clock,
        string? from, string? to, string? date, string? after, string? limit, CancellationToken ct)
    {
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(nowUtc, Colombo));

        var (query, errors) = SearchJourneysRequest.Parse(from, to, date, after, limit, today);
        if (query is null)
        {
            return Problems.Validation(http, errors);
        }

        // Journeys on the date that call (as a Stop, not a Pass) at both stations. The order of the two stations and the time
        // are checked in memory: a day has a dozen journeys, so the simplest thing is a plain query and a plain loop.
        var candidates = await db.Journeys.AsNoTracking().Include(j => j.Stops)
            .Where(j => j.ServiceDate == query.Date
                && j.Stops.Any(s => s.StationId == query.From && s.CallType == CallType.Stop)
                && j.Stops.Any(s => s.StationId == query.To && s.CallType == CallType.Stop))
            .ToListAsync(ct);

        var afterUtc = SearchJourneysRequest.AfterUtc(query, today, nowUtc, Colombo);
        var matches = candidates
            .Select(j => (Journey: j, From: j.Stops.Single(s => s.StationId == query.From), To: j.Stops.Single(s => s.StationId == query.To)))
            .Where(m => m.From.Sequence < m.To.Sequence
                && m.From.ScheduledDeparture is not null
                // Today only: a train that has already left (or gone through) the departure station cannot be caught, whatever
                // its time says. Another date is not judged by "now", so it is listed as the timetable has it.
                && (query.Date != today || m.From.Status is not (StopStatus.Departed or StopStatus.Passed))
                // The EXPECTED departure, so a late train that has not left yet stays in the list.
                && m.From.ExpectedDeparture(m.Journey.CurrentDelay.Value) >= afterUtc)
            .OrderBy(m => m.From.ScheduledDeparture)
            .Take(query.Limit)
            .ToList();

        // One call each for the names and the services; every id the response needs is known now.
        var terminusIds = matches.Select(m => m.Journey.Stops.MaxBy(s => s.Sequence)!.StationId);
        var names = await catalog.GetStationNamesAsync([query.From, query.To, .. terminusIds], ct);
        if (!names.ContainsKey(query.From) || !names.ContainsKey(query.To))
        {
            return Problems.NotFound(http);   // an unknown station
        }

        var services = await catalog.GetServicesAsync(matches.Select(m => m.Journey.ServiceId).Distinct().ToList(), ct);

        return Results.Ok(matches.Select(m =>
        {
            var service = services[m.Journey.ServiceId];
            var terminus = names[m.Journey.Stops.MaxBy(s => s.Sequence)!.StationId];
            return JourneyViewBuilder.Summary(m.Journey, m.From, m.To, names, service, JourneyViewBuilder.Name(terminus, service.TrainType));
        }).ToList());
    }
}
