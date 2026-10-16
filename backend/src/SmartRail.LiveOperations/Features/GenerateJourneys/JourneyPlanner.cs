using SmartRail.LiveOperations.Domain;
using SmartRail.Network;

namespace SmartRail.LiveOperations.Features.GenerateJourneys;

// Decides which journeys are missing. Pure: it reads nothing and writes nothing; the handler does the database work.
public sealed class JourneyPlanner(TimeProvider clock, TimeZoneInfo timeZone)
{
    // Today and the next 2 days (A-13: 3 days ahead).
    public const int DaysAhead = 3;

    // trainSetCodeByServiceNumber: which train set runs each service (from service-assignments.json); the start-up
    // check guarantees every service has one that exists.
    // existing: journeys that are already stored; they are never planned again or changed (A-17).
    public IReadOnlyList<OperationalJourney> Plan(
        NetworkSchedule schedule,
        IReadOnlyDictionary<string, string> trainSetCodeByServiceNumber,
        IReadOnlySet<(Guid ServiceId, DateOnly ServiceDate)> existing)
    {
        // "Today" is the date in Colombo, not in UTC: at 01:30 in Colombo it is still yesterday in UTC.
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, timeZone).DateTime);

        var calendars = schedule.Calendars.ToDictionary(c => c.Id);
        var trainSetIds = schedule.TrainSets.ToDictionary(t => t.Code, t => t.Id);
        var journeys = new List<OperationalJourney>();

        for (var day = 0; day < DaysAhead; day++)
        {
            var date = today.AddDays(day);

            foreach (var service in schedule.Services)
            {
                if (!Runs(calendars[service.CalendarId], date) || existing.Contains((service.Id, date)))
                {
                    continue;
                }

                var trainSetId = trainSetIds[trainSetCodeByServiceNumber[service.Number]];
                var stops = service.Stops.Select(s => new PlannedStop(
                    s.Sequence, s.StationId, ToDomain(s.CallType), s.ArrivalOffset, s.DepartureOffset, s.PlannedPlatform));

                // The ID is time-ordered; the time comes from the injected clock like every other time in the code.
                journeys.Add(OperationalJourney.Plan(Guid.CreateVersion7(now), service.Id, date, trainSetId, stops, timeZone, service.LineId));
            }
        }

        return journeys;
    }

    // A date listed as an exception wins over the weekly pattern (a Poya day stops it; a special date can add a run).
    private static bool Runs(ServiceCalendarInfo calendar, DateOnly date)
    {
        var exception = calendar.Exceptions.FirstOrDefault(e => e.Date == date);
        return exception?.Runs ?? calendar.DaysOfWeek.Contains(date.DayOfWeek);
    }

    // Network and LiveOperations.Domain each have their own call type; they must not share one (module boundaries).
    private static CallType ToDomain(ScheduledCallType callType) =>
        callType == ScheduledCallType.Pass ? CallType.Pass : CallType.Stop;
}
