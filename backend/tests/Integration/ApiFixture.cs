using System.Security.Cryptography;
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

    // Secrets for this test run only: generated here, never committed. They stand in for the user-secrets a developer sets,
    // so CI passes without any secret store.
    public string DemoPassword { get; } = "Demo-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12));
    public string JwtSigningKey { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public IReadOnlyDictionary<string, string> CabDeviceSecrets { get; } =
        new[] { "CAB-01", "CAB-02", "CAB-03", "CAB-04" }.ToDictionary(id => id, _ => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)));

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
            {
                var settings = new Dictionary<string, string?>
                {
                    ["ConnectionStrings:SmartRail"] = _database.GetConnectionString(),
                    ["Jwt:SigningKey"] = JwtSigningKey,
                    ["Seed:DemoPassword"] = DemoPassword,
                };
                foreach (var (id, secret) in CabDeviceSecrets)
                {
                    settings[$"Seed:CabDevice:{id}:Token"] = secret;
                }

                config.AddInMemoryCollection(settings);
            });
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
