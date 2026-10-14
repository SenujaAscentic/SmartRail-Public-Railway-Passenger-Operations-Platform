using Microsoft.EntityFrameworkCore;
using SmartRail.LiveOperations.Domain;

namespace SmartRail.LiveOperations.Persistence;

// The LiveOperations module's database access. Its tables live in the PostgreSQL schema "ops".
// The Domain project knows nothing about EF Core; everything EF needs to know about the classes is written here.
internal class LiveOperationsDbContext(DbContextOptions<LiveOperationsDbContext> options) : DbContext(options)
{
    public const string Schema = "ops";

    public DbSet<OperationalJourney> Journeys => Set<OperationalJourney>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema(Schema);

        model.Entity<OperationalJourney>(journey =>
        {
            journey.ToTable("Journeys");
            journey.HasKey(j => j.Id);
            journey.Property(j => j.Id).ValueGeneratedNever();
            journey.Property(j => j.Status).HasConversion<string>().HasMaxLength(20);

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
            });
            journey.Navigation(j => j.Stops).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }
}
