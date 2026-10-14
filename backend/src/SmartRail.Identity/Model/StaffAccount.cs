using Microsoft.AspNetCore.Identity;

namespace SmartRail.Identity.Model;

// A staff member. ASP.NET Core Identity supplies the password hash, lockout counters and security stamp;
// UserName holds the staff number ("CTRL-01"). Accounts come from seed data and are never self-registered (ADR-10).
internal class StaffAccount : IdentityUser<Guid>
{
    public string Name { get; set; } = "";

    public StaffRole Role { get; set; }

    // Only for a StationManager: the station they are responsible for. The seed loader looks it up once from the
    // station code, so the sign-in does not need the Network module (station IDs are stable, the network seed updates by code).
    public Guid? StationId { get; set; }

    // A deactivated account cannot sign in or refresh, even with the right password.
    public bool IsActive { get; set; } = true;

    // The ops web app is for the controller and station managers. Drivers sign in on the crew app (a separate endpoint).
    // Computed, not stored: EF Core ignores a property that has no setter.
    public bool MaySignInToOpsWeb => IsActive && Role is StaffRole.Controller or StaffRole.StationManager;
}
