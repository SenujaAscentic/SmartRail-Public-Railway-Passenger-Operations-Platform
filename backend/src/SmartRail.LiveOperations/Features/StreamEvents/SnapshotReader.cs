using Microsoft.EntityFrameworkCore;
using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Persistence;
using SmartRail.LiveOperations.Stream;

namespace SmartRail.LiveOperations.Features.StreamEvents;

// The state of one journey as a client needs it to start (or restart) following the stream: public fields only (T-07),
// so no source, no staff and no crew session. `Sequence` is the stream position the snapshot is current to: the client
// ignores any event with a lower or equal sequence.
public sealed record JourneySnapshot(
    Guid JourneyId,
    DateOnly ServiceDate,
    Guid LineId,
    int DelayMinutes,
    TrackingStatus Tracking,
    DateTime? LastSeenAt,
    SnapshotPosition? LastPosition,
    IReadOnlyList<SnapshotStop> Stops,
    long Sequence);

public sealed record SnapshotPosition(double Latitude, double Longitude);

// Expected times are what R-02 / R-03 give now; once a stop has really happened they are the actual times.
public sealed record SnapshotStop(
    Guid StationId,
    int Sequence,
    StopStatus Status,
    DateTime? ActualArrival,
    DateTime? ActualDeparture,
    DateTime? ExpectedArrival,
    DateTime? ExpectedDeparture);

internal sealed class SnapshotReader(LiveOperationsDbContext db, TimeProvider clock)
{
    private static readonly TimeZoneInfo Colombo = TimeZoneInfo.FindSystemTimeZoneById(LiveOperationsModule.TimeZoneId);

    // The journeys the filter names, plus the journeys of today (and yesterday's, if still running past midnight, E-12) that
    // call at a named station or run on a named line. A journey asked for by id is returned whatever its date.
    public async Task<List<JourneySnapshot>> ReadAsync(StreamFilter filter, long sequence, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Colombo).DateTime);
        var yesterday = today.AddDays(-1);
        var journeyIds = filter.JourneyIds.ToArray();   // arrays, so EF turns them into an SQL "IN" list
        var stationIds = filter.StationIds.ToArray();
        var lineIds = filter.LineIds.ToArray();

        var journeys = await db.Journeys.AsNoTracking().Include(j => j.Stops)
            .Where(j => journeyIds.Contains(j.Id)
                || ((j.ServiceDate == today
                        || (j.ServiceDate == yesterday && j.Stops.Any(s => s.Status == StopStatus.Scheduled || s.Status == StopStatus.Arrived)))
                    && (lineIds.Contains(j.LineId) || j.Stops.Any(s => stationIds.Contains(s.StationId)))))
            .OrderBy(j => j.ServiceDate).ThenBy(j => j.Id)
            .ToListAsync(cancellationToken);

        return journeys.Select(j => ToSnapshot(j, sequence)).ToList();
    }

    internal static JourneySnapshot ToSnapshot(OperationalJourney journey, long sequence)
    {
        var delay = journey.CurrentDelay.Value;
        var position = journey.LastLatitude is { } lat && journey.LastLongitude is { } lon ? new SnapshotPosition(lat, lon) : null;

        return new JourneySnapshot(
            journey.Id, journey.ServiceDate, journey.LineId, journey.DelayMinutes, journey.Tracking, journey.LastPositionAt, position,
            journey.Stops.OrderBy(s => s.Sequence).Select(s => new SnapshotStop(
                s.StationId, s.Sequence, s.Status, s.ActualArrival, s.ActualDeparture,
                s.ExpectedArrival(delay), s.ExpectedDeparture(delay))).ToList(),
            sequence);
    }
}
