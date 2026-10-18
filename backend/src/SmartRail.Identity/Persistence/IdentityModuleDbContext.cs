using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SmartRail.Identity.Model;

namespace SmartRail.Identity.Persistence;

// The Identity module's database access. All tables live in the PostgreSQL schema "identity"; no other module reads them.
// IdentityUserContext is ASP.NET Core Identity's own mapping for users: it supplies the Users table (here StaffAccounts)
// and three tables this module does not use (claims, logins, tokens). They stay empty; removing them would mean
// leaving the documented setup.
internal class IdentityModuleDbContext(DbContextOptions<IdentityModuleDbContext> options)
    : IdentityUserContext<StaffAccount, Guid>(options)
{
    public const string Schema = "identity";

    public DbSet<CabDevice> CabDevices => Set<CabDevice>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<CabSession> CabSessions => Set<CabSession>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema(Schema);
        base.OnModelCreating(model);   // Identity's own tables

        model.Entity<StaffAccount>(e =>
        {
            e.ToTable("StaffAccounts", table =>
                // Domain model D-9: the database itself enforces the scope rule. A station manager has a station; nobody else does.
                table.HasCheckConstraint("CK_StaffAccounts_StationScope", "(\"Role\" = 'StationManager') = (\"StationId\" IS NOT NULL)"));
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);   // stored as its name, like every enum
        });

        model.Entity<CabDevice>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(20);
            e.Property(x => x.TrainSetCode).HasMaxLength(20);
            e.Property(x => x.CredentialHash).HasMaxLength(200);
            e.HasIndex(x => x.Code).IsUnique();   // the seed loader finds a device by its code
        });

        model.Entity<CabSession>(e =>
        {
            e.HasOne<StaffAccount>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<CabDevice>().WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Cascade);

            // R-19 enforced by the database: at most ONE active session (EndedAt empty) per driver and per tablet.
            // A new sign-in ends the old session first; this index catches two sign-ins racing each other.
            e.HasIndex(x => x.AccountId).IsUnique().HasFilter("\"EndedAt\" IS NULL").HasDatabaseName("UX_CabSessions_ActivePerDriver");
            e.HasIndex(x => x.DeviceId).IsUnique().HasFilter("\"EndedAt\" IS NULL").HasDatabaseName("UX_CabSessions_ActivePerDevice");
        });

        model.Entity<AuditEntry>(e =>
        {
            e.Property(x => x.ActorRole).HasMaxLength(20);
            e.Property(x => x.Action).HasMaxLength(50);
            e.Property(x => x.TargetType).HasMaxLength(30);
            e.Property(x => x.Reason).HasMaxLength(50);
            e.Property(x => x.Summary).HasMaxLength(300);
            e.Property(x => x.RequestId).HasMaxLength(100);
            e.HasIndex(x => new { x.TargetType, x.TargetId, x.At });   // "what happened to this journey", newest last
        });

        model.Entity<RefreshToken>(e =>
        {
            e.Property(x => x.TokenHash).HasMaxLength(64);   // SHA-256 as base64 is 44 characters
            e.HasIndex(x => x.TokenHash).IsUnique();         // a refresh looks the token up by its hash
            e.HasIndex(x => x.FamilyId);                     // revoking a session updates every token of the family
            e.HasIndex(x => x.CabSessionId);                 // ending a cab session revokes its tokens
            e.HasOne<StaffAccount>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
