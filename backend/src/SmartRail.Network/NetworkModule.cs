using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SmartRail.Network.Features.SearchStations;
using SmartRail.Network.Persistence;
using SmartRail.Network.Seed;

namespace SmartRail.Network;

// The Network module's entry points for the host (SmartRail.Api).
public static class NetworkModule
{
    public static IServiceCollection AddNetworkModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<NetworkDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("SmartRail")
                ?? throw new InvalidOperationException(
                    "No connection string. Set it with: dotnet user-secrets set \"ConnectionStrings:SmartRail\" \"Host=localhost;Database=smartrail;Username=smartrail;Password=<your password>\" --project backend/src/SmartRail.Api");

            // Each module keeps its own migration history table inside its own schema.
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", NetworkDbContext.Schema));
        });

        services.AddScoped<NetworkSeeder>();
        // The OpenAPI build step starts the application only to read its endpoints; it has no database,
        // so the migration and seeding must not run then.
        if (!IsGeneratingApiDocument)
        {
            services.AddHostedService<NetworkStartupService>();
        }

        return services;
    }

    private static bool IsGeneratingApiDocument =>
        System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

    // The host decides the URL prefix (/api/v1) and passes the group in.
    public static IEndpointRouteBuilder MapNetworkEndpoints(this IEndpointRouteBuilder routes)
    {
        SearchStationsEndpoint.Map(routes);
        return routes;
    }
}

// Runs once when the application starts: brings the schema up to date, then loads the seed data.
// If anything fails, the application does not start.
internal sealed class NetworkStartupService(IServiceProvider services, IHostEnvironment environment, IConfiguration configuration)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NetworkDbContext>();

        if (environment.IsDevelopment())
        {
            // Migrations apply automatically in Development only.
            await db.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            if (pending.Count > 0)
            {
                throw new InvalidOperationException(
                    $"The network database is missing migrations ({string.Join(", ", pending)}). Apply them before starting the application.");
            }
        }

        var seedFolder = SeedFolder.Find(configuration["Seed:Path"]);
        await scope.ServiceProvider.GetRequiredService<NetworkSeeder>().SeedAsync(seedFolder, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
