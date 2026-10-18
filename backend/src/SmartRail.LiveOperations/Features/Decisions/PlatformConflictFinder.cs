using Microsoft.EntityFrameworkCore;
using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Persistence;
using SmartRail.Network;

namespace SmartRail.LiveOperations.Features.Decisions;

// One train expected at the platform around the same time (R-12). The apps word it ("Platform 5 is expected to be occupied by 1520 at
// 18:04"); the times are UTC.
public sealed record PlatformConflictResponse(Guid JourneyId, string ServiceNumber, string Platform, DateTime? ExpectedArrival, DateTime? ExpectedDeparture);

// R-12: finds the other journeys that are expected at the same station and platform within 5 minutes of this train. The overlap rule
// itself is PlatformRules.Conflicts (pure, unit-tested); this class only gathers the candidates. A day either side of the service date
// is enough to catch a train that runs past midnight; there are a dozen journeys a day, so the comparison is done in memory.
internal sealed class PlatformConflictFinder(LiveOperationsDbContext db, INetworkCatalog catalog)
{
    public async Task<IReadOnlyList<PlatformConflictResponse>> FindAsync(OperationalJourney journey, Guid stationId, string platform, CancellationToken ct)
    {
        var (start, end) = Window(journey, stationId);
        if (start is null)
        {
            return [];
        }

        var from = journey.ServiceDate.AddDays(-1);
        var to = journey.ServiceDate.AddDays(1);
        var others = await db.Journeys.AsNoTracking().Include(j => j.Stops)
            .Where(j => j.Id != journey.Id && j.ServiceDate >= from && j.ServiceDate <= to
                && (j.Status == JourneyStatus.Planned || j.Status == JourneyStatus.Running))
            .ToListAsync(ct);

        var found = new List<(OperationalJourney Journey, DateTime? Arrival, DateTime? Departure)>();
        foreach (var other in others)
        {
            var stop = other.Stops.FirstOrDefault(s => s.StationId == stationId && s.Platform == platform && s.Status is StopStatus.Scheduled or StopStatus.Arrived);
            if (stop is null)
            {
                continue;
            }

            var delay = other.CurrentDelay.Value;
            var (otherStart, otherEnd) = (stop.ExpectedArrival(delay) ?? stop.ExpectedDeparture(delay), stop.ExpectedDeparture(delay));
            if (otherStart is { } s && PlatformRules.Conflicts(start.Value, end, s, otherEnd))
            {
                found.Add((other, stop.ExpectedArrival(delay), otherEnd));
            }
        }

        var services = await catalog.GetServicesAsync(found.Select(f => f.Journey.ServiceId).Distinct().ToList(), ct);
        return found
            .OrderBy(f => f.Arrival ?? f.Departure)
            .Select(f => new PlatformConflictResponse(f.Journey.Id, services[f.Journey.ServiceId].Number, platform, f.Arrival, f.Departure))
            .ToList();
    }

    // When this train occupies the platform: from its expected arrival (or departure at the origin) to its expected departure.
    private static (DateTime? Start, DateTime? End) Window(OperationalJourney journey, Guid stationId)
    {
        var stop = journey.Stops.FirstOrDefault(s => s.StationId == stationId);
        var delay = journey.CurrentDelay.Value;
        return stop is null
            ? (null, null)
            : (stop.ExpectedArrival(delay) ?? stop.ExpectedDeparture(delay), stop.ExpectedDeparture(delay));
    }
}
