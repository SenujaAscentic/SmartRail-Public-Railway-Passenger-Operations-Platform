using Microsoft.EntityFrameworkCore;
using SmartRail.Network.Model;

namespace SmartRail.Network.Persistence;

// The Network module's database access. All tables live in the PostgreSQL schema "network";
// no other module reads or writes them (CLAUDE.md section 3).
public class NetworkDbContext(DbContextOptions<NetworkDbContext> options) : DbContext(options)
{
    public const string Schema = "network";

    public DbSet<Line> Lines => Set<Line>();
    public DbSet<Station> Stations => Set<Station>();
    public DbSet<Platform> Platforms => Set<Platform>();
    public DbSet<Route> Routes => Set<Route>();
    public DbSet<RouteStation> RouteStations => Set<RouteStation>();
    public DbSet<ServiceCalendar> Calendars => Set<ServiceCalendar>();
    public DbSet<CalendarException> CalendarExceptions => Set<CalendarException>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<ScheduledStop> ScheduledStops => Set<ScheduledStop>();
    public DbSet<TrainSet> TrainSets => Set<TrainSet>();
    public DbSet<Coach> Coaches => Set<Coach>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema(Schema);

        // Every enum is stored as its name ("Express"), not a number, so the data is readable and safe to reorder.
        model.Entity<Route>().Property(r => r.Direction).HasConversion<string>().HasMaxLength(10);
        model.Entity<Service>().Property(s => s.TrainType).HasConversion<string>().HasMaxLength(20);
        model.Entity<ScheduledStop>().Property(s => s.CallType).HasConversion<string>().HasMaxLength(10);
        model.Entity<TrainSet>().Property(t => t.Traction).HasConversion<string>().HasMaxLength(30);
        model.Entity<Coach>().Property(c => c.Class).HasConversion<string>().HasMaxLength(10);
        model.Entity<Coach>().Property(c => c.Seating).HasConversion<string>().HasMaxLength(10);

        // Each row can be found again by a natural key, which is what lets the seed loader run on every start
        // without duplicating anything. The unique indexes below make duplicates impossible.
        model.Entity<Line>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(20);
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasIndex(x => x.Code).IsUnique();
        });

        model.Entity<Station>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(10);
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasMany(x => x.Platforms).WithOne().HasForeignKey(p => p.StationId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<Platform>(e =>
        {
            e.Property(x => x.Number).HasMaxLength(10);
            e.HasIndex(x => new { x.StationId, x.Number }).IsUnique();
        });

        model.Entity<Route>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(30);
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasOne<Line>().WithMany(l => l.Routes).HasForeignKey(r => r.LineId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Stations).WithOne().HasForeignKey(s => s.RouteId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<RouteStation>(e =>
        {
            e.Property(x => x.DistanceKm).HasPrecision(8, 3);
            e.HasIndex(x => new { x.RouteId, x.Sequence }).IsUnique();
            e.HasIndex(x => new { x.RouteId, x.StationId }).IsUnique();
            e.HasOne<Station>().WithMany().HasForeignKey(x => x.StationId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<ServiceCalendar>(e =>
        {
            e.ToTable("Calendars");
            e.Property(x => x.Code).HasMaxLength(30);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasMany(x => x.Exceptions).WithOne().HasForeignKey(x => x.CalendarId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<CalendarException>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasIndex(x => new { x.CalendarId, x.Date }).IsUnique();
        });

        model.Entity<Service>(e =>
        {
            e.Property(x => x.Number).HasMaxLength(20);
            e.HasIndex(x => x.Number).IsUnique();
            e.HasOne<Route>().WithMany().HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ServiceCalendar>().WithMany().HasForeignKey(x => x.CalendarId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Stops).WithOne().HasForeignKey(s => s.ServiceId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<ScheduledStop>(e =>
        {
            e.Property(x => x.PlannedPlatform).HasMaxLength(10);
            e.HasIndex(x => new { x.ServiceId, x.Sequence }).IsUnique();
            e.HasOne<Station>().WithMany().HasForeignKey(x => x.StationId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<TrainSet>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(20);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasMany(x => x.Coaches).WithOne().HasForeignKey(c => c.TrainSetId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<Coach>().HasIndex(x => new { x.TrainSetId, x.Position }).IsUnique();
    }
}
