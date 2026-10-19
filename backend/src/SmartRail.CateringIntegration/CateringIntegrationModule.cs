using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Routing;
using SmartRail.CateringIntegration.Features.GetStationCatering;
using SmartRail.CateringIntegration.Menus;
using SmartRail.CateringIntegration.Persistence;
using SmartRail.CateringIntegration.Relay;

namespace SmartRail.CateringIntegration;

// The CateringIntegration module's entry point for the host (SmartRail.Api). Must be registered after LiveOperations and Network.
public static class CateringIntegrationModule
{
    public static IServiceCollection AddCateringIntegrationModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CateringOptions>(configuration.GetSection("Catering"));

        services.AddDbContext<CateringDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("SmartRail")
                ?? throw new InvalidOperationException("No connection string. Set ConnectionStrings:SmartRail (see the code guide).");
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", CateringDbContext.Schema));
        });

        services.AddHttpClient("railbites");
        services.AddSingleton<MenuPuller>();
        services.AddHostedService<CateringStartupService>();   // migrate first ...
        services.AddSingleton<RelayService>();                 // a singleton too, so the tests can run one round (RunOnceAsync)
        services.AddHostedService(sp => sp.GetRequiredService<RelayService>());   // ... then relay

        return services;
    }

    // The host decides the URL prefix (/api/v1) and passes the group in.
    public static IEndpointRouteBuilder MapCateringIntegrationEndpoints(this IEndpointRouteBuilder routes)
    {
        GetStationCateringEndpoint.Map(routes);
        return routes;
    }
}

// Same pattern as the other modules: Development applies migrations itself; otherwise a missing migration stops the start.
internal sealed class CateringStartupService(IServiceProvider services, IHostEnvironment environment) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CateringDbContext>();

        if (environment.IsDevelopment())
        {
            await db.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            if (pending.Count > 0)
            {
                throw new InvalidOperationException($"The catering database is missing migrations ({string.Join(", ", pending)}). Apply them before starting the application.");
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
