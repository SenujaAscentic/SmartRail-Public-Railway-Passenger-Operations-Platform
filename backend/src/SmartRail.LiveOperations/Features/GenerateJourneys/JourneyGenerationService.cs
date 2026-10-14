using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SmartRail.LiveOperations.Features.GenerateJourneys;

// Runs the generation once at start-up and then every hour (ADR-01: a hosted service).
// Hourly is safe because a run only inserts what is missing, and it cannot miss a day.
internal sealed class JourneyGenerationService(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<JourneyGenerationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), clock);

        try
        {
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // The application is shutting down.
        }
    }

    // A failed run (for example the database is briefly unavailable) is logged and tried again at the next tick;
    // it must not stop the application.
    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var added = await scope.ServiceProvider.GetRequiredService<GenerateJourneysHandler>().GenerateAsync(ct);
            logger.LogInformation("Journey generation finished: {Added} journeys added.", added);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Journey generation failed. It will be tried again in an hour.");
        }
    }
}
