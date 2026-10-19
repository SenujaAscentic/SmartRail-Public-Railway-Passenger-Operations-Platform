using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.EntityFrameworkCore;
using SmartRail.CateringIntegration.Persistence;
using SmartRail.CateringIntegration.Relay;
using SmartRail.IntegrationTests.Identity;
using SmartRail.IntegrationTests.LiveOperations;
using Testcontainers.PostgreSql;
using Xunit;

namespace SmartRail.IntegrationTests.CateringIntegration;

// Flow 5 end to end: the controller delays or cancels a train in SmartRail, and the provider (RailBites, its own service and its
// own database) receives a signed webhook about the catering station. Both applications run in memory, on one clock, each with its
// own scratch database; SmartRail's HTTP calls to the provider go straight to RailBites' test server.
// The two applications and their clock, shared by the catering tests.
public abstract class CateringTestBase(ApiFixture api) : IAsyncLifetime
{
    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected const string Secret = "test-shared-secret-value";
    protected static readonly DateOnly Future = new(2027, 3, 5);

    protected readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 14, 4, 30, 0, TimeSpan.Zero));
    private readonly PostgreSqlContainer _providerDatabase = new PostgreSqlBuilder("postgres:17").WithDatabase("railbites").Build();
    protected WebApplicationFactory<RailBitesApp> _provider = null!;
    protected WebApplicationFactory<Program> _smartRail = null!;
    protected HttpClient _client = null!;
    private RelayService _relay = null!;
    protected Task RunRoundAsync() => _relay.RunOnceAsync(Ct);
    protected bool _providerDown;

    public async ValueTask InitializeAsync()
    {
        await _providerDatabase.StartAsync();
        _provider = new WebApplicationFactory<RailBitesApp>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:RailBites", _providerDatabase.GetConnectionString());
            builder.UseSetting("RailBites:SharedSecret", Secret);
            builder.UseSetting("RailBites:ApiKey", "test-api-key-value");
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(_clock));
        });
        _ = _provider.Server;

        var database = await api.CreateDatabaseAsync("catering_relay_" + Guid.NewGuid().ToString("N")[..8]);
        _smartRail = api.CreateFactory(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Catering:ProviderUrl"] = "http://railbites.test",
                ["Catering:SharedSecret"] = Secret,
                ["Catering:ProviderApiKey"] = "test-api-key-value",
                ["Catering:RelayEnabled"] = "false",   // the tests run the rounds themselves
            }));
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<TimeProvider>(_clock);
                services.AddHttpClient("railbites").ConfigurePrimaryHttpMessageHandler(() => new ProviderSwitch(_provider.Server.CreateHandler(), () => _providerDown));
            });
        }, connectionString: database);
        _ = _smartRail.Server;

        _client = _smartRail.NewClient();
        _relay = _smartRail.Services.GetRequiredService<RelayService>();
        await _relay.RunOnceAsync(Ct);   // the first round pulls the menus and sets the webhook cursor to "now"
    }

    public async ValueTask DisposeAsync()
    {
        await _smartRail.DisposeAsync();
        await _provider.DisposeAsync();
        await _providerDatabase.DisposeAsync();
    }

    // Makes the provider unreachable the way a real outage does.
    private sealed class ProviderSwitch(HttpMessageHandler inner, Func<bool> down) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            down() ? throw new HttpRequestException("provider down") : base.SendAsync(request, cancellationToken);
    }
}
