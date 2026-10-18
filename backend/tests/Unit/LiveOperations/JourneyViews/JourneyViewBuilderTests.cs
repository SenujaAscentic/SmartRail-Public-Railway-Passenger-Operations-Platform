using System.Text.Json;
using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Features.JourneyViews;
using SmartRail.Network;
using Xunit;

namespace SmartRail.UnitTests.LiveOperations.JourneyViews;

// The public view of a journey (contract section 5). Pure: no database, no clock.
public class JourneyViewBuilderTests
{
    private static readonly Guid S1 = Guid.NewGuid();   // origin, departs 06:00
    private static readonly Guid S2 = Guid.NewGuid();   // 06:40 / 06:42
    private static readonly Guid S3 = Guid.NewGuid();   // terminus, arrives 07:30

    private static readonly IReadOnlyDictionary<Guid, string> Names =
        new Dictionary<Guid, string> { [S1] = "Colombo Fort", [S2] = "Rambukkana", [S3] = "Kandy" };

    private static readonly ServiceDetails Service = new("1005", "Express");

    private static DateTime At(int hour, int minute) => new(2026, 10, 14, hour, minute, 0, DateTimeKind.Utc);

    private static OperationalJourney NewJourney() =>
        OperationalJourney.Plan(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 14), Guid.NewGuid(),
            [
                new PlannedStop(1, S1, CallType.Stop, null, 360, "3"),
                new PlannedStop(2, S2, CallType.Pass, 400, 400, "1"),
                new PlannedStop(3, S3, CallType.Stop, 450, null, "2"),
            ],
            TimeZoneInfo.Utc, Guid.NewGuid());

    private static JourneySummaryResponse Summary(OperationalJourney j) =>
        JourneyViewBuilder.Summary(j, j.Stops[0], j.Stops[2], Names, Service, JourneyViewBuilder.Name("Kandy", "Express"));

    // ---- status ----

    [Fact]
    public void A_journey_nobody_has_reported_on_is_NotStarted_never_OnTime()
    {
        var status = JourneyViewBuilder.Status(NewJourney());

        Assert.Equal(new JourneyStatusResponse("NotStarted", null), status);
    }

    [Theory]
    [InlineData(4, "Late", 4)]
    [InlineData(0, "OnTime", 0)]
    [InlineData(-2, "Early", -2)]
    public void After_a_crew_tap_the_status_follows_the_delay(int minutesLate, string kind, int delay)
    {
        var journey = NewJourney();
        journey.RecordCrewTime(S2, StopEventKind.Arrival, At(6, 40).AddMinutes(minutesLate));

        Assert.Equal(new JourneyStatusResponse(kind, delay), JourneyViewBuilder.Status(journey));
    }

    [Fact]
    public void A_controller_delay_on_a_journey_without_positions_is_Late_not_NotStarted()
    {
        var journey = NewJourney();
        journey.SetControllerDelay(new Delay(10), At(6, 0));

        Assert.Equal(new JourneyStatusResponse("Late", 10), JourneyViewBuilder.Status(journey));
    }

    // ---- summary ----

    [Fact]
    public void The_name_is_the_terminus_and_the_train_type()
    {
        Assert.Equal("Kandy Express", JourneyViewBuilder.Name("Kandy", "Express"));
    }

    [Fact]
    public void The_summary_has_the_contract_fields_and_the_ones_that_wait_for_the_decisions_are_null()
    {
        var journey = NewJourney();
        journey.RecordCrewTime(S1, StopEventKind.Departure, At(6, 5));   // 5 min late leaving

        var summary = Summary(journey);

        Assert.Equal(("1005", "Kandy Express", "Express"), (summary.ServiceNumber, summary.Name, summary.TrainType));
        Assert.Equal(new JourneyStatusResponse("Late", 5), summary.Status);
        Assert.Equal(new JourneyFromResponse(S1, "Colombo Fort", At(6, 0), At(6, 5), "3", null), summary.From);   // actual departure
        Assert.Equal(new JourneyToResponse(S3, "Kandy", At(7, 30), At(7, 35)), summary.To);                       // R-02
        Assert.Equal(new JourneyFreshnessResponse("NotStarted", null), summary.Freshness);
        Assert.Null(summary.Hold);
        Assert.Null(summary.Reason);
        Assert.Equal(0, summary.Version);
    }

    [Fact]
    public void Fields_that_wait_for_later_phases_are_in_the_json_as_null_so_the_app_types_never_change()
    {
        var json = JsonSerializer.Serialize(Summary(NewJourney()), new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"hold\":null", json);
        Assert.Contains("\"reason\":null", json);
        Assert.Contains("\"previousPlatform\":null", json);
        Assert.Contains("\"version\":0", json);
        Assert.Contains("\"updatedAt\":null", json);
    }

    // ---- detail ----

    [Fact]
    public void The_detail_lists_the_stops_in_order_with_scheduled_expected_and_actual_times()
    {
        var journey = NewJourney();
        journey.RecordCrewTime(S1, StopEventKind.Departure, At(6, 2));
        journey.RecordCrewTime(S2, StopEventKind.Arrival, At(6, 44));   // 4 min late

        var detail = JourneyViewBuilder.Detail(journey, Names, Service, trainSet: null);

        Assert.Equal([1, 2, 3], detail.Stops.Select(s => s.Sequence));
        Assert.Equal(["Colombo Fort", "Rambukkana", "Kandy"], detail.Stops.Select(s => s.Name));
        var middle = detail.Stops[1];
        Assert.Equal(("Pass", "Arrived"), (middle.CallType, middle.Status));
        Assert.Equal((At(6, 40), At(6, 44), At(6, 44)), (middle.ScheduledArrival, middle.ExpectedArrival, middle.ActualArrival));
        Assert.Equal((At(6, 40), At(6, 44), null), (middle.ScheduledDeparture, middle.ExpectedDeparture, middle.ActualDeparture));
        var last = detail.Stops[2];
        Assert.Equal((At(7, 30), At(7, 34), null), (last.ScheduledArrival, last.ExpectedArrival, last.ActualArrival));
        Assert.Null(last.ScheduledDeparture);
        Assert.All(detail.Stops, s => { Assert.False(s.IsAdded); Assert.Null(s.PreviousPlatform); Assert.Equal(s.PlannedPlatform, s.Platform); });
        Assert.Equal("Kandy Express", detail.Name);
        Assert.Equal(new JourneyFromResponse(S1, "Colombo Fort", At(6, 0), At(6, 2), "3", null), detail.From);
    }

    [Fact]
    public void The_detail_names_the_train_set_and_the_last_known_position()
    {
        var journey = NewJourney();
        var trainSet = new TrainSetDetails("DPS-01", "DieselPowerSet", [new CoachDetails(1, "First", "Reserved"), new CoachDetails(2, "Third", "Unreserved")]);

        var before = JourneyViewBuilder.Detail(journey, Names, Service, trainSet);
        Assert.Null(before.Freshness.LastPosition);
        Assert.Equal(["First", "Third"], before.TrainSet!.Coaches.Select(c => c.Class));
        Assert.Equal(("DPS-01", "DieselPowerSet"), (before.TrainSet.Code, before.TrainSet.Traction));

        journey.RecordPosition(new PositionSample(new GeoPoint(7.29, 80.63), 40, At(6, 10), At(6, 10), TimeSpan.Zero, 0, new Dictionary<Guid, double>()), At(6, 10));
        var after = JourneyViewBuilder.Detail(journey, Names, Service, trainSet);
        Assert.Equal(new JourneyDetailFreshnessResponse("Live", At(6, 10), new PositionResponse(7.29, 80.63)), after.Freshness);
    }

    // T-07: whatever the staff-side data holds, the public JSON has no source and no staff or crew fields.
    [Fact]
    public void The_public_json_never_contains_source_or_staff_fields()
    {
        var journey = NewJourney();
        journey.RecordCrewTime(S2, StopEventKind.Arrival, At(6, 44));   // a crew tap: the source of that time is Crew

        var json = JsonSerializer.Serialize(
            JourneyViewBuilder.Detail(journey, Names, Service, null), new JsonSerializerOptions(JsonSerializerDefaults.Web));

        foreach (var forbidden in new[] { "source", "crew", "actor", "deviceId", "driver" })
        {
            Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase);
        }
    }
}
