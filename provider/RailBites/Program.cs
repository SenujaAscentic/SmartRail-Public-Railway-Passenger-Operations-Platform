using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RailBites;

// RailBites: the catering provider's own service (US-F1). It receives SmartRail's signed webhooks, shows them on /events, and serves
// its stations and menus to SmartRail's pull. Settings (user-secrets or environment variables, never in the repository):
//   ConnectionStrings:RailBites  its own database
//   RailBites:SharedSecret       the HMAC secret SmartRail signs its webhooks with (SmartRail: Catering:SharedSecret)
//   RailBites:ApiKey             the key SmartRail sends when it pulls stations and menus (SmartRail: Catering:ProviderApiKey)
//   Clock:Start, Clock:Speed     DEVELOPMENT ONLY: the same demo clock as SmartRail's, so the 5-minute replay check agrees with it
var builder = WebApplication.CreateBuilder(args);

string Need(string key, string how) =>
    builder.Configuration[key] is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException($"Setting {key} is missing. {how}");

var connection = Need("ConnectionStrings:RailBites", "Set it with: dotnet user-secrets set \"ConnectionStrings:RailBites\" \"Host=localhost;Database=railbites;Username=smartrail;Password=<your password>\" --project provider/RailBites");
Need("RailBites:SharedSecret", "Set it with: dotnet user-secrets set \"RailBites:SharedSecret\" \"<the same value as SmartRail's Catering:SharedSecret>\" --project provider/RailBites");
Need("RailBites:ApiKey", "Set it with: dotnet user-secrets set \"RailBites:ApiKey\" \"<the same value as SmartRail's Catering:ProviderApiKey>\" --project provider/RailBites");

builder.Services.AddDbContext<RailBitesDbContext>(options => options.UseNpgsql(connection));
builder.Services.AddSingleton(DemoClock.Create(builder.Configuration, builder.Environment));

var app = builder.Build();

// Its own database: created on first start (RailBites is a demo service, so no migrations), then the demo data.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RailBitesDbContext>();
    await db.Database.EnsureCreatedAsync();
    await Seed.EnsureAsync(db, CancellationToken.None);
}

WebhookEndpoint.Map(app);
ProviderApi.Map(app);
EventsPage.Map(app);
app.MapGet("/", () => Results.Redirect("/events"));

app.Run();

// For the tests (WebApplicationFactory).
// Internal so a test project that also references SmartRail.Api (two classes named Program) does not clash (CS0433); the marker below is what tests start.
internal partial class Program;

public sealed class RailBitesApp;

// The demo clock for Development: starts at Clock:Start and runs at Clock:Speed. Refused outside Development.
internal sealed class DemoClock(DateTimeOffset start, double speed, TimeProvider real) : TimeProvider
{
    private readonly DateTimeOffset _realStart = real.GetUtcNow();

    public override DateTimeOffset GetUtcNow() => start + (real.GetUtcNow() - _realStart) * speed;

    public static TimeProvider Create(IConfiguration configuration, IHostEnvironment environment)
    {
        var startText = configuration["Clock:Start"];
        var speedText = configuration["Clock:Speed"];
        if (string.IsNullOrWhiteSpace(startText) && string.IsNullOrWhiteSpace(speedText))
        {
            return TimeProvider.System;
        }

        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException("Clock:Start and Clock:Speed are allowed only in the Development environment.");
        }

        var start = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(startText) && !DateTimeOffset.TryParse(startText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out start))
        {
            throw new InvalidOperationException($"Clock:Start is not a date and time with an offset: '{startText}'.");
        }

        var speed = 1.0;
        if (!string.IsNullOrWhiteSpace(speedText) && (!double.TryParse(speedText, NumberStyles.Float, CultureInfo.InvariantCulture, out speed) || speed <= 0 || speed > 100))
        {
            throw new InvalidOperationException($"Clock:Speed must be above 0 and at most 100: '{speedText}'.");
        }

        return new DemoClock(start, speed, TimeProvider.System);
    }
}
