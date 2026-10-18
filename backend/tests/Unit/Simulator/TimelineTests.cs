using SmartRail.Simulator;
using Xunit;

namespace SmartRail.UnitTests.Simulator;

// Where the simulated train is, by the clock. Three stations in a line (about 11 km apart), the train stops at A and C and passes B.
public class TimelineTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 14, 5, 30, 0, TimeSpan.Zero);

    private static readonly SimStop[] Stops =
    [
        new("A", true, null, T0, 7.00, 80.00),
        new("B", false, T0.AddMinutes(10), T0.AddMinutes(10), 7.10, 80.00),
        new("C", true, T0.AddMinutes(20), T0.AddMinutes(22), 7.20, 80.00),
    ];

    private static Timeline Build(int shiftMinutes = 0, Scenario scenario = Scenario.Normal) =>
        Timeline.Build(Stops, TimeSpan.FromMinutes(shiftMinutes), scenario, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(4));

    [Fact]
    public void The_train_waits_in_the_first_station_until_it_departs()
    {
        var fix = Build().At(T0.AddMinutes(-3));
        Assert.Equal((7.00, 80.00, 0), (fix.Latitude, fix.Longitude, fix.SpeedKmh));
    }

    [Fact]
    public void Halfway_between_two_stations_the_train_is_halfway_between_them_and_moving()
    {
        var fix = Build().At(T0.AddMinutes(5));
        Assert.Equal(7.05, fix.Latitude, 6);
        Assert.InRange(fix.SpeedKmh, 60, 80);   // 11 km in 10 minutes
    }

    [Fact]
    public void A_train_that_stops_waits_from_its_arrival_to_its_departure()
    {
        var timeline = Build();
        Assert.Equal(0, timeline.At(T0.AddMinutes(21)).SpeedKmh);
        Assert.Equal(7.20, timeline.At(T0.AddMinutes(21)).Latitude, 6);
    }

    [Fact]
    public void After_the_last_arrival_the_train_stays_at_the_last_station()
    {
        var timeline = Build();
        Assert.Equal(T0.AddMinutes(22), timeline.End);
        Assert.Equal(7.20, timeline.At(T0.AddHours(3)).Latitude, 6);
    }

    [Fact]
    public void A_delay_moves_the_whole_run_later()
    {
        var timeline = Build(shiftMinutes: 7);
        Assert.Equal(T0.AddMinutes(7), timeline.Start);
        Assert.Equal(7.10, timeline.At(T0.AddMinutes(17)).Latitude, 6);   // at B, ten minutes after it left A
    }

    [Fact]
    public void The_stop_scenario_halts_between_stations_and_makes_everything_after_it_later()
    {
        var timeline = Build(scenario: Scenario.Stop);   // the middle section is B to C
        Assert.Equal(T0.AddMinutes(27), timeline.End);   // five minutes later than the timetable
        var halted = timeline.At(T0.AddMinutes(17));     // halfway B-C is at +15, the halt lasts until +20
        Assert.Equal(0, halted.SpeedKmh);
        Assert.Equal(7.15, halted.Latitude, 6);
    }

    [Fact]
    public void The_gap_scenario_is_silent_for_the_given_time_in_the_middle_of_a_section()
    {
        var timeline = Build(scenario: Scenario.Gap);
        Assert.Equal((T0.AddMinutes(15), T0.AddMinutes(19)), timeline.Gap);
        Assert.False(timeline.IsSilent(T0.AddMinutes(14)));
        Assert.True(timeline.IsSilent(T0.AddMinutes(17)));
        Assert.False(timeline.IsSilent(T0.AddMinutes(19)));
        Assert.Equal(T0.AddMinutes(22), timeline.End);   // the train itself is not delayed
    }

    [Fact]
    public void A_timetable_that_goes_backwards_is_refused()
    {
        SimStop[] bad = [Stops[0], Stops[1] with { Arrival = T0.AddMinutes(-5), Departure = T0.AddMinutes(-5) }];
        Assert.Throws<InvalidOperationException>(() => Timeline.Build(bad, TimeSpan.Zero, Scenario.Normal, TimeSpan.Zero, TimeSpan.Zero));
    }

    [Fact]
    public async Task The_instant_clock_moves_forward_by_what_it_is_asked_to_wait()
    {
        var time = new InstantSimulatorTime(T0);
        await time.WaitAsync(TimeSpan.FromMinutes(3), TestContext.Current.CancellationToken);
        Assert.Equal(T0.AddMinutes(3), time.Now);
    }
}
