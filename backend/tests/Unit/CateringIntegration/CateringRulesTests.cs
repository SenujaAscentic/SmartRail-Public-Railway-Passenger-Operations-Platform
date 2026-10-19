using SmartRail.CateringIntegration.Menus;
using Xunit;

namespace SmartRail.UnitTests.CateringIntegration;

// openNow and the freshness of the cached menu (US-P8, US-F3). Opening hours are a Colombo time of day; Colombo is UTC+05:30.
public class CateringRulesTests
{
    private static DateTimeOffset Utc(int hour, int minute) => new(2026, 10, 20, hour, minute, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, 29, false)]    // 05:59 in Colombo: not yet
    [InlineData(0, 30, true)]     // 06:00: opens
    [InlineData(10, 0, true)]     // 15:30
    [InlineData(14, 29, true)]    // 19:59
    [InlineData(14, 30, false)]   // 20:00: closed from here
    [InlineData(20, 0, false)]    // 01:30 next day
    public void The_hotline_is_open_from_the_opening_time_up_to_but_not_including_the_closing_time_in_Colombo_time(int hour, int minute, bool open)
    {
        Assert.Equal(open, CateringRules.IsOpenNow(Utc(hour, minute), "06:00", "20:00"));
    }

    [Fact]
    public void The_Colombo_day_is_used_not_the_UTC_day()
    {
        // 20:00 UTC on the 19th is 01:30 on the 20th in Colombo.
        Assert.False(CateringRules.IsOpenNow(new DateTimeOffset(2026, 10, 19, 20, 0, 0, TimeSpan.Zero), "06:00", "20:00"));
    }

    [Fact]
    public void Hours_that_run_past_midnight_are_open_on_both_sides_of_it()
    {
        Assert.True(CateringRules.IsOpenNow(Utc(17, 0), "22:00", "02:00"));    // 22:30 in Colombo
        Assert.True(CateringRules.IsOpenNow(Utc(20, 0), "22:00", "02:00"));    // 01:30
        Assert.False(CateringRules.IsOpenNow(Utc(8, 0), "22:00", "02:00"));    // 13:30
    }

    [Fact]
    public void Hours_that_cannot_be_read_are_never_reported_as_open()
    {
        Assert.False(CateringRules.IsOpenNow(Utc(5, 0), "soon", "20:00"));
    }

    [Fact]
    public void The_menu_is_available_while_the_cache_is_at_most_15_minutes_old_and_the_provider_takes_orders()
    {
        var fetched = Utc(5, 0).UtcDateTime;
        Assert.True(CateringRules.IsAvailable(fetched, true, Utc(5, 15)));
        Assert.False(CateringRules.IsAvailable(fetched, true, Utc(5, 16)));
        Assert.False(CateringRules.IsAvailable(fetched, false, Utc(5, 1)));
    }
}
