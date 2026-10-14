using SmartRail.LiveOperations.Domain;
using Xunit;

namespace SmartRail.UnitTests.LiveOperations;

// R-06: 2 min without an accepted position = Stale, 10 min = Lost, any accepted position = Live.
public class TrackingFreshnessTests
{
    private static readonly DateTime LastPosition = new(2026, 10, 14, 4, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Tracking_is_live_just_under_2_minutes_after_the_last_position()
    {
        Assert.Equal(TrackingStatus.Live, TrackingFreshness.Of(LastPosition, LastPosition.AddSeconds(119)));
    }

    [Fact]
    public void Tracking_becomes_stale_after_2_minutes_without_position()
    {
        Assert.Equal(TrackingStatus.Stale, TrackingFreshness.Of(LastPosition, LastPosition.AddMinutes(2)));
        Assert.Equal(TrackingStatus.Stale, TrackingFreshness.Of(LastPosition, LastPosition.AddSeconds(599)));
    }

    [Fact]
    public void Tracking_becomes_lost_after_10_minutes_without_position()
    {
        Assert.Equal(TrackingStatus.Lost, TrackingFreshness.Of(LastPosition, LastPosition.AddMinutes(10)));
        Assert.Equal(TrackingStatus.Lost, TrackingFreshness.Of(LastPosition, LastPosition.AddHours(3)));
    }

    // A newly accepted position becomes the "last position", so the same call gives Live again.
    [Fact]
    public void Tracking_returns_to_live_on_next_accepted_position()
    {
        var now = LastPosition.AddMinutes(12);
        Assert.Equal(TrackingStatus.Lost, TrackingFreshness.Of(LastPosition, now));

        Assert.Equal(TrackingStatus.Live, TrackingFreshness.Of(lastPositionAt: now, now: now));
    }
}
