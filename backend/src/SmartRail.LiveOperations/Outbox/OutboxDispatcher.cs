using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartRail.LiveOperations.Persistence;

namespace SmartRail.LiveOperations.Outbox;

// How often the dispatcher looks for new outbox rows. Short, so an event reaches the stream well within a second.
internal sealed record OutboxSettings(TimeSpan PollInterval);

// Picks up outbox rows and hands them to the stream, in order, within about a second of being saved.
//
// Deliberate exception to "all time comes from TimeProvider": the wait between polls is a REAL timer. It is scheduling, not
// a business timestamp, and the tests run on a frozen clock that would never wake it. Every timestamp it stores still
// comes from TimeProvider (OutboxPump).
internal sealed class OutboxDispatcher(
    IServiceScopeFactory scopes, OutboxPump pump, OutboxSettings settings, TimeProvider clock, ILogger<OutboxDispatcher> log) : BackgroundService
{
    private static readonly TimeSpan CleanupEvery = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var lastCleanup = DateTimeOffset.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<LiveOperationsDbContext>();

                // A full batch means there may be more waiting: go on at once instead of sleeping.
                while (await pump.DispatchBatchAsync(db, stoppingToken) == OutboxPump.BatchSize)
                {
                }

                if (clock.GetUtcNow() - lastCleanup >= CleanupEvery)
                {
                    await pump.DeleteOldAsync(db, stoppingToken);
                    lastCleanup = clock.GetUtcNow();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // The database may be briefly away. Keep going; nothing is lost, the rows are still Pending.
                log.LogError(ex, "The outbox dispatcher failed; it will try again.");
            }

            try
            {
                await Task.Delay(settings.PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
