using SmartRail.Network.Seed;
using Xunit;

namespace SmartRail.UnitTests.Network.Seed;

// Checks that the loader can rely on: text values are known, keys are unique, sequences have no gaps.
// Without these the loader would fail with a confusing error from the database instead of a clear message.
public class SeedValidatorValueTests
{
    private static void AssertError(NetworkSeedData data, SeedRule expected) =>
        Assert.Contains(SeedValidator.Validate(data).Errors, e => e.Rule == expected);

    // ---- unknown text values ----

    [Fact]
    public void Unknown_direction_is_rejected()
    {
        var data = SeedTestData.Valid();
        data = data with { Routes = [data.Routes[0] with { Direction = "Sideways" }] };

        AssertError(data, SeedRule.UnknownValue);
    }

    [Fact]
    public void Unknown_train_type_is_rejected()
    {
        var data = SeedTestData.Valid();
        data = data with { Services = [data.Services[0] with { TrainType = "Rocket" }] };

        AssertError(data, SeedRule.UnknownValue);
    }

    [Fact]
    public void Unknown_call_type_is_rejected()
    {
        var data = SeedTestData.Valid().WithStops(
            new(1, "AAA", "Stop", null, 360, "1"),
            new(2, "BBB", "Halt", 380, 382, "2"),
            new(3, "CCC", "Stop", 400, null, "1"));

        AssertError(data, SeedRule.UnknownValue);
    }

    [Fact]
    public void Unknown_traction_is_rejected()
    {
        var data = SeedTestData.Valid();
        data = data with { TrainSets = [data.TrainSets[0] with { Traction = "Steam" }] };

        AssertError(data, SeedRule.UnknownValue);
    }

    [Fact]
    public void Unknown_coach_class_is_rejected()
    {
        var data = SeedTestData.Valid();
        var coaches = new List<CoachSeed> { data.TrainSets[0].Coaches[0] with { Class = "Business" }, data.TrainSets[0].Coaches[1] };
        data = data with { TrainSets = [data.TrainSets[0] with { Coaches = coaches }] };

        AssertError(data, SeedRule.UnknownValue);
    }

    [Fact]
    public void Unknown_seating_is_rejected()
    {
        var data = SeedTestData.Valid();
        var coaches = new List<CoachSeed> { data.TrainSets[0].Coaches[0] with { Seating = "Standing" }, data.TrainSets[0].Coaches[1] };
        data = data with { TrainSets = [data.TrainSets[0] with { Coaches = coaches }] };

        AssertError(data, SeedRule.UnknownValue);
    }

    [Fact]
    public void Unknown_day_of_week_is_rejected()
    {
        var data = SeedTestData.Valid();
        data = data with { Calendars = [data.Calendars[0] with { DaysOfWeek = ["Mon", "Funday"] }] };

        AssertError(data, SeedRule.UnknownValue);
    }

    // ---- unknown reference ----

    [Fact]
    public void Route_referencing_an_unknown_line_is_rejected()
    {
        var data = SeedTestData.Valid();
        data = data with { Routes = [data.Routes[0] with { LineId = "NO-SUCH-LINE" }] };

        AssertError(data, SeedRule.UnknownLine);
    }

    // ---- duplicate keys ----

    [Fact]
    public void Duplicate_line_ids_are_rejected()
    {
        var data = SeedTestData.Valid();
        data = data with { Lines = [data.Lines[0], data.Lines[0]] };

        AssertError(data, SeedRule.DuplicateKey);
    }

    [Fact]
    public void Duplicate_route_ids_are_rejected()
    {
        var data = SeedTestData.Valid();
        data = data with { Routes = [data.Routes[0], data.Routes[0]] };

        AssertError(data, SeedRule.DuplicateKey);
    }

    [Fact]
    public void Duplicate_service_numbers_are_rejected()
    {
        var data = SeedTestData.Valid();
        data = data with { Services = [data.Services[0], data.Services[0]] };

        AssertError(data, SeedRule.DuplicateKey);
    }

    [Fact]
    public void Duplicate_calendar_ids_are_rejected()
    {
        var data = SeedTestData.Valid();
        data = data with { Calendars = [data.Calendars[0], data.Calendars[0]] };

        AssertError(data, SeedRule.DuplicateKey);
    }

    [Fact]
    public void Duplicate_calendar_exception_dates_are_rejected()
    {
        var data = SeedTestData.Valid();
        var poya = new CalendarExceptionSeed(new DateOnly(2026, 10, 25), false, "Vap Poya");
        data = data with { Calendars = [data.Calendars[0] with { Exceptions = [poya, poya] }] };

        AssertError(data, SeedRule.DuplicateKey);
    }

    [Fact]
    public void Duplicate_train_set_codes_are_rejected()
    {
        var data = SeedTestData.Valid();
        data = data with { TrainSets = [data.TrainSets[0], data.TrainSets[0]] };

        AssertError(data, SeedRule.DuplicateKey);
    }

    // ---- sequences without gaps ----

    [Fact]
    public void Route_station_sequence_with_a_gap_is_rejected()
    {
        var data = SeedTestData.Valid();
        var stations = data.Routes[0].Stations.ToList();
        stations[2] = stations[2] with { Sequence = 5 };   // 1, 2, 5
        data = data with { Routes = [data.Routes[0] with { Stations = stations }] };

        AssertError(data, SeedRule.SequenceNotContiguous);
    }

    [Fact]
    public void Stop_sequence_with_a_gap_is_rejected()
    {
        var data = SeedTestData.Valid().WithStops(
            new(1, "AAA", "Stop", null, 360, "1"),
            new(3, "BBB", "Stop", 380, 382, "2"),     // 1, 3, 4
            new(4, "CCC", "Stop", 400, null, "1"));

        AssertError(data, SeedRule.SequenceNotContiguous);
    }

    [Fact]
    public void Coach_positions_with_a_gap_are_rejected()
    {
        var data = SeedTestData.Valid();
        var coaches = new List<CoachSeed> { data.TrainSets[0].Coaches[0], data.TrainSets[0].Coaches[1] with { Position = 4 } };
        data = data with { TrainSets = [data.TrainSets[0] with { Coaches = coaches }] };

        AssertError(data, SeedRule.SequenceNotContiguous);
    }
}
