namespace SmartRail.Simulator;

// What the simulated train does (scenarios): a normal run along the timetable, a halt between two stations, or a stretch with no GPS.
public enum Scenario { Normal, Stop, Gap }

// One stop of the journey as the public API describes it, with the station's coordinates from GET /lines.
public sealed record SimStop(string Name, bool Stops, DateTimeOffset? Arrival, DateTimeOffset? Departure, double Latitude, double Longitude, Guid StationId = default);

// One GPS reading: where the train is and how fast it goes.
public readonly record struct SimFix(double Latitude, double Longitude, double SpeedKmh);

// Where the train is at any moment. Built from the journey's own timetable, so the simulated run can be checked against the same
// times the system shows: the train waits in each station from its arrival to its departure (so the 20 seconds of R-04 are met),
// and moves in a straight line between stations, which is the route the backend uses to judge GPS (R-08).
//
// The three scenarios:
//   Normal  runs to the timetable, plus the delay asked for.
//   Stop    halts for a while in the middle of a section, far from any station; everything after is later by that time.
//   Gap     keeps moving but sends no GPS for a while (a tunnel, a dead antenna): the caller asks Gap for the silent stretch.
public sealed class Timeline
{
    private const double EarthRadiusMetres = 6_371_000;

    private readonly List<(DateTimeOffset Time, double Latitude, double Longitude)> _keys = [];

    // When the train reaches and leaves each stop (index = position in the journey), for holds: the train waits until released.
    private readonly List<(DateTimeOffset Arrival, DateTimeOffset Departure)> _stopTimes = [];

    public DateTimeOffset ArrivalAt(int stopIndex) => _stopTimes[stopIndex].Arrival;

    public DateTimeOffset DepartureAt(int stopIndex) => _stopTimes[stopIndex].Departure;

    public DateTimeOffset Start => _keys[0].Time;

    public DateTimeOffset End => _keys[^1].Time;

    // The silent stretch of the Gap scenario, or null.
    public (DateTimeOffset From, DateTimeOffset To)? Gap { get; private set; }

    // `shift`: how much later than the timetable the whole run is (a delay, or a late start). `pause` and `gap` are used by the
    // Stop and Gap scenarios.
    public static Timeline Build(IReadOnlyList<SimStop> stops, TimeSpan shift, Scenario scenario, TimeSpan pause, TimeSpan gapLength)
    {
        if (stops.Count < 2)
        {
            throw new ArgumentException("A journey needs at least two stops.", nameof(stops));
        }

        var timeline = new Timeline();
        var middleSection = (stops.Count - 1) / 2;   // between stop k and stop k + 1
        var later = TimeSpan.Zero;                   // extra delay picked up after the halt

        for (var i = 0; i < stops.Count; i++)
        {
            var stop = stops[i];

            if (i > 0)
            {
                // Between the previous stop and this one: the halt or the silent stretch, in the middle of the section.
                var before = stops[i - 1];
                var leaves = (before.Departure ?? before.Arrival!.Value) + shift + later;
                var arrives = (stop.Arrival ?? stop.Departure!.Value) + shift + later;
                if (i - 1 == middleSection && scenario != Scenario.Normal)
                {
                    var half = leaves + (arrives - leaves) / 2;
                    var (midLat, midLng) = ((before.Latitude + stop.Latitude) / 2, (before.Longitude + stop.Longitude) / 2);
                    if (scenario == Scenario.Stop)
                    {
                        timeline.Add(half, midLat, midLng);
                        timeline.Add(half + pause, midLat, midLng);
                        later += pause;
                    }
                    else
                    {
                        timeline.Gap = (half, half + gapLength);
                    }
                }
            }

            var arrival = (stop.Arrival ?? stop.Departure!.Value) + shift + later;
            var departure = (stop.Departure ?? stop.Arrival!.Value) + shift + later;
            if (i > 0)
            {
                timeline.Add(arrival, stop.Latitude, stop.Longitude);
            }

            timeline.Add(departure, stop.Latitude, stop.Longitude);   // a train that stops waits here; one that passes does not
            timeline._stopTimes.Add((arrival, departure));
        }

        return timeline;
    }

    // Where the train is at `time`. Before the start it waits at the first station, after the end at the last.
    public SimFix At(DateTimeOffset time)
    {
        if (time <= _keys[0].Time)
        {
            return new SimFix(_keys[0].Latitude, _keys[0].Longitude, 0);
        }

        if (time >= _keys[^1].Time)
        {
            return new SimFix(_keys[^1].Latitude, _keys[^1].Longitude, 0);
        }

        for (var i = 0; i < _keys.Count - 1; i++)
        {
            var (a, b) = (_keys[i], _keys[i + 1]);
            if (time < a.Time || time > b.Time)
            {
                continue;
            }

            var seconds = (b.Time - a.Time).TotalSeconds;
            if (seconds <= 0)
            {
                continue;
            }

            var metres = Distance(a.Latitude, a.Longitude, b.Latitude, b.Longitude);
            var fraction = (time - a.Time).TotalSeconds / seconds;
            return new SimFix(
                a.Latitude + (b.Latitude - a.Latitude) * fraction,
                a.Longitude + (b.Longitude - a.Longitude) * fraction,
                metres / seconds * 3.6);
        }

        throw new InvalidOperationException("The timeline has a gap in its times.");
    }

    // True while the Gap scenario's GPS is silent.
    public bool IsSilent(DateTimeOffset time) => Gap is { } gap && time >= gap.From && time < gap.To;

    private void Add(DateTimeOffset time, double latitude, double longitude)
    {
        // Equal times are allowed (a train that passes a station has one time there); going backwards is not.
        if (_keys.Count > 0 && time < _keys[^1].Time)
        {
            throw new InvalidOperationException("The timetable goes backwards in time.");
        }

        _keys.Add((time, latitude, longitude));
    }

    public static double Distance(double lat1, double lng1, double lat2, double lng2)
    {
        double Rad(double d) => d * Math.PI / 180;
        var dLat = Rad(lat2 - lat1);
        var dLng = Rad(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * EarthRadiusMetres * Math.Asin(Math.Sqrt(a));
    }
}
