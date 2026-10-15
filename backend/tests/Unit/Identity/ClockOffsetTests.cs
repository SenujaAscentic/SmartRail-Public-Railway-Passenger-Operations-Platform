using SmartRail.Identity.Tokens;
using Xunit;

namespace SmartRail.UnitTests.Identity;

// R-09: the cab tablet's clock may be wrong. At sign-in the server measures how far off it is, and later times from
// the device are corrected by that offset (event time = device time + offset).
public class ClockOffsetTests
{
    private static readonly DateTimeOffset Server = new(2026, 10, 15, 4, 30, 0, TimeSpan.Zero);

    [Fact]
    public void A_tablet_clock_that_is_5_seconds_slow_gives_plus_5000_ms()
    {
        Assert.True(ClockOffset.TryMeasure(Server, Server.AddSeconds(-5), out var offsetMs));
        Assert.Equal(5000, offsetMs);
    }

    [Fact]
    public void A_tablet_clock_that_is_ahead_gives_a_negative_offset()
    {
        Assert.True(ClockOffset.TryMeasure(Server, Server.AddMinutes(3), out var offsetMs));
        Assert.Equal(-180_000, offsetMs);
    }

    [Fact]
    public void A_correct_clock_gives_zero()
    {
        Assert.True(ClockOffset.TryMeasure(Server, Server, out var offsetMs));
        Assert.Equal(0, offsetMs);
    }

    [Fact]
    public void An_offset_of_exactly_24_hours_is_accepted_and_anything_more_is_not()
    {
        Assert.True(ClockOffset.TryMeasure(Server, Server.AddHours(-24), out _));
        Assert.True(ClockOffset.TryMeasure(Server, Server.AddHours(24), out _));
        Assert.False(ClockOffset.TryMeasure(Server, Server.AddHours(-24).AddMilliseconds(-1), out _));
        Assert.False(ClockOffset.TryMeasure(Server, Server.AddHours(24).AddMilliseconds(1), out _));
    }

    [Fact]
    public void The_device_time_is_read_as_the_same_instant_whatever_offset_it_is_written_with()
    {
        var deviceWritten = new DateTimeOffset(2026, 10, 15, 10, 0, 0, TimeSpan.FromHours(5.5));   // 04:30 UTC

        Assert.True(ClockOffset.TryMeasure(Server, deviceWritten, out var offsetMs));
        Assert.Equal(0, offsetMs);
    }
}
