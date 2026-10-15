using SmartRail.LiveOperations.Domain;
using Xunit;

namespace SmartRail.UnitTests.LiveOperations;

// R-04: zone radius 300 m. Inside and below 5 km/h for 20 s = Arrived; leaving after that = Departed;
// entering and leaving without stopping = Passed.
public class StationZoneTests
{
    private static readonly DateTime T0 = new(2026, 10, 14, 4, 0, 0, DateTimeKind.Utc);

    private static ZoneSample At(int seconds, double distanceMetres, double speedKmh) =>
        new(T0.AddSeconds(seconds), distanceMetres, speedKmh);

    // Feeds the samples one by one and returns the event each one produced.
    private static List<ZoneEvent> Run(params ZoneSample[] samples)
    {
        var state = ZoneState.Start;
        var events = new List<ZoneEvent>();
        foreach (var sample in samples)
        {
            var step = StationZone.Observe(state, sample);
            state = step.State;
            events.Add(step.Event);
        }
        return events;
    }

    [Fact]
    public void Arrived_when_inside_zone_and_slow_for_20_seconds()
    {
        var events = Run(At(0, 800, 60), At(5, 200, 3), At(15, 100, 2), At(25, 50, 0));

        Assert.Equal([ZoneEvent.None, ZoneEvent.None, ZoneEvent.None, ZoneEvent.Arrived], events);
    }

    // The arrival time is when the train stopped (first slow sample inside the zone), not the sample that confirmed it.
    [Fact]
    public void Arrived_time_is_the_first_slow_sample_inside_the_zone()
    {
        var state = ZoneState.Start;
        ZoneStep last = default;
        foreach (var sample in new[] { At(0, 800, 60), At(5, 200, 3), At(15, 100, 2), At(25, 50, 0) })
        {
            last = StationZone.Observe(state, sample);
            state = last.State;
        }

        Assert.Equal(ZoneEvent.Arrived, last.Event);
        Assert.Equal(T0.AddSeconds(5), last.EventTime);
    }

    [Fact]
    public void Departed_and_passed_times_are_the_sample_that_left_the_zone()
    {
        var arrived = StationZone.Observe(ZoneState.Start, At(0, 100, 0)).State;
        arrived = StationZone.Observe(arrived, At(20, 100, 0)).State;
        var departed = StationZone.Observe(arrived, At(70, 400, 30));
        Assert.Equal(ZoneEvent.Departed, departed.Event);
        Assert.Equal(T0.AddSeconds(70), departed.EventTime);

        var inside = StationZone.Observe(ZoneState.Start, At(0, 100, 80)).State;
        var passed = StationZone.Observe(inside, At(10, 400, 80));
        Assert.Equal(ZoneEvent.Passed, passed.Event);
        Assert.Equal(T0.AddSeconds(10), passed.EventTime);
    }

    [Fact]
    public void Not_arrived_when_slow_for_only_19_seconds()
    {
        var events = Run(At(0, 100, 2), At(19, 100, 0));

        Assert.Equal([ZoneEvent.None, ZoneEvent.None], events);
    }

    [Fact]
    public void Slow_timer_restarts_when_the_train_speeds_up_again()
    {
        // Slow from 0 s, fast at 10 s, slow again from 15 s: 20 s are reached at 35 s, not at 20 s.
        var events = Run(At(0, 100, 2), At(10, 100, 30), At(15, 100, 2), At(34, 100, 2), At(35, 100, 2));

        Assert.Equal([ZoneEvent.None, ZoneEvent.None, ZoneEvent.None, ZoneEvent.None, ZoneEvent.Arrived], events);
    }

    [Fact]
    public void Exactly_5_kmh_is_not_slow()
    {
        var events = Run(At(0, 100, 5), At(30, 100, 5));

        Assert.Equal([ZoneEvent.None, ZoneEvent.None], events);
    }

    [Fact]
    public void Passed_when_train_crosses_zone_without_stopping()
    {
        var events = Run(At(0, 900, 80), At(10, 200, 80), At(20, 500, 80));

        Assert.Equal([ZoneEvent.None, ZoneEvent.None, ZoneEvent.Passed], events);
    }

    [Fact]
    public void Departed_when_train_leaves_zone_after_arriving()
    {
        var events = Run(At(0, 100, 0), At(20, 100, 0), At(60, 150, 10), At(80, 450, 40));

        Assert.Equal([ZoneEvent.None, ZoneEvent.Arrived, ZoneEvent.None, ZoneEvent.Departed], events);
    }

    [Fact]
    public void Zone_edge_is_300_metres_inclusive()
    {
        var atEdge = StationZone.Observe(ZoneState.Start, At(0, 300, 80));
        var justOutside = StationZone.Observe(ZoneState.Start, At(0, 301, 80));

        Assert.Equal(ZonePhase.Inside, atEdge.State.Phase);
        Assert.Equal(ZonePhase.Outside, justOutside.State.Phase);
    }

    [Fact]
    public void Nothing_is_reported_while_the_train_has_not_entered_the_zone()
    {
        var events = Run(At(0, 5000, 80), At(10, 2000, 80), At(20, 1000, 80));

        Assert.Equal([ZoneEvent.None, ZoneEvent.None, ZoneEvent.None], events);
    }

    [Fact]
    public void Nothing_more_is_reported_once_the_stop_is_finished()
    {
        var events = Run(At(0, 100, 80), At(10, 500, 80), At(20, 100, 0), At(60, 100, 0), At(80, 500, 80));

        Assert.Equal(
            [ZoneEvent.None, ZoneEvent.Passed, ZoneEvent.None, ZoneEvent.None, ZoneEvent.None],
            events);
    }
}
