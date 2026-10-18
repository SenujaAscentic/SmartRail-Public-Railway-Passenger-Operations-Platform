using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Persistence;
using Xunit;

namespace SmartRail.IntegrationTests.LiveOperations;

// R-06 (Live -> Stale -> Lost) and R-23 run by the 15-second timer. The timer runs on the injected clock, so the test starts a
// second application on its own fake clock and moves that clock; the shared fixture clock is never touched.
[Collection("Api")]
public class TrackingRefreshTests(ApiFixture api) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await JourneyCleanup.RemoveAllAsync(api.ConnectionString);

    private async Task<TrackingStatus> TrackingOf(IServiceProvider services, Guid journeyId)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiveOperationsDbContext>();
        return await db.Journeys.AsNoTracking().Where(j => j.Id == journeyId).Select(j => j.Tracking).SingleAsync(Ct);
    }

    // Moves the clock in 15-second steps (one timer tick each) until the journey reaches the status, or gives up.
    private async Task<bool> AdvanceUntil(FakeTimeProvider clock, IServiceProvider services, Guid journeyId, TrackingStatus wanted, TimeSpan upTo)
    {
        for (var moved = TimeSpan.Zero; moved <= upTo; moved += TimeSpan.FromSeconds(15))
        {
            clock.Advance(TimeSpan.FromSeconds(15));
            for (var wait = 0; wait < 10; wait++)   // give the service a moment to run its round
            {
                await Task.Delay(30, Ct);
                if (await TrackingOf(services, journeyId) == wanted)
                {
                    return true;
                }
            }
        }

        return false;
    }

    [Fact]
    public async Task A_train_that_goes_quiet_becomes_stale_after_2_minutes_then_lost_after_10_and_the_stream_is_told()
    {
        var clock = new FakeTimeProvider(api.Clock.GetUtcNow());
        using var factory = api.CreateFactory(builder => builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock)));
        var services = factory.Services;   // starts the application

        var now = clock.GetUtcNow().UtcDateTime;
        var id = await RealJourneys.CreateAsync(services, "2001", new DateOnly(2027, 4, 1), j =>
            j.RecordPosition(new PositionSample(new GeoPoint(7.0, 80.0), 60, now, now, TimeSpan.Zero, 0, new Dictionary<Guid, double>()), now));
        var quiet = await RealJourneys.CreateAsync(services, "2002", new DateOnly(2027, 4, 1));   // never had a position

        Assert.Equal(TrackingStatus.Live, await TrackingOf(services, id));

        Assert.False(await AdvanceUntil(clock, services, id, TrackingStatus.Stale, TimeSpan.FromSeconds(90)));   // under 2 min: still Live
        Assert.True(await AdvanceUntil(clock, services, id, TrackingStatus.Stale, TimeSpan.FromSeconds(60)));    // 2 min reached
        Assert.True(await AdvanceUntil(clock, services, id, TrackingStatus.Lost, TimeSpan.FromMinutes(9)));      // 10 min reached

        // The status changes were announced (Live was announced when the position arrived).
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiveOperationsDbContext>();
        var announced = await db.OutboxEvents.AsNoTracking().Where(e => e.JourneyId == id && e.Type == "DataFreshnessChanged").CountAsync(Ct);
        Assert.Equal(3, announced);   // NotStarted -> Live, Live -> Stale, Stale -> Lost

        // A journey nobody reported on is left alone: there is nothing to age.
        Assert.Equal(TrackingStatus.NotStarted, await TrackingOf(services, quiet));
    }
}
