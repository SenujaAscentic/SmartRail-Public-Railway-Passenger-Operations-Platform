using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace SmartRail.IntegrationTests;

// One real PostgreSQL (in Docker) and one running copy of the whole application, shared by the tests in the "Api" collection.
// Starting the application applies the migrations and loads the seed data, exactly as in Development.
public sealed class ApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17").Build();

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
