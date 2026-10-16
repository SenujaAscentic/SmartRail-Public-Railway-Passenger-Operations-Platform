using Microsoft.EntityFrameworkCore;
using SmartRail.Network.Model;
using SmartRail.Network.Persistence;

namespace SmartRail.Network;

// Answers INetworkSchedule from the network tables. Copies the data into plain records, so callers never see a table.
internal sealed class NetworkScheduleReader(NetworkDbContext db) : INetworkSchedule
{
    public async Task<NetworkSchedule> GetScheduleAsync(CancellationToken cancellationToken)
    {
        var services = await db.Services.AsNoTracking().Include(s => s.Stops).OrderBy(s => s.Number).ToListAsync(cancellationToken);
        var calendars = await db.Calendars.AsNoTracking().Include(c => c.Exceptions).OrderBy(c => c.Code).ToListAsync(cancellationToken);
        var lineByRoute = await db.Routes.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.LineId, cancellationToken);
        var trainSets = await db.TrainSets.AsNoTracking().OrderBy(t => t.Code).ToListAsync(cancellationToken);

        return new NetworkSchedule(
            services.Select(s => new ScheduledService(
                s.Id,
                s.Number,
                s.CalendarId,
                s.Stops.OrderBy(stop => stop.Sequence).Select(stop => new ScheduledStopInfo(
                    stop.Sequence,
                    stop.StationId,
                    stop.CallType == CallType.Pass ? ScheduledCallType.Pass : ScheduledCallType.Stop,
                    stop.ArrivalOffset,
                    stop.DepartureOffset,
                    stop.PlannedPlatform)).ToList(),
                lineByRoute[s.RouteId])).ToList(),
            calendars.Select(c => new ServiceCalendarInfo(
                c.Id,
                c.DaysOfWeek,
                c.Exceptions.Select(e => new CalendarExceptionInfo(e.Date, e.Runs)).ToList())).ToList(),
            trainSets.Select(t => new TrainSetInfo(t.Id, t.Code)).ToList());
    }
}
