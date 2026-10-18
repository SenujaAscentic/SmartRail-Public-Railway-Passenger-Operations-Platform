using Microsoft.Extensions.DependencyInjection;
using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Persistence;
using SmartRail.Network;

namespace SmartRail.IntegrationTests.LiveOperations;

// A journey of a REAL seeded service (real stations, names, platforms and train set) on a date of the test's choosing, so a
// test controls its own data. Dates far from the generated ones (2027) never clash with journey generation.
internal static class RealJourneys
{
    private static readonly TimeZoneInfo Colombo = TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo");

    // `change` runs on the new journey before it is saved (a crew tap, a controller delay, ...).
    public static async Task<Guid> CreateAsync(
        IServiceProvider services, string serviceNumber, DateOnly date, Action<OperationalJourney>? change = null)
    {
        using var scope = services.CreateScope();
        var schedule = await scope.ServiceProvider.GetRequiredService<INetworkSchedule>().GetScheduleAsync(CancellationToken.None);
        var service = schedule.Services.Single(s => s.Number == serviceNumber);
        var trainSet = schedule.TrainSets.Single(t => t.Code == "DPS-01");

        var journey = OperationalJourney.Plan(
            Guid.CreateVersion7(), service.Id, date, trainSet.Id,
            service.Stops.Select(s => new PlannedStop(
                s.Sequence, s.StationId, s.CallType == ScheduledCallType.Pass ? CallType.Pass : CallType.Stop,
                s.ArrivalOffset, s.DepartureOffset, s.PlannedPlatform)),
            Colombo, service.LineId);
        change?.Invoke(journey);

        var db = scope.ServiceProvider.GetRequiredService<LiveOperationsDbContext>();
        db.Journeys.Add(journey);
        await db.SaveChangesAsync();
        JourneyCleanup.Remember(journey.Id);
        return journey.Id;
    }

    // The station ids by code ("FOT", "KDT", ...).
    public static async Task<IReadOnlyDictionary<string, Guid>> StationsAsync(IServiceProvider services, params string[] codes)
    {
        using var scope = services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IStationDirectory>().GetStationIdsAsync(codes, CancellationToken.None);
    }
}
