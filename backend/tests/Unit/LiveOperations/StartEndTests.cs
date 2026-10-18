using SmartRail.LiveOperations.Domain;
using Xunit;

namespace SmartRail.UnitTests.LiveOperations;

// Start and End of a journey by the driver on the cab tablet (US-C2; domain model 4.2).
public class StartEndTests
{
    private static readonly Guid S1 = Guid.NewGuid(), S2 = Guid.NewGuid();
    private static readonly Guid Device = Guid.NewGuid(), Session = Guid.NewGuid();
    private static DateTime At(int h, int m, int s = 0) => new(2026, 10, 14, h, m, s, DateTimeKind.Utc);

    // Departs 06:00, so the earliest start is 05:00.
    private static OperationalJourney NewJourney() => OperationalJourney.Plan(
        Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 14), Guid.NewGuid(),
        [new PlannedStop(1, S1, CallType.Stop, null, 360, "1"), new PlannedStop(2, S2, CallType.Stop, 400, null, "2")],
        TimeZoneInfo.Utc, Guid.NewGuid());

    [Fact]
    public void Start_is_refused_more_than_60_minutes_before_departure()
    {
        var journey = NewJourney();

        var result = journey.Start(At(4, 59, 59), Device, Session);

        Assert.Equal(StartResult.TooEarly, result);
        Assert.Equal(JourneyStatus.Planned, journey.Status);
        Assert.Empty(journey.DomainEvents);
    }

    [Fact]
    public void Start_is_allowed_exactly_60_minutes_before_departure()
    {
        var journey = NewJourney();

        var result = journey.Start(At(5, 0), Device, Session);

        Assert.Equal(StartResult.Started, result);
        Assert.Equal(JourneyStatus.Running, journey.Status);
        Assert.Equal([new JourneyStarted(journey.Id, At(5, 0), Device, Session)], journey.DomainEvents);
    }

    [Fact]
    public void Start_is_allowed_after_the_scheduled_departure_a_late_start_is_still_a_start()
    {
        var journey = NewJourney();

        Assert.Equal(StartResult.Started, journey.Start(At(6, 20), Device, Session));
    }

    [Fact]
    public void Starting_is_not_a_controller_decision_and_tracking_waits_for_the_first_position()
    {
        var journey = NewJourney();

        journey.Start(At(5, 30), Device, Session);

        Assert.Equal(0, journey.ControlVersion);                       // R-18: only decisions bump it
        Assert.Equal(TrackingStatus.NotStarted, journey.Tracking);     // domain model 4.4
    }

    [Fact]
    public void A_journey_that_is_running_or_completed_cannot_be_started_again()
    {
        var journey = NewJourney();
        journey.Start(At(5, 30), Device, Session);
        Assert.Equal(StartResult.WrongState, journey.Start(At(5, 31), Device, Session));

        journey.End(At(7, 0));
        Assert.Equal(StartResult.WrongState, journey.Start(At(7, 1), Device, Session));
    }

    [Fact]
    public void End_completes_a_running_journey_and_ends_the_tracking()
    {
        var journey = NewJourney();
        journey.Start(At(5, 30), Device, Session);
        journey.RecordPosition(new PositionSample(new GeoPoint(7, 80), 60, At(6, 30), At(6, 30), TimeSpan.Zero, 0, new Dictionary<Guid, double>()), At(6, 30));
        journey.ClearDomainEvents();

        var result = journey.End(At(6, 45));

        Assert.Equal(EndResult.Ended, result);
        Assert.Equal(JourneyStatus.Completed, journey.Status);
        Assert.Equal(TrackingStatus.Ended, journey.Tracking);
        Assert.Equal(
            [
                new JourneyEnded(journey.Id, At(6, 45), JourneyEndReason.Driver),
                new DataFreshnessChanged(journey.Id, TrackingStatus.Live, TrackingStatus.Ended, At(6, 30), At(6, 45)),
            ],
            journey.DomainEvents);
    }

    [Fact]
    public void An_ended_journey_is_no_longer_aged_by_the_timer()
    {
        var journey = NewJourney();
        journey.Start(At(5, 30), Device, Session);
        journey.RecordPosition(new PositionSample(new GeoPoint(7, 80), 60, At(6, 30), At(6, 30), TimeSpan.Zero, 0, new Dictionary<Guid, double>()), At(6, 30));
        journey.End(At(6, 45));
        journey.ClearDomainEvents();

        journey.RefreshTracking(At(9, 0));

        Assert.Equal(TrackingStatus.Ended, journey.Tracking);
        Assert.Empty(journey.DomainEvents);
    }

    [Fact]
    public void End_is_refused_for_a_journey_that_is_not_running()
    {
        var planned = NewJourney();
        Assert.Equal(EndResult.WrongState, planned.End(At(6, 0)));
        Assert.Equal(JourneyStatus.Planned, planned.Status);

        var completed = NewJourney();
        completed.Start(At(5, 30), Device, Session);
        completed.End(At(6, 30));
        Assert.Equal(EndResult.WrongState, completed.End(At(6, 31)));
    }
}
