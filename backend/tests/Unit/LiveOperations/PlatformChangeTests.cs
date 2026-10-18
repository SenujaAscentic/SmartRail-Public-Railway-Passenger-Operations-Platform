using SmartRail.LiveOperations.Domain;
using Xunit;

namespace SmartRail.UnitTests.LiveOperations;

// R-11, R-12, R-13 and US-O5 (a star story): a platform change with its lead time, the warning below 5 minutes with an optional hold,
// the "train will wait" promise only when a hold is really set, and the conflict warning. Written before the code (ADR-08).
public class PlatformChangeTests
{
    private static readonly Guid S1 = Guid.NewGuid();   // origin, departs 06:00 (platform 1)
    private static readonly Guid S2 = Guid.NewGuid();   // arrives 06:40, departs 06:42 (platform 2)
    private static readonly Guid S3 = Guid.NewGuid();   // terminus, arrives 07:30 (platform 3)
    private static readonly Guid Ruwan = Guid.NewGuid();
    private static readonly string[] Platforms = ["1", "2", "3", "4", "5"];

    private static DateTime At(int hour, int minute, int second = 0) => new(2026, 10, 14, hour, minute, second, DateTimeKind.Utc);

    private static OperationalJourney NewJourney() =>
        OperationalJourney.Plan(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 14), Guid.NewGuid(),
            [
                new PlannedStop(1, S1, CallType.Stop, null, 360, "1"),
                new PlannedStop(2, S2, CallType.Stop, 400, 402, "2"),
                new PlannedStop(3, S3, CallType.Stop, 450, null, "3"),
            ],
            TimeZoneInfo.Utc, Guid.NewGuid());

    private static JourneyStop Stop(OperationalJourney journey, Guid station) => journey.Stops.Single(s => s.StationId == station);

    // ---- lead time (R-11) ----

    [Fact]
    public void Lead_time_is_measured_from_expected_arrival_except_at_origin()
    {
        // S2: arrival 06:40 (departure 06:42). Asked at 06:30, the lead time is 10 minutes, not 12.
        Assert.Equal(TimeSpan.FromMinutes(10), PlatformRules.LeadTime(At(6, 40), At(6, 42), At(6, 30)).LeadTime);
        // The origin has no arrival: its departure is used.
        Assert.Equal(TimeSpan.FromMinutes(20), PlatformRules.LeadTime(null, At(6, 0), At(5, 40)).LeadTime);
    }

    [Fact]
    public void Lead_time_over_15_minutes_is_normal()
    {
        Assert.Equal(LeadTimeLevel.Normal, PlatformRules.LeadTime(At(6, 40), null, At(6, 24, 59)).Level);
        Assert.Equal(LeadTimeLevel.Normal, PlatformRules.LeadTime(At(6, 40), null, At(5, 0)).Level);
    }

    [Fact]
    public void Lead_time_between_5_and_15_minutes_is_short_notice()
    {
        Assert.Equal(LeadTimeLevel.ShortNotice, PlatformRules.LeadTime(At(6, 40), null, At(6, 25)).Level);   // exactly 15
        Assert.Equal(LeadTimeLevel.ShortNotice, PlatformRules.LeadTime(At(6, 40), null, At(6, 30)).Level);
        Assert.Equal(LeadTimeLevel.ShortNotice, PlatformRules.LeadTime(At(6, 40), null, At(6, 35)).Level);   // exactly 5
    }

    [Fact]
    public void Lead_time_under_5_minutes_recommends_a_hold_and_does_not_block()
    {
        var late = PlatformRules.LeadTime(At(6, 40), null, At(6, 35, 1));

        Assert.Equal(LeadTimeLevel.Late, late.Level);
        Assert.True(late.HoldRecommended);
        Assert.False(PlatformRules.LeadTime(At(6, 40), null, At(6, 30)).HoldRecommended);

        // And the change itself goes through without a hold (US-O5 AC5, approved 18 Oct).
        var journey = NewJourney();
        var outcome = journey.ChangePlatformByController(0, S2, "5", Platforms, holdMinutes: null, Ruwan, At(6, 38));
        Assert.Equal(DecisionResult.Done, outcome.Result);
        Assert.Equal(LeadTimeLevel.Late, outcome.LeadTime!.Level);
    }

    [Fact]
    public void Lead_time_is_negative_once_the_train_is_due_or_already_here()
    {
        Assert.Equal(LeadTimeLevel.Late, PlatformRules.LeadTime(At(6, 40), null, At(6, 45)).Level);
        Assert.Equal(-5, PlatformRules.LeadTime(At(6, 40), null, At(6, 45)).Minutes);
    }

    // ---- "the train will wait" only with a hold (US-O5 AC5, a star criterion) ----

    [Fact]
    public void Train_will_wait_message_only_when_hold_is_set()
    {
        var withoutHold = NewJourney();
        withoutHold.ChangePlatformByController(0, S2, "5", Platforms, holdMinutes: null, Ruwan, At(6, 38));
        var changed = withoutHold.DomainEvents.OfType<PlatformChanged>().Single();
        Assert.False(changed.HoldSet);                                      // < 5 minutes, no hold: no promise
        Assert.Null(withoutHold.Hold);
        Assert.DoesNotContain(withoutHold.DomainEvents, e => e is JourneyHeld);

        var withHold = NewJourney();
        var outcome = withHold.ChangePlatformByController(0, S2, "5", Platforms, holdMinutes: 8, Ruwan, At(6, 38));
        Assert.True(outcome.HoldSet);
        Assert.True(withHold.DomainEvents.OfType<PlatformChanged>().Single().HoldSet);
        Assert.Equal(S2, withHold.Hold!.StationId);
        Assert.Equal(8, withHold.Hold.Minutes);
        Assert.Contains(withHold.DomainEvents, e => e is JourneyHeld { Released: false, Minutes: 8 });
        Assert.Equal(1, withHold.ControlVersion);   // one decision, one version
    }

    // ---- the change itself ----

    [Fact]
    public void A_platform_change_records_the_new_and_the_previous_platform_and_the_lead_time_level()
    {
        var journey = NewJourney();

        var outcome = journey.ChangePlatformByController(0, S2, "4", Platforms, holdMinutes: null, Ruwan, At(6, 20));

        Assert.Equal(DecisionResult.Done, outcome.Result);
        Assert.Equal(("4", "2"), (Stop(journey, S2).Platform, Stop(journey, S2).PreviousPlatform));
        Assert.Equal("2", Stop(journey, S2).PlannedPlatform);   // the plan itself does not change
        Assert.Equal(LeadTimeLevel.Normal, outcome.LeadTime!.Level);
        Assert.Contains(new PlatformChanged(journey.Id, S2, "4", "2", LeadTimeLevel.Normal, 20, HoldSet: false, At(6, 20)), journey.DomainEvents);
        Assert.Equal(1, journey.ControlVersion);
    }

    [Fact]
    public void Changing_back_keeps_the_platform_before_the_last_change()
    {
        var journey = NewJourney();
        journey.ChangePlatformByController(0, S2, "4", Platforms, null, Ruwan, At(6, 0));

        journey.ChangePlatformByController(1, S2, "5", Platforms, null, Ruwan, At(6, 5));

        Assert.Equal(("5", "4"), (Stop(journey, S2).Platform, Stop(journey, S2).PreviousPlatform));
    }

    [Fact]
    public void The_lead_time_uses_the_expected_arrival_including_the_current_delay()
    {
        var journey = NewJourney();
        journey.SetDelayByController(0, new Delay(10), new JourneyReason("Weather", null), Ruwan, At(6, 0));   // expected arrival at S2: 06:50

        var outcome = journey.ChangePlatformByController(1, S2, "4", Platforms, null, Ruwan, At(6, 40));

        Assert.Equal(10, outcome.LeadTime!.Minutes);
        Assert.Equal(LeadTimeLevel.ShortNotice, outcome.LeadTime.Level);
    }

    [Fact]
    public void Platform_change_refused_for_departed_stop()
    {
        var journey = NewJourney();
        journey.RecordCrewTime(S2, StopEventKind.Departure, At(6, 43));   // S1 and S2 are behind the train

        Assert.Equal(DecisionResult.InvalidState, journey.ChangePlatformByController(0, S2, "4", Platforms, null, Ruwan, At(6, 44)).Result);
        Assert.Equal(DecisionResult.InvalidState, journey.ChangePlatformByController(0, S1, "4", Platforms, null, Ruwan, At(6, 44)).Result);
        Assert.Equal(0, journey.ControlVersion);
    }

    [Fact]
    public void A_platform_change_is_allowed_for_a_stop_the_train_is_standing_at()
    {
        var journey = NewJourney();
        journey.RecordCrewTime(S2, StopEventKind.Arrival, At(6, 40));

        Assert.Equal(DecisionResult.Done, journey.ChangePlatformByController(0, S2, "4", Platforms, null, Ruwan, At(6, 41)).Result);
    }

    [Fact]
    public void Platform_change_refused_for_platform_not_at_station()
    {
        var journey = NewJourney();

        var outcome = journey.ChangePlatformByController(0, S2, "9", Platforms, null, Ruwan, At(6, 20));

        Assert.Equal(DecisionResult.PlatformUnavailable, outcome.Result);
        Assert.Equal("2", Stop(journey, S2).Platform);
        Assert.Equal(0, journey.ControlVersion);
    }

    [Fact]
    public void A_change_to_the_platform_the_stop_already_has_is_refused()
    {
        var journey = NewJourney();

        Assert.Equal(DecisionResult.SamePlatform, journey.ChangePlatformByController(0, S2, "2", Platforms, null, Ruwan, At(6, 20)).Result);
    }

    [Fact]
    public void A_platform_change_for_a_station_that_is_not_a_stop_is_refused()
    {
        var journey = NewJourney();

        Assert.Equal(DecisionResult.UnknownStop, journey.ChangePlatformByController(0, Guid.NewGuid(), "4", Platforms, null, Ruwan, At(6, 20)).Result);
    }

    [Fact]
    public void A_platform_change_on_a_stale_version_is_refused_and_changes_nothing()
    {
        var journey = NewJourney();

        var outcome = journey.ChangePlatformByController(2, S2, "4", Platforms, holdMinutes: 5, Ruwan, At(6, 20));

        Assert.Equal(DecisionResult.StaleVersion, outcome.Result);
        Assert.Null(journey.Hold);
        Assert.Equal("2", Stop(journey, S2).Platform);
    }

    [Fact]
    public void A_hold_with_a_platform_change_is_refused_when_another_hold_exists_and_nothing_changes()
    {
        var journey = NewJourney();
        journey.SetHoldByController(0, S1, 10, null, Ruwan, At(5, 50));

        var outcome = journey.ChangePlatformByController(1, S2, "4", Platforms, holdMinutes: 5, Ruwan, At(6, 20));

        Assert.Equal(DecisionResult.InvalidState, outcome.Result);
        Assert.Equal("2", Stop(journey, S2).Platform);
        Assert.Equal(1, journey.ControlVersion);
    }

    // ---- conflicts (R-12) ----

    [Fact]
    public void Conflict_warning_when_another_train_expected_at_same_platform_within_5_minutes()
    {
        // This train at the platform 06:40-06:42.
        Assert.True(PlatformRules.Conflicts(At(6, 40), At(6, 42), At(6, 44), At(6, 46)));   // the other arrives 2 minutes after we leave
        Assert.True(PlatformRules.Conflicts(At(6, 40), At(6, 42), At(6, 47), At(6, 48)));   // exactly 5 minutes after we leave
        Assert.True(PlatformRules.Conflicts(At(6, 40), At(6, 42), At(6, 33), At(6, 35)));   // the other leaves 5 minutes before we arrive
        Assert.True(PlatformRules.Conflicts(At(6, 40), At(6, 42), At(6, 41), At(6, 41)));   // at the same time
    }

    [Fact]
    public void No_conflict_when_the_other_train_is_more_than_5_minutes_away()
    {
        Assert.False(PlatformRules.Conflicts(At(6, 40), At(6, 42), At(6, 47, 1), At(6, 50)));
        Assert.False(PlatformRules.Conflicts(At(6, 40), At(6, 42), At(6, 20), At(6, 34, 59)));
    }

    [Fact]
    public void A_stop_with_only_one_time_is_a_point_in_time_for_the_conflict_check()
    {
        Assert.True(PlatformRules.Conflicts(At(6, 40), null, At(6, 44), null));
        Assert.False(PlatformRules.Conflicts(At(6, 40), null, At(6, 46), null));
    }
}
