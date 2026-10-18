using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartRail.Identity;
using SmartRail.LiveOperations.Features.GenerateJourneys;
using SmartRail.LiveOperations.Persistence;
using SmartRail.Network;
using Xunit;

namespace SmartRail.IntegrationTests.LiveOperations;

// Each journey knows its driver (R-07 accepts positions only from that driver): set at generation from service-assignments.json, and
// filled in afterwards where a journey was made without one.
[Collection("Api")]
public class JourneyDriverTests(ApiFixture api) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await JourneyCleanup.RemoveAllAsync(api.ConnectionString);

    // service number -> the driver's account id, worked out from the seed file and Identity (the test's own copy of the rule)
    private async Task<Dictionary<string, Guid>> ExpectedDrivers()
    {
        using var scope = api.Factory.Services.CreateScope();
        var folder = SeedFolder.Find(configuredPath: null);
        var staffNumbers = ServiceAssignments.ReadDrivers(folder);
        var ids = await scope.ServiceProvider.GetRequiredService<IStaffDirectory>().GetDriverIdsAsync(staffNumbers.Values.Distinct().ToList(), Ct);
        return staffNumbers.ToDictionary(p => p.Key, p => ids[p.Value]);
    }

    private async Task<Dictionary<Guid, string>> NumberByService()
    {
        using var scope = api.Factory.Services.CreateScope();
        var schedule = await scope.ServiceProvider.GetRequiredService<INetworkSchedule>().GetScheduleAsync(Ct);
        return schedule.Services.ToDictionary(s => s.Id, s => s.Number);
    }

    [Fact]
    public async Task Generated_journeys_have_the_driver_named_for_their_service()
    {
        var expected = await ExpectedDrivers();
        var numbers = await NumberByService();
        using var scope = api.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiveOperationsDbContext>();
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!await db.Journeys.AnyAsync(j => j.ServiceDate == new DateOnly(2026, 10, 14), Ct) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100, Ct);
        }

        var generated = await db.Journeys.AsNoTracking()
            .Where(j => j.ServiceDate >= new DateOnly(2026, 10, 14) && j.ServiceDate <= new DateOnly(2026, 10, 16))
            .Select(j => new { j.ServiceId, j.DriverId }).ToListAsync(Ct);

        Assert.NotEmpty(generated);
        Assert.All(generated, j => Assert.Equal(expected[numbers[j.ServiceId]], j.DriverId));
    }

    [Fact]
    public async Task A_journey_made_without_a_driver_gets_one_on_the_next_run_and_a_driver_already_set_is_kept()
    {
        var expected = await ExpectedDrivers();
        var someoneElse = Guid.NewGuid();
        var day = new DateOnly(2027, 5, 3);
        var without = await RealJourneys.CreateAsync(api.Factory.Services, "2001", day);
        var withOther = await RealJourneys.CreateAsync(api.Factory.Services, "2002", day, j => j.AssignDriver(someoneElse));

        using (var scope = api.Factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<GenerateJourneysHandler>().GenerateAsync(Ct);   // what the hourly run does
        }

        using var check = api.Factory.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<LiveOperationsDbContext>();
        Assert.Equal(expected["2001"], (await db.Journeys.AsNoTracking().SingleAsync(j => j.Id == without, Ct)).DriverId);
        Assert.Equal(someoneElse, (await db.Journeys.AsNoTracking().SingleAsync(j => j.Id == withOther, Ct)).DriverId);
    }
}
