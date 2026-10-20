using SmartRail.Simulator;
using Xunit;

namespace SmartRail.UnitTests.Simulator;

// The simulated train waits at a held station until the hold is lifted (R-22), then carries on from where it stood.
public class HoldGateTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 20, 1, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(10);

    // A, B, C: the train waits in B from 01:10 to 01:12.
    private static Timeline Line() => Timeline.Build(
    [
        new SimStop("A", true, null, T0, 7.0, 80.0),
        new SimStop("B", true, T0.AddMinutes(10), T0.AddMinutes(12), 7.1, 80.1),
        new SimStop("C", true, T0.AddMinutes(20), null, 7.2, 80.2),
    ], TimeSpan.Zero, Scenario.Normal, TimeSpan.Zero, TimeSpan.Zero);

    // The simulator asks every tick: this plays the ticks from `from` to `to` (inclusive) with the hold set.
    private static void Hold(HoldGate gate, Timeline line, DateTimeOffset from, DateTimeOffset to, int stop)
    {
        for (var now = from; now <= to; now += Tick)
        {
            gate.Update(now, line, stop, Tick);
        }
    }

    [Fact]
    public void Without_a_hold_the_train_keeps_the_real_time()
    {
        var gate = new HoldGate();

        Assert.False(gate.Update(T0.AddMinutes(11), Line(), heldStop: null, Tick));
        Assert.Equal(T0.AddMinutes(11), gate.TrainTime(T0.AddMinutes(11)));
    }

    [Fact]
    public void A_held_train_stands_in_the_station_just_before_its_departure_time_for_as_long_as_the_hold_lasts()
    {
        var gate = new HoldGate();
        var line = Line();

        Assert.False(gate.Update(T0.AddMinutes(11), line, heldStop: 1, Tick));   // still dwelling: nothing to hold yet
        Assert.True(gate.Update(T0.AddMinutes(12), line, heldStop: 1, Tick));    // would leave now
        var standing = gate.TrainTime(T0.AddMinutes(12));
        Hold(gate, line, T0.AddMinutes(12).Add(Tick), T0.AddMinutes(30), stop: 1);   // eighteen minutes later

        Assert.Equal(standing, gate.TrainTime(T0.AddMinutes(30)));
        Assert.Equal(T0.AddMinutes(12).AddSeconds(-1), standing);   // still inside the station, not yet leaving
    }

    [Fact]
    public void After_the_release_the_train_goes_on_and_is_late_by_exactly_the_time_it_was_held()
    {
        var gate = new HoldGate();
        var line = Line();
        Hold(gate, line, T0.AddMinutes(12), T0.AddMinutes(30), stop: 1);   // held for 18 minutes and 1 second

        Assert.False(gate.Update(T0.AddMinutes(30).AddSeconds(10), line, heldStop: null, Tick));

        var trainTime = gate.TrainTime(T0.AddMinutes(30).AddSeconds(10));
        Assert.Equal(T0.AddMinutes(12).AddSeconds(9), trainTime);   // 10 s after the release, 9 s past "just before leaving"
        Assert.Equal(TimeSpan.FromMinutes(18).Add(TimeSpan.FromSeconds(1)), gate.Held);
    }

    [Fact]
    public void A_hold_on_a_station_the_train_has_not_reached_does_not_stop_it_early()
    {
        var gate = new HoldGate();

        Assert.False(gate.Update(T0.AddMinutes(5), Line(), heldStop: 2, Tick));
        Assert.Equal(T0.AddMinutes(5), gate.TrainTime(T0.AddMinutes(5)));
    }

    [Fact]
    public void A_hold_on_a_station_the_train_has_already_left_is_ignored()
    {
        var gate = new HoldGate();

        Assert.False(gate.Update(T0.AddMinutes(15), Line(), heldStop: 1, Tick));
        Assert.Equal(TimeSpan.Zero, gate.Held);
    }
}
