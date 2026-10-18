using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SmartRail.LiveOperations.Features.RefreshTracking;

// Runs TrackingRefresher every 15 seconds (the contract asks for at least every 30 s, so that lateness grows "within a minute",
// US-P4 AC1). The timer runs on the injected clock, not on a real one: this is business time (how old is the last position?),
// so a test moves it by advancing the fake clock. (The outbox dispatcher is different: it must run on a frozen test clock.)
internal sealed class TrackingRefreshService(IServiceScopeFactory scopes, TimeProvider clock, ILogger<TrackingRefreshService> log) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<TrackingRefresher>().RunOnceAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // A failed round is retried at the next tick; it never stops the application.
                    log.LogError(ex, "Refreshing the tracking status failed; it will be tried again.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The application is stopping.
        }
    }
}
