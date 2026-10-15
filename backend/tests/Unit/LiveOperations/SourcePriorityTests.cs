using SmartRail.LiveOperations.Domain;
using Xunit;

namespace SmartRail.UnitTests.LiveOperations;

// R-05: Controller > Crew > GPS > Schedule. A lower source never overwrites a higher one's time at the same stop.
// The journey delay follows the most recent stop event whatever its source; only a controller delay blocks it.
public class SourcePriorityTests
{
    private static readonly DateTime T = new(2026, 10, 14, 4, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Gps_arrival_does_not_overwrite_crew_tapped_arrival()
    {
        var crew = new TimeWithSource(T, Source.Crew);
        var gps = new TimeWithSource(T.AddMinutes(1), Source.Gps);

        Assert.Equal(crew, TimeWithSource.Resolve(crew, gps));
    }

    [Fact]
    public void Crew_tap_overwrites_gps_arrival()
    {
        var gps = new TimeWithSource(T, Source.Gps);
        var crew = new TimeWithSource(T.AddMinutes(1), Source.Crew);

        Assert.Equal(crew, TimeWithSource.Resolve(gps, crew));
    }

    [Fact]
    public void First_value_is_accepted_whatever_its_source()
    {
        var schedule = new TimeWithSource(T, Source.Schedule);

        Assert.Equal(schedule, TimeWithSource.Resolve(null, schedule));
    }

    [Fact]
    public void Same_source_overwrites_so_a_newer_gps_fix_replaces_an_older_one()
    {
        var older = new TimeWithSource(T, Source.Gps);
        var newer = new TimeWithSource(T.AddSeconds(30), Source.Gps);

        Assert.Equal(newer, TimeWithSource.Resolve(older, newer));
    }

    [Fact]
    public void Controller_delay_is_kept_until_controller_clears_it()
    {
        var delay = SourcedDelay.FromSchedule
            .Apply(new Delay(10), Source.Controller)
            .Apply(new Delay(2), Source.Gps)
            .Apply(new Delay(3), Source.Crew);

        Assert.Equal(new SourcedDelay(new Delay(10), Source.Controller), delay);

        // Clearing hands back the delay recalculated from the latest recorded stop time (here 4 min late, from GPS),
        // not 0: a reset would show a false "On time" (US-P4).
        var recalculated = new SourcedDelay(new Delay(4), Source.Gps);
        var cleared = delay.ClearController(recalculated);
        Assert.Equal(recalculated, cleared);

        // After the clear, GPS values apply again.
        Assert.Equal(new SourcedDelay(new Delay(2), Source.Gps), cleared.Apply(new Delay(2), Source.Gps));
    }

    [Fact]
    public void A_crew_delay_does_not_block_a_later_gps_delay()
    {
        var delay = SourcedDelay.FromSchedule
            .Apply(new Delay(3), Source.Crew)
            .Apply(new Delay(6), Source.Gps);

        Assert.Equal(new SourcedDelay(new Delay(6), Source.Gps), delay);
    }

    [Fact]
    public void Controller_can_replace_their_own_delay()
    {
        var delay = SourcedDelay.FromSchedule
            .Apply(new Delay(10), Source.Controller)
            .Apply(new Delay(4), Source.Controller);

        Assert.Equal(new SourcedDelay(new Delay(4), Source.Controller), delay);
    }

    [Fact]
    public void Clearing_does_nothing_when_the_delay_was_not_set_by_the_controller()
    {
        var gps = SourcedDelay.FromSchedule.Apply(new Delay(2), Source.Gps);

        Assert.Equal(gps, gps.ClearController(new SourcedDelay(new Delay(9), Source.Crew)));
    }
}
