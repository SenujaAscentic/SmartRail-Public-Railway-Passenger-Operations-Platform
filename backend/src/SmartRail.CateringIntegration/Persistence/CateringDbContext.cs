using Microsoft.EntityFrameworkCore;

namespace SmartRail.CateringIntegration.Persistence;

public enum DeliveryStatus { Pending, Delivered, Failed }

// Where the relay has read up to in LiveOperations' event feed (one row). Kept in the same transaction as the deliveries it created,
// so an event is turned into deliveries exactly once, even if the application stops in the middle.
internal sealed class RelayCursor
{
    public int Id { get; set; } = 1;
    public long LastSequence { get; set; }
}

// One webhook to the provider: the body as it will be sent, and how its delivery is going (US-F2: retried, each with an id).
internal sealed class Delivery
{
    public Guid Id { get; set; }                         // the eventId the provider de-duplicates on
    public string Type { get; set; } = "";               // provider.eta-updated or provider.journey-changed
    public string BodyJson { get; set; } = "";
    public DeliveryStatus Status { get; set; }
    public int Attempts { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public DateTime? FirstAttemptAt { get; set; }        // the 24 hours of retrying count from here
    public DateTime? DeliveredAt { get; set; }
    public string? LastError { get; set; }               // a code ("HTTP 500", "Timeout"), never the body or a message that could quote it
}

// The last ETA the provider was told for a journey at a station, so a new ETA is sent only when it differs by a minute or more (ADR-07).
internal sealed class SentEta
{
    public Guid JourneyId { get; set; }
    public string StationRef { get; set; } = "";
    public DateTime ExpectedArrival { get; set; }
}

// What the provider last told us about one catering station, so the app can show a menu even while the provider is slow or down
// (US-F3). One row per station; the menu is always read and replaced as a whole, so it is kept as one JSON text (a list of MenuEntry).
internal sealed class CachedStation
{
    public string StationRef { get; set; } = "";         // the provider's reference (our station code in the MVP)
    public string Name { get; set; } = "";
    public string Hotline { get; set; } = "";
    public string Opens { get; set; } = "";              // "HH:mm", Colombo time of day, as the provider says it
    public string Closes { get; set; } = "";
    public bool ProviderAvailable { get; set; }          // the provider says it is taking orders at this station
    public string MenuJson { get; set; } = "[]";
    public DateTime FetchedAt { get; set; }              // UTC, when the pull succeeded
}

internal sealed record MenuEntry(string Name, int Price, bool Available);

internal sealed class CateringDbContext(DbContextOptions<CateringDbContext> options) : DbContext(options)
{
    public const string Schema = "catering";

    public DbSet<RelayCursor> Cursor => Set<RelayCursor>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<SentEta> SentEtas => Set<SentEta>();
    public DbSet<CachedStation> CachedStations => Set<CachedStation>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema(Schema);
        model.Entity<RelayCursor>().HasKey(x => x.Id);
        model.Entity<Delivery>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasMaxLength(40);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.LastError).HasMaxLength(60);
            e.HasIndex(x => new { x.Status, x.NextAttemptAt });   // the worker asks for the pending ones that are due
        });
        model.Entity<CachedStation>(e =>
        {
            e.HasKey(x => x.StationRef);
            e.Property(x => x.StationRef).HasMaxLength(20);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Hotline).HasMaxLength(40);
            e.Property(x => x.Opens).HasMaxLength(5);
            e.Property(x => x.Closes).HasMaxLength(5);
        });
        model.Entity<SentEta>(e =>
        {
            e.HasKey(x => new { x.JourneyId, x.StationRef });
            e.Property(x => x.StationRef).HasMaxLength(20);
        });
    }
}
