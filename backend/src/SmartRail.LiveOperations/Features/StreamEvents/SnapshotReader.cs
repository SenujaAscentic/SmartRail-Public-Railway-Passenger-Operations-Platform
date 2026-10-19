using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Features.JourneyViews;
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
    JourneyStatus Status,
    int DelayMinutes,
    TrackingStatus Tracking,
    DateTime? LastSeenAt,
    SnapshotPosition? LastPosition,
    IReadOnlyList<SnapshotStop> Stops,
    // The same public journey detail as GET /journeys/{id} (hold, reason, platforms, version, last position, freshness): added 18 Oct so a
    // reconnecting app does not have to ask again. The flat fields above stay for the apps that already read them.
    JourneyDetailResponse Journey,
    long Sequence,
    // Who supplied the delay (Controller, Crew, Gps, Schedule). Staff only (T-07): never serialised here; /ops/stream writes it by hand.
    [property: JsonIgnore] string? DelaySource = null);

// The same fields as GET /journeys/{id} freshness.lastPosition and the TrainPositionUpdated payload.
public sealed record SnapshotPosition(double Latitude, double Longitude, double SpeedKmh, Guid LastStationId, Guid NextStationId, double FractionToNext);

// Expected times are what R-02 / R-03 give now; once a stop has really happened they are the actual times.
public sealed record SnapshotStop(
    Guid StationId,
    int Sequence,
    StopStatus Status,
    DateTime? ActualArrival,
    DateTime? ActualDeparture,
    DateTime? ExpectedArrival,
    DateTime? ExpectedDeparture,
    // Who supplied the actual times at this stop. Staff only, like DelaySource.
    [property: JsonIgnore] string? ActualArrivalSource = null,
    [property: JsonIgnore] string? ActualDepartureSource = null);

internal sealed class SnapshotReader(LiveOperationsDbContext db, TimeProvider clock, JourneyDetailReader detailReader)
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

        var snapshots = new List<JourneySnapshot>();
        foreach (var journey in journeys)
        {
            snapshots.Add(ToSnapshot(journey, sequence, await detailReader.BuildAsync(journey, cancellationToken, tolerant: true)));
        }

        return snapshots;
    }

    internal static JourneySnapshot ToSnapshot(OperationalJourney journey, long sequence, JourneyDetailResponse detail)
    {
        var delay = journey.CurrentDelay.Value;
        var position = journey.LastLatitude is { } lat && journey.LastLongitude is { } lon
            && journey.LastSpeedKmh is { } speed && journey.LastStationId is { } last && journey.NextStationId is { } next && journey.FractionToNext is { } fraction
            ? new SnapshotPosition(lat, lon, speed, last, next, fraction)
            : null;

        return new JourneySnapshot(
            journey.Id, journey.ServiceDate, journey.LineId, journey.Status, journey.DelayMinutes, journey.Tracking, journey.LastPositionAt, position,
            journey.Stops.OrderBy(s => s.Sequence).Select(s => new SnapshotStop(
                s.StationId, s.Sequence, s.Status, s.ActualArrival, s.ActualDeparture,
                s.ExpectedArrival(delay), s.ExpectedDeparture(delay), s.ActualArrivalSource?.ToString(), s.ActualDepartureSource?.ToString())).ToList(),
            detail,
            sequence,
            journey.DelaySource.ToString());
    }
}
