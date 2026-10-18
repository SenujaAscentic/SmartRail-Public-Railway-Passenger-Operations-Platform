using Microsoft.Extensions.Time.Testing;
using SmartRail.LiveOperations.Features.GenerateJourneys;
using SmartRail.Network;
using Xunit;

namespace SmartRail.UnitTests.LiveOperations.GenerateJourneys;

public class JourneyPlannerTests
{
    private static readonly TimeZoneInfo Colombo = TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo");

    private static readonly Guid ServiceId = Guid.NewGuid();
    private static readonly Guid WeekdayCalendarId = Guid.NewGuid();
    private static readonly Guid WeekendCalendarId = Guid.NewGuid();
    private static readonly Guid TrainSetId = Guid.NewGuid();
    private static readonly Guid LineId = Guid.NewGuid();
    private static readonly IReadOnlyDictionary<string, Guid> NoDrivers = new Dictionary<string, Guid>();

    private static readonly IReadOnlyDictionary<string, string> Assignments = new Dictionary<string, string> { ["1001"] = "DPS-01", ["1003"] = "DPS-01" };
    private static readonly IReadOnlySet<(Guid, DateOnly)> NothingStored = new HashSet<(Guid, DateOnly)>();

    // Service 1001 runs on the weekday calendar (Mon-Fri); service 1003 on the weekend calendar (Sat-Sun, no trains on Poya 25 Oct 2026).
    private static NetworkSchedule Schedule(Guid? weekendServiceId = null)
    {
        ScheduledStopInfo[] stops =
        [
            new(1, Guid.NewGuid(), ScheduledCallType.Stop, null, 390, "1"),
            new(2, Guid.NewGuid(), ScheduledCallType.Stop, 555, null, "2"),
        ];

        return new NetworkSchedule(
            [
                new ScheduledService(ServiceId, "1001", WeekdayCalendarId, stops, LineId),
                new ScheduledService(weekendServiceId ?? Guid.NewGuid(), "1003", WeekendCalendarId, stops, LineId),
            ],
            [
                new ServiceCalendarInfo(WeekdayCalendarId, [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday], []),
                new ServiceCalendarInfo(WeekendCalendarId, [DayOfWeek.Saturday, DayOfWeek.Sunday], [new CalendarExceptionInfo(new DateOnly(2026, 10, 25), Runs: false)]),
            ],
            [new TrainSetInfo(TrainSetId, "DPS-01")]);
    }

    // The clock is set to a moment in Colombo local time.
    private static JourneyPlanner PlannerAt(string colomboLocalTime)
    {
        var local = DateTime.Parse(colomboLocalTime, System.Globalization.CultureInfo.InvariantCulture);
        var utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Colombo);
        return new JourneyPlanner(new FakeTimeProvider(new DateTimeOffset(utc, TimeSpan.Zero)), Colombo);
    }

    [Fact]
    public void Generates_journeys_for_next_3_days()
    {
        // Wednesday 14 Oct 2026: today, Thursday and Friday are all weekdays.
        var journeys = PlannerAt("2026-10-14 10:00").Plan(Schedule(), Assignments, NothingStored, NoDrivers)
            .Where(j => j.ServiceId == ServiceId).ToList();

        Assert.Equal(
            [new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 16)],
            journeys.Select(j => j.ServiceDate).Order());
    }

    // The line comes from the service (Network knows route -> line); the stream envelope needs it (domain model 8.1).
    [Fact]
    public void Journeys_carry_the_line_of_their_service()
    {
        var journeys = PlannerAt("2026-10-14 10:00").Plan(Schedule(), Assignments, NothingStored, NoDrivers);

        Assert.All(journeys, j => Assert.Equal(LineId, j.LineId));
    }

    // The driver of a service is the account id found for the staff number in service-assignments.json (R-07 checks against it).
    [Fact]
    public void Journeys_get_the_driver_of_their_service_and_none_when_the_service_has_no_driver()
    {
        var driver = Guid.NewGuid();
        var drivers = new Dictionary<string, Guid> { ["1001"] = driver };   // service 1003 has none

        var journeys = PlannerAt("2026-10-24 08:00").Plan(Schedule(), Assignments, NothingStored, drivers);

        Assert.All(journeys.Where(j => j.ServiceId == ServiceId), j => Assert.Equal(driver, j.DriverId));
        Assert.All(journeys.Where(j => j.ServiceId != ServiceId), j => Assert.Null(j.DriverId));
    }

    [Fact]
    public void Skips_service_not_running_on_poya_day()
    {
        // Saturday 24 Oct, Sunday 25 Oct (Poya) and Monday 26 Oct: the weekend service runs on Saturday only.
        var weekendServiceId = Guid.NewGuid();
        var journeys = PlannerAt("2026-10-24 08:00").Plan(Schedule(weekendServiceId), Assignments, NothingStored, NoDrivers)
            .Where(j => j.ServiceId == weekendServiceId).ToList();

        Assert.Equal([new DateOnly(2026, 10, 24)], journeys.Select(j => j.ServiceDate));
    }

    [Fact]
    public void Does_not_duplicate_existing_journeys()
    {
        var existing = new HashSet<(Guid, DateOnly)> { (ServiceId, new DateOnly(2026, 10, 14)) };

        var journeys = PlannerAt("2026-10-14 10:00").Plan(Schedule(), Assignments, existing, NoDrivers)
            .Where(j => j.ServiceId == ServiceId).ToList();

        Assert.Equal([new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 16)], journeys.Select(j => j.ServiceDate).Order());
    }

    // The service date is the date in Colombo, not in UTC: at 01:30 Colombo on 14 Oct it is still 13 Oct in UTC.
    [Fact]
    public void Today_is_the_date_in_Colombo_not_in_UTC()
    {
        var journeys = PlannerAt("2026-10-14 01:30").Plan(Schedule(), Assignments, NothingStored, NoDrivers)
            .Where(j => j.ServiceId == ServiceId).ToList();

        Assert.Equal(new DateOnly(2026, 10, 14), journeys.Min(j => j.ServiceDate));
    }

    [Fact]
    public void Exception_that_runs_adds_a_journey_on_a_day_outside_the_weekly_pattern()
    {
        // Saturday 17 Oct is not a weekday, but this calendar says the weekday service runs on that date.
        var schedule = Schedule() with
        {
            Calendars =
            [
                new ServiceCalendarInfo(WeekdayCalendarId, [DayOfWeek.Monday], [new CalendarExceptionInfo(new DateOnly(2026, 10, 17), Runs: true)]),
                new ServiceCalendarInfo(WeekendCalendarId, [], []),
            ],
        };

        var journeys = PlannerAt("2026-10-16 10:00").Plan(schedule, Assignments, NothingStored, NoDrivers)
            .Where(j => j.ServiceId == ServiceId).ToList();

        Assert.Equal([new DateOnly(2026, 10, 17)], journeys.Select(j => j.ServiceDate));
    }

    [Fact]
    public void A_journey_gets_the_train_set_assigned_to_its_service()
    {
        var journey = PlannerAt("2026-10-14 10:00").Plan(Schedule(), Assignments, NothingStored, NoDrivers)
            .First(j => j.ServiceId == ServiceId);

        Assert.Equal(TrainSetId, journey.TrainSetId);
    }
}
