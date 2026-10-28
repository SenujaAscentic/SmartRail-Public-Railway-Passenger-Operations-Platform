using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

    // SM-2 (rehearsal 20 Oct): a Running train whose GPS is Lost and which is 22 minutes past its next stop's scheduled time must be
    // shown 22 minutes late (R-23), by the 15-second timer, in the stored journey and in what the public API answers.
    [Fact]
    public async Task A_lost_train_that_is_22_minutes_overdue_at_its_next_stop_is_shown_22_minutes_late()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2027, 6, 9, 0, 0, 0, TimeSpan.Zero));
        var database = await api.CreateDatabaseAsync("sm2_" + Guid.NewGuid().ToString("N")[..8]);
        using var factory = api.CreateFactory(builder => builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock)), useFixtureClock: false, connectionString: database);
        var services = factory.Services;

        // 2001, already Running, last heard of at midnight UTC and 0 minutes late.
        var id = await RealJourneys.CreateAsync(services, "2001", new DateOnly(2027, 6, 20), j =>
        {
            j.Start(j.Stops[0].ScheduledDeparture!.Value, Guid.NewGuid(), Guid.NewGuid());
            var now = clock.GetUtcNow().UtcDateTime;
            j.RecordPosition(new PositionSample(new GeoPoint(7.0, 80.0), 60, now, now, TimeSpan.Zero, 0, new Dictionary<Guid, double>()), now);
        });

        DateTime nextScheduled;
        using (var scope = services.CreateScope())
        {
            var journey = await scope.ServiceProvider.GetRequiredService<LiveOperationsDbContext>().Journeys.AsNoTracking().Include(j => j.Stops).SingleAsync(j => j.Id == id, Ct);
            nextScheduled = journey.Stops.OrderBy(x => x.Sequence).First(x => x.Status == StopStatus.Scheduled && x.ScheduledArrival is not null).ScheduledArrival!.Value;
        }

        clock.SetUtcNow(new DateTimeOffset(nextScheduled.AddMinutes(22), TimeSpan.Zero));   // hours of silence: Lost, and 22 minutes overdue

        var client = factory.CreateClient();
        JsonElement detail = default;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            clock.Advance(TimeSpan.FromSeconds(15) - TimeSpan.FromSeconds(15));   // no more time passes: the timer already has its tick
            await Task.Delay(50, Ct);
            detail = await client.GetFromJsonAsync<JsonElement>($"/api/v1/journeys/{id}", Ct);
            if (detail.GetProperty("status").GetProperty("kind").GetString() == "Late")
            {
                break;
            }
        }

        Assert.Equal("Lost", detail.GetProperty("freshness").GetProperty("tracking").GetString());
        Assert.Equal("Late", detail.GetProperty("status").GetProperty("kind").GetString());
        Assert.Equal(22, detail.GetProperty("status").GetProperty("delayMinutes").GetInt32());
    }

    // SM-2 candidate (QA 20 Oct): the refresh round handles every tracked journey. If ONE of them cannot be refreshed (here: a stored hold
    // that names a station the journey does not have), the others must still be refreshed in the same round, and the failure is logged
    // with the journey id and the kind of error only.
    [Fact]
    public async Task One_journey_that_cannot_be_refreshed_does_not_stop_the_others_and_the_error_is_logged_without_personal_data()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2027, 6, 9, 0, 0, 0, TimeSpan.Zero));
        var log = new LogCapture("TrackingRefresher", LogLevel.Error);
        var database = await api.CreateDatabaseAsync("iso_" + Guid.NewGuid().ToString("N")[..8]);
        using var factory = api.CreateFactory(
            builder =>
            {
                builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock));
                builder.ConfigureLogging(logging => logging.AddProvider(log));
            },
            useFixtureClock: false, connectionString: database);
        var services = factory.Services;

        PositionSample Far(DateTime now) => new(new GeoPoint(7.0, 80.0), 60, now, now, TimeSpan.Zero, 0, new Dictionary<Guid, double>());
        Action<OperationalJourney> running = j =>
        {
            j.Start(j.Stops[0].ScheduledDeparture!.Value, Guid.NewGuid(), Guid.NewGuid());
            var now = clock.GetUtcNow().UtcDateTime;
            j.RecordPosition(Far(now), now);
        };

        // Created first, so it is read first: the broken one must not be able to hide the good one behind it.
        var broken = await RealJourneys.CreateAsync(services, "2002", new DateOnly(2027, 6, 21), running);
        var good = await RealJourneys.CreateAsync(services, "2001", new DateOnly(2027, 6, 20), running);

        DateTime nextScheduled;
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiveOperationsDbContext>();
            var now = clock.GetUtcNow().UtcDateTime;
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE ops."Journeys" SET "HoldStationId" = {Guid.NewGuid()}, "HoldMinutes" = 5, "HoldSetAt" = {now} WHERE "Id" = {broken}""", Ct);
            var journey = await db.Journeys.AsNoTracking().Include(j => j.Stops).SingleAsync(j => j.Id == good, Ct);
            nextScheduled = journey.Stops.OrderBy(x => x.Sequence).First(x => x.Status == StopStatus.Scheduled && x.ScheduledArrival is not null).ScheduledArrival!.Value;
        }

        clock.SetUtcNow(new DateTimeOffset(nextScheduled.AddMinutes(22), TimeSpan.Zero));   // hours of silence, and 22 minutes overdue

        var delay = 0;
        for (var attempt = 0; attempt < 100 && delay != 22; attempt++)
        {
            await Task.Delay(50, Ct);
            using var scope = services.CreateScope();
            delay = await scope.ServiceProvider.GetRequiredService<LiveOperationsDbContext>().Journeys.AsNoTracking()
                .Where(j => j.Id == good).Select(j => j.DelayMinutes).SingleAsync(Ct);
        }

        Assert.Equal(22, delay);   // the good journey was refreshed although the broken one failed in the same round
        var errors = log.Messages.Where(m => m.Level == LogLevel.Error).Select(m => m.Message).ToList();
        Assert.Contains(errors, m => m.Contains(broken.ToString()) && m.Contains("InvalidOperationException"));
        Assert.All(errors, m => Assert.DoesNotMatch(@"@|\+?\d[\d ]{8,}|token|password", m.Replace(broken.ToString(), "<journey id>")));   // the id and the error type only (a GUID can hold a long run of digits, so it is taken out first)
    }
}
