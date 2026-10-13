using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartRail.Network.Persistence;
using SmartRail.Network.Seed;
using Xunit;

namespace SmartRail.IntegrationTests.Network;

// The seed loader against a real PostgreSQL: what it writes, what happens on a restart, what stops the application.
[Collection("Api")]
public class NetworkSeedTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<T> InDb<T>(Func<NetworkDbContext, Task<T>> query)
    {
        using var scope = api.Factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<NetworkDbContext>());
    }

    [Fact]
    public async Task Starting_the_application_loads_the_agreed_seed_data()
    {
        Assert.Equal(11, await InDb(db => db.Stations.CountAsync(Ct)));
        Assert.Equal(45, await InDb(db => db.Platforms.CountAsync(Ct)));       // 10+10+2+3+3+4+3+2+2+3+3
        Assert.Equal(1, await InDb(db => db.Lines.CountAsync(Ct)));
        Assert.Equal(2, await InDb(db => db.Routes.CountAsync(Ct)));
        Assert.Equal(22, await InDb(db => db.RouteStations.CountAsync(Ct)));
        Assert.Equal(8, await InDb(db => db.Services.CountAsync(Ct)));
        Assert.Equal(88, await InDb(db => db.ScheduledStops.CountAsync(Ct)));
        Assert.Equal(2, await InDb(db => db.Calendars.CountAsync(Ct)));
        Assert.Equal(1, await InDb(db => db.CalendarExceptions.CountAsync(Ct)));
        Assert.Equal(4, await InDb(db => db.TrainSets.CountAsync(Ct)));
        Assert.Equal(26, await InDb(db => db.Coaches.CountAsync(Ct)));         // 5+5+8+8
    }

    [Fact]
    public async Task The_tables_are_in_the_network_schema()
    {
        var tables = await InDb(db => db.Database
            .SqlQuery<string>($"SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'network' AND table_name = 'Stations'")
            .ToListAsync(Ct));

        Assert.Single(tables);
    }

    [Fact]
    public async Task Loading_the_seed_again_keeps_the_same_ids_and_adds_nothing()
    {
        var idsBefore = await InDb(db => db.Stations.OrderBy(s => s.Code).Select(s => s.Id).ToListAsync(Ct));
        var serviceIdsBefore = await InDb(db => db.Services.OrderBy(s => s.Number).Select(s => s.Id).ToListAsync(Ct));

        // Same as restarting the application: the loader runs again against the same database.
        using (var scope = api.Factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<NetworkSeeder>().SeedAsync(SeedFolder.Find(null), Ct);
        }

        Assert.Equal(idsBefore, await InDb(db => db.Stations.OrderBy(s => s.Code).Select(s => s.Id).ToListAsync(Ct)));
        Assert.Equal(serviceIdsBefore, await InDb(db => db.Services.OrderBy(s => s.Number).Select(s => s.Id).ToListAsync(Ct)));
        Assert.Equal(45, await InDb(db => db.Platforms.CountAsync(Ct)));
        Assert.Equal(88, await InDb(db => db.ScheduledStops.CountAsync(Ct)));
        Assert.Equal(26, await InDb(db => db.Coaches.CountAsync(Ct)));
    }

    [Fact]
    public async Task A_second_application_start_does_not_duplicate_anything()
    {
        using var second = api.CreateFactory();
        _ = second.Server;   // starts a second copy of the application against the same database

        Assert.Equal(11, await InDb(db => db.Stations.CountAsync(Ct)));
        Assert.Equal(8, await InDb(db => db.Services.CountAsync(Ct)));
    }

    [Fact]
    public async Task Start_logs_one_warning_that_names_Kandy_as_an_estimate()
    {
        var logs = new ListLoggerProvider();
        using var second = api.CreateFactory(builder => builder.ConfigureLogging(l => l.AddProvider(logs)));
        _ = second.Server;

        var warning = Assert.Single(logs.Warnings);
        Assert.Contains("KDT", warning);
        Assert.Contains("ESTIMATE", warning);
    }

    [Fact]
    public void Seed_data_marked_WRONG_stops_the_application_from_starting()
    {
        var folder = CopyOfSeedFolder();
        try
        {
            var csv = Path.Combine(folder, "stations.csv");
            File.WriteAllText(csv, File.ReadAllText(csv).Replace("ESTIMATE", "WRONG"));

            using var broken = api.CreateFactory(builder =>
                builder.ConfigureAppConfiguration((_, config) =>
                    config.AddInMemoryCollection(new Dictionary<string, string?> { ["Seed:Path"] = folder })));

            var failure = Assert.ThrowsAny<Exception>(() => broken.Server);

            Assert.Contains(AllMessages(failure), m => m.Contains("DistanceSourceWrong"));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static string CopyOfSeedFolder()
    {
        var copy = Path.Combine(Path.GetTempPath(), "smartrail-seed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(copy);
        foreach (var file in Directory.GetFiles(SeedFolder.Find(null)))
        {
            File.Copy(file, Path.Combine(copy, Path.GetFileName(file)));
        }

        return copy;
    }

    private static IEnumerable<string> AllMessages(Exception? exception)
    {
        for (; exception is not null; exception = exception.InnerException)
        {
            yield return exception.Message;
        }
    }

    // Collects the warnings written while the application starts.
    private sealed class ListLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _warnings = [];
        public IReadOnlyList<string> Warnings => _warnings;

        public ILogger CreateLogger(string categoryName) => new ListLogger(_warnings, categoryName);
        public void Dispose() { }

        private sealed class ListLogger(List<string> warnings, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                // Only the network seed's own warnings, not framework warnings.
                if (logLevel == LogLevel.Warning && category.EndsWith(nameof(NetworkSeeder), StringComparison.Ordinal))
                {
                    lock (warnings)
                    {
                        warnings.Add(formatter(state, exception));
                    }
                }
            }
        }
    }
}
