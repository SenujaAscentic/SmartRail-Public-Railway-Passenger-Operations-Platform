using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.PostgreSql;
using Xunit;

namespace SmartRail.IntegrationTests;

// One real PostgreSQL (in Docker) and one running copy of the whole application, shared by the tests in the "Api" collection.
// Starting the application applies the migrations and loads the seed data, exactly as in Development.
public sealed class ApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17").Build();

    // Every application started by the tests runs on this clock: Wednesday 14 Oct 2026, 10:00 in Colombo (04:30 UTC).
    // Fixed, so the number of journeys the tests expect does not depend on the real date. Time stands still unless a test moves it.
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 14, 4, 30, 0, TimeSpan.Zero));

    public string ConnectionString => _database.GetConnectionString();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync();
        Factory = CreateFactory();
        _ = Factory.Server;   // starts the application now, so a startup problem fails here with a clear message
    }

    // A fresh application against the same database. Extra settings can be added, for example Seed:Path.
    public WebApplicationFactory<Program> CreateFactory(Action<IWebHostBuilder>? configure = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(Clock));
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:SmartRail"] = _database.GetConnectionString(),
                }));
            configure?.Invoke(builder);
        });

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _database.DisposeAsync();
    }
}

[CollectionDefinition("Api")]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>;
