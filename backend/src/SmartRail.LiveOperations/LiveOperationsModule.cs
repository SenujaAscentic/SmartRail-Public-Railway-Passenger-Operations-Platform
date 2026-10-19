using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SmartRail.LiveOperations.Features.GenerateJourneys;
using SmartRail.LiveOperations.Features.CrewJourney;
using SmartRail.LiveOperations.Features.Decisions;
using SmartRail.LiveOperations.Features.JourneyViews;
using SmartRail.LiveOperations.Features.StationBoard;
using SmartRail.LiveOperations.Features.CrewPositions;
using SmartRail.LiveOperations.Features.GetJourney;
using SmartRail.LiveOperations.Features.Idempotency;
using SmartRail.LiveOperations.Features.RefreshTracking;
using SmartRail.LiveOperations.Features.SearchJourneys;
using SmartRail.LiveOperations.Features.StreamEvents;
using SmartRail.LiveOperations.Outbox;
using SmartRail.LiveOperations.Persistence;
using SmartRail.LiveOperations.Stream;
using SmartRail.Network;

namespace SmartRail.LiveOperations;

// The LiveOperations module's entry point for the host (SmartRail.Api).
public static class LiveOperationsModule
{
    // Service dates and offsets are local times in Sri Lanka (CLAUDE.md: store and send UTC, convert once).
    internal const string TimeZoneId = "Asia/Colombo";

    // Call this AFTER AddNetworkModule: hosted services start in the order they are registered, and the start-up
    // check below needs the network seed to be loaded first.
    public static IServiceCollection AddLiveOperationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<OutboxInterceptor>();   // writes the aggregate's events into the outbox with every save
        services.AddDbContext<LiveOperationsDbContext>((provider, options) =>
        {
            var connectionString = configuration.GetConnectionString("SmartRail")
                ?? throw new InvalidOperationException("No connection string. See the message of the Network module for how to set it.");

            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", LiveOperationsDbContext.Schema));
            options.AddInterceptors(provider.GetRequiredService<OutboxInterceptor>());
        });

        services.AddSingleton(provider => new JourneyPlanner(
            provider.GetRequiredService<TimeProvider>(), TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId)));
        services.AddScoped<GenerateJourneysHandler>();

        // The outbox reaches the clients through the stream hub (one fan-out for the whole application).
        // Read when first needed, not now: the host (and the tests) add settings after this method has run.
        services.AddSingleton(provider => new OutboxSettings(
            TimeSpan.FromMilliseconds(provider.GetRequiredService<IConfiguration>().GetValue("Outbox:PollMilliseconds", 250))));
        services.AddSingleton<StreamHub>();
        services.AddSingleton<IEventPublisher>(provider => provider.GetRequiredService<StreamHub>());
        services.AddSingleton<OutboxPump>();
        services.AddSingleton(provider => new StreamSettings(
            TimeSpan.FromSeconds(provider.GetRequiredService<IConfiguration>().GetValue("Stream:HeartbeatSeconds", 15))));
        services.AddScoped<SnapshotReader>();
        services.AddScoped<StreamHandler>();
        services.AddScoped<TrackingRefresher>();
        services.AddScoped<IdempotencyStore>();
        services.AddScoped<CrewJourneyHandler>();
        services.AddScoped<JourneyDetailReader>();
        services.AddScoped<DecisionHandler>();
        services.AddScoped<IJourneyFeed, JourneyFeed>();   // the door CateringIntegration reads journey events through
        services.AddScoped<PlatformConflictFinder>();
        services.AddScoped<CrewPositionsHandler>();

        services.AddHostedService<LiveOperationsStartupService>();   // fails the start if the database or the assignments are wrong
        services.AddHostedService<JourneyGenerationService>();       // then generates now and every hour
        services.AddHostedService<TrackingRefreshService>();         // R-06 and R-23: ages the data of tracked trains every 15 s
        services.AddHostedService<OutboxDispatcher>();               // sends saved events to the stream

        return services;
    }

    // The host decides the URL prefix (/api/v1) and passes the group in.
    public static IEndpointRouteBuilder MapLiveOperationsEndpoints(this IEndpointRouteBuilder routes)
    {
        StreamEndpoint.Map(routes);
        OpsStreamEndpoint.Map(routes);
        StationBoardEndpoint.Map(routes);
        SearchJourneysEndpoint.Map(routes);
        CrewJourneyEndpoints.Map(routes);
        CrewPositionsEndpoint.Map(routes);
        GetJourneyEndpoint.Map(routes);
        DelayEndpoints.Map(routes);
        HoldEndpoints.Map(routes);
        CancelEndpoints.Map(routes);
        TerminateEndpoints.Map(routes);
        SkipEndpoints.Map(routes);
        PlatformEndpoints.Map(routes);
        return routes;
    }
}

// Runs once when the application starts, before the generation begins: brings the "ops" schema up to date
// (Development only, like Network), then checks service-assignments.json against the network.
internal sealed class LiveOperationsStartupService(IServiceProvider services, IHostEnvironment environment) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiveOperationsDbContext>();

        if (environment.IsDevelopment())
        {
            await db.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            if (pending.Count > 0)
            {
                throw new InvalidOperationException(
                    $"The ops database is missing migrations ({string.Join(", ", pending)}). Apply them before starting the application.");
            }
        }

        var schedule = await scope.ServiceProvider.GetRequiredService<INetworkSchedule>().GetScheduleAsync(cancellationToken);
        scope.ServiceProvider.GetRequiredService<GenerateJourneysHandler>().LoadAssignments(schedule);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
