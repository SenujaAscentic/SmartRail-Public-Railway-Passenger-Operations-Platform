using SmartRail.LiveOperations.Domain;
using Xunit;

namespace SmartRail.UnitTests.LiveOperations;

// R-01 to R-06, R-08 and R-09 wired into the aggregate: what the journey records, which delay it shows,
// and which domain events it collects.
public class OperationalJourneyLiveTests
{
    private static readonly Guid S1 = Guid.NewGuid();   // origin, departs 06:00
    private static readonly Guid S2 = Guid.NewGuid();   // arrives 06:40, departs 06:42
    private static readonly Guid S3 = Guid.NewGuid();   // terminus, arrives 07:30

    private static DateTime At(int hour, int minute, int second = 0) =>
        new(2026, 10, 14, hour, minute, second, DateTimeKind.Utc);

    private static OperationalJourney NewJourney() =>
        OperationalJourney.Plan(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 14), Guid.NewGuid(),
            [
                new PlannedStop(1, S1, CallType.Stop, null, 360, "1"),
                new PlannedStop(2, S2, CallType.Stop, 400, 402, "2"),
                new PlannedStop(3, S3, CallType.Stop, 450, null, "3"),
            ],
            TimeZoneInfo.Utc);

    private static JourneyStop Stop(OperationalJourney journey, Guid station) =>
        journey.Stops.Single(s => s.StationId == station);

    // The train is where the time says; GPS time = device time, no offset. Distances only for the one station of interest.
    private static PositionSample Near(DateTime time, double speedKmh, Guid station, double metres) =>
        new(new GeoPoint(7.0, 80.0), speedKmh, time, time, TimeSpan.Zero, 0,
            new Dictionary<Guid, double> { [station] = metres });

    private static PositionSample Far(DateTime time, double speedKmh = 60) =>
        new(new GeoPoint(7.0, 80.0), speedKmh, time, time, TimeSpan.Zero, 0, new Dictionary<Guid, double>());

    // now = the position's own time, so nothing here depends on a clock.
    private static PositionVerdict Feed(OperationalJourney journey, PositionSample sample) =>
        journey.RecordPosition(sample, sample.GpsTime ?? sample.DeviceTime);

    // Brings the train to a stop in S2's zone at 06:44:00 (4 min late) and returns once Arrived is detected.
    private static void GpsArrivesAtS2(OperationalJourney journey)
    {
        Feed(journey, Near(At(6, 43, 50), 40, S2, 400));
        Feed(journey, Near(At(6, 44, 0), 3, S2, 100));
        Feed(journey, Near(At(6, 44, 20), 0, S2, 50));
    }

    // ---- GPS detection (R-04) into stop times, status, delay and events -------------------------------------

    [Fact]
    public void Gps_arrival_records_the_stop_moment_marks_arrived_and_sets_the_delay()
    {
        var journey = NewJourney();

        GpsArrivesAtS2(journey);

        var stop = Stop(journey, S2);
        Assert.Equal(StopStatus.Arrived, stop.Status);
        Assert.Equal(new TimeWithSource(At(6, 44), Source.Gps), stop.ActualArrivalTime);   // when it stopped, not 06:44:20
        Assert.Equal(new SourcedDelay(new Delay(4), Source.Gps), journey.CurrentDelay);
        Assert.Contains(new StopStatusChanged(journey.Id, S2, 2, StopStatus.Scheduled, StopStatus.Arrived, At(6, 44), Source.Gps), journey.DomainEvents);
        Assert.Contains(new DelayChanged(journey.Id, 4, 0, Source.Gps, At(6, 44)), journey.DomainEvents);
    }

    [Fact]
    public void Gps_departure_records_the_time_and_updates_the_delay()
    {
        var journey = NewJourney();
        GpsArrivesAtS2(journey);

        Feed(journey, Near(At(6, 50), 30, S2, 400));   // leaves the zone, scheduled departure was 06:42

        var stop = Stop(journey, S2);
        Assert.Equal(StopStatus.Departed, stop.Status);
        Assert.Equal(new TimeWithSource(At(6, 50), Source.Gps), stop.ActualDepartureTime);
        Assert.Equal(8, journey.DelayMinutes);
        Assert.Contains(new StopStatusChanged(journey.Id, S2, 2, StopStatus.Arrived, StopStatus.Departed, At(6, 50), Source.Gps), journey.DomainEvents);
        Assert.Contains(new DelayChanged(journey.Id, 8, 4, Source.Gps, At(6, 50)), journey.DomainEvents);
    }

    [Fact]
    public void Passed_stop_records_gps_passing_time_and_updates_delay()
    {
        var journey = NewJourney();

        Feed(journey, Near(At(6, 41), 80, S2, 100));
        Feed(journey, Near(At(6, 41, 20), 80, S2, 500));   // scheduled 06:40, passed 06:41:20: 1 min late

        var stop = Stop(journey, S2);
        Assert.Equal(StopStatus.Passed, stop.Status);
        var passedAt = new TimeWithSource(At(6, 41, 20), Source.Gps);
        Assert.Equal(passedAt, stop.ActualArrivalTime);     // a pass has one time; it is stored as both
        Assert.Equal(passedAt, stop.ActualDepartureTime);
        Assert.Equal(new SourcedDelay(new Delay(1), Source.Gps), journey.CurrentDelay);
        Assert.Contains(new StopStatusChanged(journey.Id, S2, 2, StopStatus.Scheduled, StopStatus.Passed, At(6, 41, 20), Source.Gps), journey.DomainEvents);
        Assert.Contains(new DelayChanged(journey.Id, 1, 0, Source.Gps, At(6, 41, 20)), journey.DomainEvents);
    }

    // Express trains: the seed gives a Pass stop one time (arrival = departure) for exactly this.
    [Fact]
    public void Passed_stop_without_a_scheduled_arrival_uses_the_scheduled_departure()
    {
        var journey = OperationalJourney.Plan(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 14), Guid.NewGuid(),
            [new PlannedStop(1, S2, CallType.Pass, null, 400, "1")], TimeZoneInfo.Utc);

        Feed(journey, Near(At(6, 41), 80, S2, 100));
        Feed(journey, Near(At(6, 42, 30), 80, S2, 500));

        Assert.Equal(2, journey.DelayMinutes);
    }

    [Fact]
    public void Gps_pass_does_not_overwrite_a_controller_delay()
    {
        var journey = NewJourney();
        journey.SetControllerDelay(new Delay(10), At(6, 30));

        Feed(journey, Near(At(6, 41), 80, S2, 100));
        Feed(journey, Near(At(6, 41, 20), 80, S2, 500));

        Assert.Equal(new SourcedDelay(new Delay(10), Source.Controller), journey.CurrentDelay);
        Assert.Equal(StopStatus.Passed, Stop(journey, S2).Status);
    }

    [Fact]
    public void Later_stops_expected_times_follow_the_new_delay()
    {
        var journey = NewJourney();
        Assert.Equal(At(7, 30), Stop(journey, S3).ExpectedArrival(journey.CurrentDelay.Value));

        GpsArrivesAtS2(journey);

        Assert.Equal(At(7, 34), Stop(journey, S3).ExpectedArrival(journey.CurrentDelay.Value));   // R-02
        Assert.Equal(At(6, 44), Stop(journey, S2).ExpectedArrival(journey.CurrentDelay.Value));   // already happened
    }

    [Fact]
    public void Expected_departure_of_a_stop_ahead_is_never_before_schedule_when_running_early()
    {
        var journey = NewJourney();
        journey.RecordCrewTime(S2, StopEventKind.Arrival, At(6, 38));   // 2 min early

        Assert.Equal(-2, journey.DelayMinutes);
        Assert.Equal(At(6, 42), Stop(journey, S2).ExpectedDeparture(journey.CurrentDelay.Value));   // R-03
        Assert.Equal(At(7, 28), Stop(journey, S3).ExpectedArrival(journey.CurrentDelay.Value));     // R-02, early carries on
    }

    // ---- Crew taps and source priority (R-05) -------------------------------------------------------------

    [Fact]
    public void Gps_arrival_does_not_overwrite_crew_tapped_arrival_on_the_journey()
    {
        var journey = NewJourney();
        journey.RecordCrewTime(S2, StopEventKind.Arrival, At(6, 43));

        GpsArrivesAtS2(journey);   // GPS thinks 06:44

        Assert.Equal(new TimeWithSource(At(6, 43), Source.Crew), Stop(journey, S2).ActualArrivalTime);
        Assert.Equal(new SourcedDelay(new Delay(3), Source.Crew), journey.CurrentDelay);
        Assert.Single(journey.DomainEvents.OfType<StopStatusChanged>(), e => e.StationId == S2);   // already Arrived, so no second event
    }

    [Fact]
    public void Crew_tap_after_gps_replaces_the_gps_time_and_delay()
    {
        var journey = NewJourney();
        GpsArrivesAtS2(journey);

        journey.RecordCrewTime(S2, StopEventKind.Arrival, At(6, 43));

        Assert.Equal(new TimeWithSource(At(6, 43), Source.Crew), Stop(journey, S2).ActualArrivalTime);
        Assert.Equal(new SourcedDelay(new Delay(3), Source.Crew), journey.CurrentDelay);
        Assert.Contains(new DelayChanged(journey.Id, 3, 4, Source.Crew, At(6, 43)), journey.DomainEvents);
    }

    [Fact]
    public void Crew_departure_tap_without_an_arrival_marks_the_stop_departed()
    {
        var journey = NewJourney();

        journey.RecordCrewTime(S2, StopEventKind.Departure, At(6, 45));

        Assert.Equal(StopStatus.Departed, Stop(journey, S2).Status);
        Assert.Equal(3, journey.DelayMinutes);
    }

    [Fact]
    public void Crew_tap_for_a_station_not_on_the_journey_is_refused()
    {
        var journey = NewJourney();

        Assert.Throws<ArgumentException>(() => journey.RecordCrewTime(Guid.NewGuid(), StopEventKind.Arrival, At(6, 43)));
    }

    // ---- Controller delay (R-05) ----------------------------------------------------------------------------

    [Fact]
    public void Controller_delay_is_kept_when_gps_and_crew_report_but_their_stop_times_are_still_recorded()
    {
        var journey = NewJourney();
        journey.SetControllerDelay(new Delay(10), At(6, 30));

        GpsArrivesAtS2(journey);
        journey.RecordCrewTime(S3, StopEventKind.Arrival, At(7, 31));

        Assert.Equal(new SourcedDelay(new Delay(10), Source.Controller), journey.CurrentDelay);
        Assert.Equal(new TimeWithSource(At(6, 44), Source.Gps), Stop(journey, S2).ActualArrivalTime);
        Assert.Equal(new TimeWithSource(At(7, 31), Source.Crew), Stop(journey, S3).ActualArrivalTime);
        Assert.Single(journey.DomainEvents.OfType<DelayChanged>());   // only the controller's own change
    }

    // Decision 2: clearing recalculates from the most recent recorded stop time. It must not show a false "On time".
    [Fact]
    public void Clearing_the_controller_delay_recalculates_from_the_latest_recorded_stop_time()
    {
        var journey = NewJourney();
        GpsArrivesAtS2(journey);                                   // 4 min late (GPS)
        journey.SetControllerDelay(new Delay(10), At(6, 46));
        journey.ClearDomainEvents();

        journey.ClearControllerDelay(At(6, 47));

        Assert.Equal(new SourcedDelay(new Delay(4), Source.Gps), journey.CurrentDelay);   // not 0
        Assert.Equal([new DelayChanged(journey.Id, 4, 10, Source.Gps, At(6, 47))], journey.DomainEvents);
    }

    [Fact]
    public void Clearing_the_controller_delay_uses_the_most_recent_stop_event_not_the_first()
    {
        var journey = NewJourney();
        GpsArrivesAtS2(journey);                                                   // arrival 4 min late
        journey.RecordCrewTime(S2, StopEventKind.Departure, At(6, 50));            // departure 8 min late, the latest event
        journey.SetControllerDelay(new Delay(10), At(6, 51));

        journey.ClearControllerDelay(At(6, 52));

        Assert.Equal(new SourcedDelay(new Delay(8), Source.Crew), journey.CurrentDelay);
    }

    [Fact]
    public void Clearing_the_controller_delay_gives_zero_from_schedule_only_when_no_stop_event_exists()
    {
        var journey = NewJourney();
        journey.SetControllerDelay(new Delay(10), At(6, 0));

        journey.ClearControllerDelay(At(6, 1));

        Assert.Equal(SourcedDelay.FromSchedule, journey.CurrentDelay);
        Assert.Contains(new DelayChanged(journey.Id, 0, 10, Source.Schedule, At(6, 1)), journey.DomainEvents);
    }

    [Fact]
    public void Clearing_does_nothing_when_the_delay_is_not_a_controller_delay()
    {
        var journey = NewJourney();
        GpsArrivesAtS2(journey);
        journey.ClearDomainEvents();

        journey.ClearControllerDelay(At(6, 47));

        Assert.Equal(new SourcedDelay(new Delay(4), Source.Gps), journey.CurrentDelay);
        Assert.Empty(journey.DomainEvents);
    }

    // ---- Plausibility and clock correction (R-08, R-09) ------------------------------------------------------

    [Fact]
    public void Off_route_position_is_discarded_and_changes_nothing()
    {
        var journey = NewJourney();
        Feed(journey, Far(At(6, 0)));
        journey.ClearDomainEvents();

        var offRoute = Far(At(6, 1)) with { DistanceFromRouteMetres = 600 };
        var verdict = Feed(journey, offRoute);

        Assert.Equal(PositionVerdict.OffRoute, verdict);
        Assert.Equal(At(6, 0), journey.LastPositionAt);
        Assert.Empty(journey.DomainEvents);
    }

    [Fact]
    public void Position_older_than_the_last_accepted_is_discarded_by_the_journey()
    {
        var journey = NewJourney();
        Feed(journey, Far(At(6, 5)));

        var verdict = Feed(journey, Near(At(6, 4), 0, S2, 10));

        Assert.Equal(PositionVerdict.OlderThanLastAccepted, verdict);
        Assert.Equal(At(6, 5), journey.LastPositionAt);
    }

    [Fact]
    public void Position_implying_more_than_120_kmh_is_discarded_by_the_journey()
    {
        var journey = NewJourney();
        Feed(journey, Far(At(6, 0)));

        var jumped = Far(At(6, 0, 30)) with { Location = new GeoPoint(7.01, 80.0) };   // 1112 m in 30 s

        Assert.Equal(PositionVerdict.ImpliedSpeedTooHigh, Feed(journey, jumped));
    }

    [Fact]
    public void Stop_times_use_the_corrected_device_clock_when_there_is_no_gps_time()
    {
        var journey = NewJourney();
        // No GPS time. The tablet runs 7 min fast: its 06:51:00 is really 06:44:00, offset (server - device) = -7 min.
        PositionSample NoGps(DateTime deviceTime, double speed, double metres) =>
            new(new GeoPoint(7.0, 80.0), speed, null, deviceTime, TimeSpan.FromMinutes(-7), 0,
                new Dictionary<Guid, double> { [S2] = metres });

        journey.RecordPosition(NoGps(At(6, 51, 0), 3, 100), At(6, 44, 0));
        journey.RecordPosition(NoGps(At(6, 51, 20), 0, 50), At(6, 44, 20));

        Assert.Equal(new TimeWithSource(At(6, 44), Source.Gps), Stop(journey, S2).ActualArrivalTime);
    }

    // ---- Freshness (R-06) -----------------------------------------------------------------------------------

    [Fact]
    public void First_accepted_position_makes_tracking_live()
    {
        var journey = NewJourney();
        Assert.Equal(TrackingStatus.NotStarted, journey.Tracking);

        Feed(journey, Far(At(6, 0)));

        Assert.Equal(TrackingStatus.Live, journey.Tracking);
        Assert.Contains(new DataFreshnessChanged(journey.Id, TrackingStatus.NotStarted, TrackingStatus.Live, At(6, 0), At(6, 0)), journey.DomainEvents);
    }

    [Fact]
    public void Tracking_becomes_stale_then_lost_and_returns_to_live_on_the_next_position()
    {
        var journey = NewJourney();
        Feed(journey, Far(At(6, 0)));
        journey.ClearDomainEvents();

        journey.RefreshTracking(At(6, 1, 59));
        Assert.Equal(TrackingStatus.Live, journey.Tracking);

        journey.RefreshTracking(At(6, 2));
        Assert.Equal(TrackingStatus.Stale, journey.Tracking);

        journey.RefreshTracking(At(6, 10));
        Assert.Equal(TrackingStatus.Lost, journey.Tracking);

        Feed(journey, Far(At(6, 11)));
        Assert.Equal(TrackingStatus.Live, journey.Tracking);

        Assert.Equal(
            [
                new DataFreshnessChanged(journey.Id, TrackingStatus.Live, TrackingStatus.Stale, At(6, 0), At(6, 2)),
                new DataFreshnessChanged(journey.Id, TrackingStatus.Stale, TrackingStatus.Lost, At(6, 0), At(6, 10)),
                new DataFreshnessChanged(journey.Id, TrackingStatus.Lost, TrackingStatus.Live, At(6, 11), At(6, 11)),
            ],
            journey.DomainEvents);
    }

    [Fact]
    public void Refreshing_a_journey_that_has_no_position_yet_changes_nothing()
    {
        var journey = NewJourney();

        journey.RefreshTracking(At(9, 0));

        Assert.Equal(TrackingStatus.NotStarted, journey.Tracking);
        Assert.Empty(journey.DomainEvents);
    }

    // Stale or lost tracking does not touch the delay: the ETA stays at the last delay (domain model 4.4).
    [Fact]
    public void Lost_tracking_keeps_the_last_delay()
    {
        var journey = NewJourney();
        GpsArrivesAtS2(journey);

        journey.RefreshTracking(At(7, 0));

        Assert.Equal(TrackingStatus.Lost, journey.Tracking);
        Assert.Equal(4, journey.DelayMinutes);
    }

    // ---- Events -----------------------------------------------------------------------------------------------

    [Fact]
    public void Clearing_domain_events_empties_the_list_but_keeps_the_state()
    {
        var journey = NewJourney();
        GpsArrivesAtS2(journey);
        Assert.NotEmpty(journey.DomainEvents);

        journey.ClearDomainEvents();

        Assert.Empty(journey.DomainEvents);
        Assert.Equal(4, journey.DelayMinutes);
    }
}
