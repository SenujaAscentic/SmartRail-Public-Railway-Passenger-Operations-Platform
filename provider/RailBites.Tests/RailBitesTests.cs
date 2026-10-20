using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.PostgreSql;
using Xunit;

namespace RailBites.Tests;

// One real PostgreSQL and one running RailBites for the tests of this file. RailBites has its own database, as in the demo.
public sealed class RailBitesFixture : IAsyncLifetime
{
    public const string Secret = "test-shared-secret-value";
    public const string ApiKey = "test-api-key-value";

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17").WithDatabase("railbites").Build();

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 20, 0, 55, 0, TimeSpan.Zero));
    public WebApplicationFactory<RailBitesApp> Factory { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync();
        Factory = new WebApplicationFactory<RailBitesApp>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:RailBites", _database.GetConnectionString());
            builder.UseSetting("RailBites:SharedSecret", Secret);
            builder.UseSetting("RailBites:ApiKey", ApiKey);
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(Clock));
        });
        _ = Factory.Server;
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _database.DisposeAsync();
    }
}

public class RailBitesTests(RailBitesFixture fixture) : IClassFixture<RailBitesFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly HttpClient _client = fixture.Factory.CreateClient();

    private static object Eta(Guid id, string? reason = null) => new
    {
        eventId = id, type = "provider.eta-updated", occurredAt = "2026-10-20T00:55:00Z", serviceNumber = "1001", serviceDate = "2026-10-20",
        stationRef = "PLG", scheduledArrival = "2026-10-20T02:43:00Z", expectedArrival = "2026-10-20T02:50:00Z", reason,
    };

    // Sends the way SmartRail does: the body as text, the timestamp, and the signature over timestamp + body.
    private async Task<HttpResponseMessage> Send(object body, DateTimeOffset? sentAt = null, string? secret = null, Func<string, string>? tamper = null, Guid? headerId = null, string? signatureOverride = null)
    {
        var text = body as string ?? JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var timestamp = (sentAt ?? fixture.Clock.GetUtcNow()).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss'Z'");
        var signature = signatureOverride ?? WebhookSignature.Compute(secret ?? RailBitesFixture.Secret, timestamp, text);
        var request = new HttpRequestMessage(HttpMethod.Post, "/provider-api/v1/webhooks/smartrail")
        {
            Content = new StringContent(tamper?.Invoke(text) ?? text, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-SmartRail-Event-Id", (headerId ?? (body is string ? Guid.NewGuid() : (Guid)body.GetType().GetProperty("eventId")!.GetValue(body)!)).ToString());
        request.Headers.Add("X-SmartRail-Timestamp", timestamp);
        request.Headers.Add("X-SmartRail-Signature", signature);
        return await _client.SendAsync(request, Ct);
    }

    private async Task<JsonElement> Stored() => await _client.GetFromJsonAsync<JsonElement>("/events.json", Ct);

    private static bool Has(JsonElement events, Guid id) => events.EnumerateArray().Any(e => e.GetProperty("eventId").GetGuid() == id);

    // ---- the webhook ----

    [Fact]
    public async Task A_correctly_signed_event_is_accepted_and_stored_with_what_it_says()
    {
        var id = Guid.NewGuid();

        var response = await Send(Eta(id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = (await Stored()).EnumerateArray().Single(e => e.GetProperty("eventId").GetGuid() == id);
        Assert.Equal("provider.eta-updated", stored.GetProperty("type").GetString());
        Assert.Equal("1001", stored.GetProperty("serviceNumber").GetString());
        Assert.Equal("PLG", stored.GetProperty("stationRef").GetString());
        Assert.Equal(new DateTime(2026, 10, 20, 2, 50, 0, DateTimeKind.Utc), stored.GetProperty("expectedArrival").GetDateTime());
    }

    [Fact]
    public async Task A_wrong_or_missing_signature_is_refused_and_nothing_is_stored()
    {
        var id = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(Eta(id), secret: "another-secret")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(Eta(id), signatureOverride: "sha256=00")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(Eta(id), signatureOverride: "")).StatusCode);

        Assert.False(Has(await Stored(), id));
    }

    [Fact]
    public async Task A_body_changed_after_signing_is_refused()
    {
        var id = Guid.NewGuid();

        var response = await Send(Eta(id), tamper: text => text.Replace("02:50:00Z", "04:50:00Z"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(Has(await Stored(), id));
    }

    [Fact]
    public async Task A_request_older_than_5_minutes_or_from_the_future_is_refused_even_when_correctly_signed()
    {
        var now = fixture.Clock.GetUtcNow();

        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(Eta(Guid.NewGuid()), sentAt: now.AddMinutes(-5).AddSeconds(-5))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(Eta(Guid.NewGuid()), sentAt: now.AddMinutes(6))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(Eta(Guid.NewGuid()), sentAt: now.AddMinutes(-4))).StatusCode);   // inside the window
    }

    [Fact]
    public async Task An_event_that_was_already_received_is_answered_200_and_stored_once()
    {
        var id = Guid.NewGuid();

        var first = await Send(Eta(id));
        var again = await Send(Eta(id));
        var concurrent = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Send(Eta(id))));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.All(concurrent, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(1, (await Stored()).EnumerateArray().Count(e => e.GetProperty("eventId").GetGuid() == id));
        Assert.Contains("duplicate", await again.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task An_unknown_type_an_id_that_does_not_match_the_header_and_broken_JSON_are_400()
    {
        var id = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(new { eventId = id, type = "provider.nonsense", occurredAt = "2026-10-20T00:55:00Z", serviceNumber = "1", serviceDate = "2026-10-20", stationRef = "PLG" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(Eta(id), headerId: Guid.NewGuid())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("{ this is not json")).StatusCode);   // signed correctly, but not JSON
        Assert.False(Has(await Stored(), id));
    }

    // ---- the event log page ----

    [Fact]
    public async Task The_event_page_lists_received_events_and_shows_a_reason_only_as_text()
    {
        var id = Guid.NewGuid();
        await Send(Eta(id, reason: "<script>alert('x')</script> signal fault"));

        var page = await _client.GetStringAsync("/events", Ct);

        Assert.Contains(id.ToString(), page);
        Assert.Contains("ETA updated", page);
        Assert.Contains("<title>RailCantima: events from SmartRail</title>", page);   // its display name; RailBites is the code name
        Assert.Contains("&lt;script&gt;alert(&#39;x&#39;)&lt;/script&gt; signal fault", page);
        Assert.DoesNotContain("<script>alert", page);
    }

    // ---- what SmartRail pulls ----

    private async Task<HttpResponseMessage> Pull(string path, string? key = RailBitesFixture.ApiKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (key is not null) request.Headers.Add("X-Api-Key", key);
        return await _client.SendAsync(request, Ct);
    }

    [Fact]
    public async Task The_stations_and_menus_need_SmartRails_API_key()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Pull("/provider-api/v1/stations", key: null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Pull("/provider-api/v1/stations", key: "wrong")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Pull("/provider-api/v1/stations/PLG/menu", key: "wrong")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Pull("/provider-api/v1/stations")).StatusCode);
    }

    [Fact]
    public async Task Polgahawela_is_served_with_hotline_opening_hours_and_a_menu_with_a_sold_out_item()
    {
        var stations = await (await Pull("/provider-api/v1/stations")).Content.ReadFromJsonAsync<JsonElement>(Ct);
        var polgahawela = stations.EnumerateArray().Single(s => s.GetProperty("ref").GetString() == "PLG");
        Assert.Equal("Polgahawela", polgahawela.GetProperty("name").GetString());
        Assert.False(string.IsNullOrWhiteSpace(polgahawela.GetProperty("hotline").GetString()));
        Assert.Equal("06:00", polgahawela.GetProperty("openingHours").GetProperty("opens").GetString());
        Assert.Equal("20:00", polgahawela.GetProperty("openingHours").GetProperty("closes").GetString());

        var menu = await (await Pull("/provider-api/v1/stations/PLG/menu")).Content.ReadFromJsonAsync<JsonElement>(Ct);
        var items = menu.GetProperty("items").EnumerateArray().ToList();
        Assert.True(items.Count >= 3);
        Assert.All(items, i => Assert.True(i.GetProperty("priceLkr").GetInt32() > 0));
        Assert.Contains(items, i => !i.GetProperty("available").GetBoolean());   // one is sold out
        Assert.Contains(items, i => i.GetProperty("available").GetBoolean());
    }

    [Fact]
    public async Task An_unknown_station_has_no_menu()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await Pull("/provider-api/v1/stations/XXX/menu")).StatusCode);
    }

    // ---- the demo clock ----

    [Fact]
    public void The_demo_clock_is_refused_outside_Development()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Clock:Start"] = "2026-10-20T06:25:00+05:30" }).Build();

        Assert.Throws<InvalidOperationException>(() => DemoClock.Create(configuration, new TestEnvironment("Production")));
        Assert.IsType<DemoClock>(DemoClock.Create(configuration, new TestEnvironment("Development")));
    }

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "RailBites";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
