using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Features.JourneyViews;
using SmartRail.LiveOperations.Persistence;
using SmartRail.Network;

namespace SmartRail.LiveOperations.Features.StationBoard;

// One line of a station board (US-D1). `Time` is the scheduled time at THIS station (departure on a departures board, arrival on an
// arrivals board) and `ExpectedTime` what the system expects now (the actual time once it has happened). `CallType` is Stop or Pass at
// this station, so a display can say "does not stop here". `Hold` is true while the train is held at this station.
public sealed record BoardRowResponse(
    Guid JourneyId,
    string ServiceNumber,
    string TrainType,
    JourneyStationResponse Origin,
    JourneyStationResponse Destination,
    DateOnly ServiceDate,
    DateTime Time,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTime? ExpectedTime,
    string Platform,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? PreviousPlatform,
    string CallType,
    string StopStatus,
    JourneyStatusResponse Status,
    bool Hold,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] JourneyReasonResponse? Reason);

// Which board: trains leaving this station or trains arriving at it.
public enum BoardKind { Departures, Arrivals }

// GET /api/v1/stations/{stationId}/board?kind=departures|arrivals&limit=10   (contract 3.1, policy Public, US-D1)
// Journeys of today's service day only; each row carries its serviceDate.
internal static class StationBoardEndpoint
{
    public const int DefaultLimit = 10;
    public const int MaxLimit = 50;

    public static void Map(IEndpointRouteBuilder routes) =>
        routes.MapGet("/stations/{stationId}/board", HandleAsync)
            .AllowAnonymous()
            .WithName("GetStationBoard").WithTags("LiveOperations")
            .WithSummary("The next departures (or arrivals) at a station for the display pages. No staff fields (T-07).")
            .Produces<IReadOnlyList<BoardRowResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<IResult> HandleAsync(
        string stationId, string? kind, string? limit, HttpContext http,
        LiveOperationsDbContext db, INetworkCatalog catalog, TimeProvider clock, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        var boardKind = BoardKind.Departures;
        if (!string.IsNullOrWhiteSpace(kind) && !Enum.TryParse(kind.Trim(), ignoreCase: true, out boardKind))
        {
            errors["kind"] = ["Use kind=departures or kind=arrivals."];
        }

        var max = DefaultLimit;
        if (!string.IsNullOrWhiteSpace(limit)
            && (!int.TryParse(limit.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out max) || max is < 1 or > MaxLimit))
        {
            errors["limit"] = [$"The limit is a whole number from 1 to {MaxLimit}."];
        }

        if (errors.Count > 0)
        {
            return Problems.Validation(http, errors);
        }

        // An id that is not a GUID is the same 404 as an unknown station (contract section 6).
        if (!Guid.TryParse(stationId, out var station) || !(await catalog.GetStationNamesAsync([station], ct)).ContainsKey(station))
        {
            return Problems.NotFound(http);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(LiveOperationsModule.TimeZoneId)).DateTime);

        // Only today's service day (Colombo date from the server clock): a board that also showed tomorrow's trains would repeat the
        // same times with no date. When none are left the list is empty and the display says "No more departures today".
        var journeys = await db.Journeys.AsNoTracking().Include(j => j.Stops)
            .Where(j => j.ServiceDate == today && j.Stops.Any(s => s.StationId == station))
            .ToListAsync(ct);

        var candidates = StationBoardBuilder.Candidates(journeys, station, boardKind, now, max);
        var stationIds = candidates.SelectMany(c => { var (o, d) = StationBoardBuilder.Ends(c.Journey); return new[] { o, d }; }).Distinct().ToList();
        var names = await catalog.GetStationNamesAsync(stationIds, ct);
        var services = await catalog.GetServicesAsync(candidates.Select(c => c.Journey.ServiceId).Distinct().ToList(), ct);

        return Results.Ok(StationBoardBuilder.Rows(candidates, names, services));
    }
}

// The rules of a board, pure so they are unit-tested (US-D1): which trains are on it, in which order, and what each line says.
internal static class StationBoardBuilder
{
    // How long a train that has left (or arrived) stays on the board (US-D1 AC3).
    public static readonly TimeSpan StaysAfter = TimeSpan.FromMinutes(1);

    // How long a train nobody has started stays past its expected time, shown as "Not started" (S-09). 10 minutes, the same as the Lost
    // threshold (R-06): a passenger waiting for a late, unreported train must not see it vanish after a minute. Decided by Senuja, 19 Oct.
    public static readonly TimeSpan NotStartedStaysFor = TimeSpan.FromMinutes(10);

    internal sealed record Candidate(OperationalJourney Journey, JourneyStop Stop, DateTime Time, DateTime? ExpectedTime);

    public static List<Candidate> Candidates(IEnumerable<OperationalJourney> journeys, Guid station, BoardKind kind, DateTime now, int limit)
    {
        var rows = new List<Candidate>();
        foreach (var journey in journeys)
        {
            var stop = journey.Stops.FirstOrDefault(s => s.StationId == station);
            if (stop is null)
            {
                continue;
            }

            var delay = journey.CurrentDelay.Value;
            // A departures board needs a departure here (not the terminus); an arrivals board an arrival (not the origin).
            var (time, expected, actual) = kind == BoardKind.Departures
                ? (stop.ScheduledDeparture, stop.ExpectedDeparture(delay), stop.ActualDeparture)
                : (stop.ScheduledArrival, stop.ExpectedArrival(delay), stop.ActualArrival);
            if (time is null)
            {
                continue;
            }

            if (!IsShown(journey, stop, time.Value, expected, actual, now))
            {
                continue;
            }

            rows.Add(new Candidate(journey, stop, time.Value, expected));
        }

        return rows.OrderBy(r => r.ExpectedTime ?? r.Time).ThenBy(r => r.Time).Take(limit).ToList();
    }

    // US-D1 AC3: a train that has left (or arrived) disappears one minute later; a cancelled or terminated train, or a skipped stop, stays
    // until its scheduled time has passed (A-35). A journey nobody has started stays for 10 minutes past its expected time, then goes
    // (open point 49).
    private static bool IsShown(OperationalJourney journey, JourneyStop stop, DateTime scheduled, DateTime? expected, DateTime? actual, DateTime now)
    {
        if (actual is { } happened)
        {
            return happened > now - StaysAfter;
        }

        if (journey.Status is JourneyStatus.Cancelled or JourneyStatus.TerminatedEarly || stop.Status == StopStatus.Skipped)
        {
            return scheduled > now;
        }

        if (journey.Status == JourneyStatus.Planned)
        {
            return (expected ?? scheduled) + NotStartedStaysFor > now;
        }

        return true;   // running (or completed with this stop never happening): shown with whatever the system knows
    }

    // Where the train starts and where it is going: the last stop it will still call at. After an early termination that is the
    // termination station, not the nominal terminus (a cancelled train, with every stop skipped, keeps its nominal ends).
    public static (Guid Origin, Guid Destination) Ends(OperationalJourney journey)
    {
        var stops = journey.Stops.OrderBy(s => s.Sequence).ToList();
        var ahead = stops.Where(s => s.Status != StopStatus.Skipped).ToList();
        return (stops[0].StationId, (ahead.Count > 0 ? ahead[^1] : stops[^1]).StationId);
    }

    public static List<BoardRowResponse> Rows(
        IReadOnlyList<Candidate> candidates, IReadOnlyDictionary<Guid, string> names, IReadOnlyDictionary<Guid, ServiceDetails> services) =>
        candidates.Select(c =>
        {
            var (origin, destination) = Ends(c.Journey);
            var hold = c.Journey.Hold is { } h && h.StationId == c.Stop.StationId;
            return new BoardRowResponse(
                c.Journey.Id, services[c.Journey.ServiceId].Number, services[c.Journey.ServiceId].TrainType,
                new JourneyStationResponse(origin, names[origin]),
                new JourneyStationResponse(destination, names[destination]),
                c.Journey.ServiceDate, c.Time, c.ExpectedTime, c.Stop.Platform, c.Stop.PreviousPlatform, c.Stop.CallType.ToString(), c.Stop.Status.ToString(),
                JourneyViewBuilder.Status(c.Journey), hold,
                c.Journey.Reason is { } r ? new JourneyReasonResponse(r.Code, r.Note) : null);
        }).ToList();
}
