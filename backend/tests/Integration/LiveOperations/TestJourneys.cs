using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Persistence;

namespace SmartRail.IntegrationTests.LiveOperations;

// A journey of its own for one test, so tests never touch each other's data. The journey tables hold no foreign keys to
// Network (module boundary), so the services and stations can be made up. Times are UTC, on the fixed test date.
internal sealed record TestJourney(Guid Id, Guid LineId, Guid Origin, Guid Middle, Guid Terminus, DateTime MiddleScheduledArrival)
{
    public static readonly DateOnly Date = new(2026, 10, 14);   // "today" for the fixed test clock

    public static async Task<TestJourney> CreateAsync(IServiceProvider services, Guid? lineId = null)
    {
        var line = lineId ?? Guid.NewGuid();
        var origin = Guid.NewGuid();
        var middle = Guid.NewGuid();
        var terminus = Guid.NewGuid();
        var journey = OperationalJourney.Plan(
            Guid.CreateVersion7(), Guid.NewGuid(), Date, Guid.NewGuid(),
            [
                new PlannedStop(1, origin, CallType.Stop, null, 360, "1"),   // departs 06:00
                new PlannedStop(2, middle, CallType.Stop, 400, 402, "2"),    // 06:40 / 06:42
                new PlannedStop(3, terminus, CallType.Stop, 450, null, "3"), // arrives 07:30
            ],
            TimeZoneInfo.Utc, line);

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiveOperationsDbContext>();
        db.Journeys.Add(journey);
        await db.SaveChangesAsync();
        JourneyCleanup.Remember(journey.Id);

        return new TestJourney(journey.Id, line, origin, middle, terminus, new DateTime(2026, 10, 14, 6, 40, 0, DateTimeKind.Utc));
    }

    // A crew tap at one of the stops, saved the way a slice would: load the journey, change it, save.
    public async Task TapAsync(IServiceProvider services, Guid stationId, StopEventKind kind, DateTime at, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiveOperationsDbContext>();
        var journey = await db.Journeys.Include(j => j.Stops).SingleAsync(j => j.Id == Id, ct);
        journey.RecordCrewTime(stationId, kind, at);
        await db.SaveChangesAsync(ct);
    }

    // The middle stop reached `minutes` after its scheduled arrival: stop 1 inferred, stop 2 arrived, delay changed.
    public Task ArriveLateAtMiddleAsync(IServiceProvider services, int minutes, CancellationToken ct) =>
        TapAsync(services, Middle, StopEventKind.Arrival, MiddleScheduledArrival.AddMinutes(minutes), ct);
}
