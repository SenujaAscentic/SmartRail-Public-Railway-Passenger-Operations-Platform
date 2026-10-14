namespace SmartRail.Identity.Model;

// The three staff roles of the MVP (CLAUDE.md section 2; the guard role is deferred, domain model D-12).
// One role per person, so it is a column, not Identity's role tables. Stored and sent as its name ("StationManager"), never as a number.
public enum StaffRole
{
    Controller,
    StationManager,
    Driver,
}
