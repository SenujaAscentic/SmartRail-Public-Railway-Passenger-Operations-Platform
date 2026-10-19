using Microsoft.EntityFrameworkCore;

namespace RailBites;

// RailBites is the catering provider's OWN system (US-F1): its own service and its own database. It knows nothing of SmartRail's
// tables; it only receives signed webhooks and answers SmartRail's pulls. No passenger data is ever stored (US-F2 AC5).

// One event received from SmartRail, kept for the event log page. The EventId is the key: a repeated event is the same row.
public sealed class ReceivedEvent
{
    public Guid EventId { get; set; }
    public string Type { get; set; } = "";               // provider.eta-updated or provider.journey-changed
    public DateTime ReceivedAt { get; set; }             // UTC, the provider's clock
    public DateTime OccurredAt { get; set; }             // UTC, SmartRail's clock
    public string ServiceNumber { get; set; } = "";
    public DateOnly ServiceDate { get; set; }
    public string StationRef { get; set; } = "";         // our reference of the catering station (the MVP uses SmartRail's station code)
    public DateTime? ScheduledArrival { get; set; }
    public DateTime? ExpectedArrival { get; set; }
    public string? Change { get; set; }                  // Cancelled or Terminated (journey-changed events)
    public string? Reason { get; set; }                  // plain text, shown as text only
}

public sealed class CateringStation
{
    public string Ref { get; set; } = "";
    public string Name { get; set; } = "";
    public string Hotline { get; set; } = "";
    public TimeOnly Opens { get; set; }
    public TimeOnly Closes { get; set; }
    public bool Available { get; set; } = true;
}

public sealed class MenuItem
{
    public int Id { get; set; }
    public string StationRef { get; set; } = "";
    public string Name { get; set; } = "";
    public int PriceLkr { get; set; }
    public bool Available { get; set; } = true;          // false = sold out
}

public sealed class RailBitesDbContext(DbContextOptions<RailBitesDbContext> options) : DbContext(options)
{
    public DbSet<ReceivedEvent> Events => Set<ReceivedEvent>();
    public DbSet<CateringStation> Stations => Set<CateringStation>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<ReceivedEvent>(e =>
        {
            e.HasKey(x => x.EventId);
            e.HasIndex(x => x.ReceivedAt);
        });
        model.Entity<CateringStation>().HasKey(x => x.Ref);
        model.Entity<MenuItem>().HasIndex(x => x.StationRef);
    }
}

// The demo data (US-F1 AC2): the two stations SmartRail's seed marks as catering stations, with a menu, a hotline and opening hours.
// The hotline numbers are fictional.
internal static class Seed
{
    public static async Task EnsureAsync(RailBitesDbContext db, CancellationToken ct)
    {
        if (await db.Stations.AnyAsync(ct))
        {
            return;
        }

        db.Stations.AddRange(
            new CateringStation { Ref = "PLG", Name = "Polgahawela", Hotline = "+94 11 555 0100", Opens = new TimeOnly(6, 0), Closes = new TimeOnly(20, 0) },
            new CateringStation { Ref = "PDA", Name = "Peradeniya", Hotline = "+94 11 555 0101", Opens = new TimeOnly(6, 0), Closes = new TimeOnly(20, 0) });

        db.MenuItems.AddRange(
            new MenuItem { StationRef = "PLG", Name = "Egg sandwich", PriceLkr = 250 },
            new MenuItem { StationRef = "PLG", Name = "Vegetable roti", PriceLkr = 150 },
            new MenuItem { StationRef = "PLG", Name = "Fish bun", PriceLkr = 120 },
            new MenuItem { StationRef = "PLG", Name = "Milk tea", PriceLkr = 80, Available = false },
            new MenuItem { StationRef = "PLG", Name = "Wood apple juice", PriceLkr = 200 },
            new MenuItem { StationRef = "PDA", Name = "Rice and curry packet", PriceLkr = 450 },
            new MenuItem { StationRef = "PDA", Name = "Vegetable roti", PriceLkr = 150 },
            new MenuItem { StationRef = "PDA", Name = "Plain tea", PriceLkr = 60 });

        await db.SaveChangesAsync(ct);
    }
}
