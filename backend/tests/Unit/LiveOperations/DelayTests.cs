using SmartRail.LiveOperations.Domain;
using Xunit;

namespace SmartRail.UnitTests.LiveOperations;

// R-01 (delay), R-02 (expected arrival), R-03 (expected departure). Domain model sections 5 and 10.
public class DelayTests
{
    private static readonly DateTime Scheduled = new(2026, 10, 14, 4, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Delay_is_zero_when_train_departs_59_seconds_late()
    {
        var delay = Delay.Between(Scheduled, Scheduled.AddSeconds(59));

        Assert.Equal(0, delay.Minutes);
        Assert.True(delay.IsOnTime);
    }

    [Fact]
    public void Delay_is_one_minute_when_train_departs_119_seconds_late()
    {
        var delay = Delay.Between(Scheduled, Scheduled.AddSeconds(119));

        Assert.Equal(1, delay.Minutes);
        Assert.True(delay.IsLate);
    }

    [Fact]
    public void Delay_is_negative_when_train_arrives_early()
    {
        var delay = Delay.Between(Scheduled, Scheduled.AddMinutes(-3));

        Assert.Equal(-3, delay.Minutes);
        Assert.True(delay.IsEarly);
    }

    [Fact]
    public void Expected_arrival_adds_current_delay_to_scheduled_arrival()
    {
        Assert.Equal(Scheduled.AddMinutes(7), ExpectedTimes.Arrival(Scheduled, new Delay(7)));
    }

    // R-02: early running carries forward for arrivals.
    [Fact]
    public void Expected_arrival_is_earlier_when_train_is_running_early()
    {
        Assert.Equal(Scheduled.AddMinutes(-2), ExpectedTimes.Arrival(Scheduled, new Delay(-2)));
    }

    [Fact]
    public void Expected_departure_is_never_before_scheduled_departure_when_running_early()
    {
        Assert.Equal(Scheduled, ExpectedTimes.Departure(Scheduled, new Delay(-4)));
    }

    [Fact]
    public void Expected_departure_adds_delay_when_running_late()
    {
        Assert.Equal(Scheduled.AddMinutes(5), ExpectedTimes.Departure(Scheduled, new Delay(5)));
    }

    // R-03: only the prediction is clamped. An actual early departure gives a negative delay, as it happened.
    [Fact]
    public void Actual_early_departure_is_recorded_as_it_happened()
    {
        var delay = Delay.Between(Scheduled, Scheduled.AddMinutes(-2));

        Assert.Equal(-2, delay.Minutes);
        Assert.True(delay.IsEarly);
    }
}
