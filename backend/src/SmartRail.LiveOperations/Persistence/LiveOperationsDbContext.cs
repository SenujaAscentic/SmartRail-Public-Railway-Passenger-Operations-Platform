using Microsoft.EntityFrameworkCore;
using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Features.Idempotency;
using SmartRail.LiveOperations.Outbox;

namespace SmartRail.LiveOperations.Persistence;

// The LiveOperations module's database access. Its tables live in the PostgreSQL schema "ops".
// The Domain project knows nothing about EF Core; everything EF needs to know about the classes is written here.
internal class LiveOperationsDbContext(DbContextOptions<LiveOperationsDbContext> options) : DbContext(options)
{
    public const string Schema = "ops";

    public DbSet<OperationalJourney> Journeys => Set<OperationalJourney>();
    internal DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();
    internal DbSet<ProcessedAction> ProcessedActions => Set<ProcessedAction>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema(Schema);

        model.Entity<OperationalJourney>(journey =>
        {
            journey.ToTable("Journeys");
            journey.HasKey(j => j.Id);
            journey.Property(j => j.Id).ValueGeneratedNever();
            journey.Property(j => j.Status).HasConversion<string>().HasMaxLength(20);
            journey.Property(j => j.DelaySource).HasConversion<string>().HasMaxLength(20);
            journey.Property(j => j.Tracking).HasConversion<string>().HasMaxLength(20);

            // The row version (R-18 rowVersion): Postgres changes the system column xmin on every update, and EF refuses an update
            // when someone else saved first. A shadow property, so the Domain project stays free of EF.
            journey.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();

            // The database itself guarantees a service runs at most once per date (A-13): no duplicates, whatever the code does.
            journey.HasIndex(j => new { j.ServiceId, j.ServiceDate }).IsUnique();

            // The stops belong to the journey and are never read without it: one owned collection, one table.
            journey.OwnsMany(j => j.Stops, stop =>
            {
                stop.ToTable("JourneyStops");
                stop.WithOwner().HasForeignKey("JourneyId");
                stop.HasKey("JourneyId", nameof(JourneyStop.Sequence));
                stop.Property(s => s.CallType).HasConversion<string>().HasMaxLength(10);
                stop.Property(s => s.PlannedPlatform).HasMaxLength(10);
                stop.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
                stop.Property(s => s.Zone).HasConversion<string>().HasMaxLength(20);
                stop.Property(s => s.ActualArrivalSource).HasConversion<string>().HasMaxLength(20);
                stop.Property(s => s.ActualDepartureSource).HasConversion<string>().HasMaxLength(20);
            });
            journey.Navigation(j => j.Stops).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        model.Entity<ProcessedAction>(action =>
        {
            action.ToTable("ProcessedActions");
            action.HasKey(a => a.ClientActionId);
            action.Property(a => a.ClientActionId).ValueGeneratedNever();
            action.Property(a => a.Kind).HasMaxLength(20);
            // "json", not "jsonb": a repeat must get back the exact text it was first answered with (jsonb would reorder the keys).
            action.Property(a => a.ResultJson).HasColumnType("json");
            action.HasIndex(a => a.CreatedAt);   // the 48-hour clean-up
        });

        // The stream sequence (contract section 4). A database sequence never goes back, even after old rows are deleted,
        // so a client's Last-Event-ID can never match a newer event by accident.
        model.HasSequence<long>("EventSequence");

        model.Entity<OutboxEvent>(outbox =>
        {
            outbox.ToTable("OutboxEvents");
            outbox.HasKey(e => e.Id);
            outbox.Property(e => e.Id).ValueGeneratedOnAdd();
            outbox.Property(e => e.Type).HasMaxLength(60);
            outbox.Property(e => e.Json).HasColumnType("jsonb");
            outbox.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            outbox.HasIndex(e => e.EventId).IsUnique();
            outbox.HasIndex(e => new { e.Status, e.Id });      // the dispatcher: oldest Pending first
            outbox.HasIndex(e => e.Sequence).IsUnique();       // resume: rows after a sequence (NULLs may repeat)
        });
    }
}
