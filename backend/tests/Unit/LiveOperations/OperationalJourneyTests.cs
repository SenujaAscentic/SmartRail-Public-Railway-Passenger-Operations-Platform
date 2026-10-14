using SmartRail.LiveOperations.Domain;
using Xunit;

namespace SmartRail.UnitTests.LiveOperations;

public class OperationalJourneyTests
{
    private static readonly TimeZoneInfo Colombo = TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo");   // UTC+05:30, no daylight saving
    private static readonly Guid Station1 = Guid.NewGuid();
    private static readonly Guid Station2 = Guid.NewGuid();

    private static OperationalJourney Plan(DateOnly date, params PlannedStop[] stops) =>
        OperationalJourney.Plan(Guid.NewGuid(), Guid.NewGuid(), date, Guid.NewGuid(), stops, Colombo);

    [Fact]
    public void Scheduled_times_are_service_date_plus_offset_in_utc()
    {
        // 06:30 local on 14 Oct is 01:00 UTC; 09:15 local is 03:45 UTC.
        var journey = Plan(new DateOnly(2026, 10, 14),
            new PlannedStop(1, Station1, CallType.Stop, null, 390, "5"),
            new PlannedStop(2, Station2, CallType.Stop, 555, null, "2"));

        Assert.Null(journey.Stops[0].ScheduledArrival);
        Assert.Equal(new DateTime(2026, 10, 14, 1, 0, 0, DateTimeKind.Utc), journey.Stops[0].ScheduledDeparture);
        Assert.Equal(new DateTime(2026, 10, 14, 3, 45, 0, DateTimeKind.Utc), journey.Stops[1].ScheduledArrival);
        Assert.Null(journey.Stops[1].ScheduledDeparture);
    }

    // E-12: a run that crosses midnight stays one journey on its service date; its later stops simply have offsets over 1440.
    [Fact]
    public void Offsets_after_midnight_stay_in_the_journey_of_the_service_date()
    {
        // 25:00 after midnight of 14 Oct is 01:00 local on 15 Oct, which is 19:30 UTC on 14 Oct.
        var journey = Plan(new DateOnly(2026, 10, 14),
            new PlannedStop(1, Station1, CallType.Stop, null, 1380, "1"),
            new PlannedStop(2, Station2, CallType.Stop, 1500, null, "1"));

        Assert.Equal(new DateOnly(2026, 10, 14), journey.ServiceDate);
        Assert.Equal(new DateTime(2026, 10, 14, 19, 30, 0, DateTimeKind.Utc), journey.Stops[1].ScheduledArrival);
    }

    [Fact]
    public void A_planned_journey_keeps_the_service_train_set_stops_and_platforms()
    {
        var serviceId = Guid.NewGuid();
        var trainSetId = Guid.NewGuid();
        var journey = OperationalJourney.Plan(Guid.NewGuid(), serviceId, new DateOnly(2026, 10, 14), trainSetId,
            [new PlannedStop(1, Station1, CallType.Stop, null, 390, "5"), new PlannedStop(2, Station2, CallType.Pass, 400, 400, "2")],
            Colombo);

        Assert.Equal(JourneyStatus.Planned, journey.Status);
        Assert.Equal(serviceId, journey.ServiceId);
        Assert.Equal(trainSetId, journey.TrainSetId);
        Assert.Equal([1, 2], journey.Stops.Select(s => s.Sequence));
        Assert.Equal([Station1, Station2], journey.Stops.Select(s => s.StationId));
        Assert.Equal(["5", "2"], journey.Stops.Select(s => s.PlannedPlatform));
        Assert.Equal(CallType.Pass, journey.Stops[1].CallType);
    }
}
