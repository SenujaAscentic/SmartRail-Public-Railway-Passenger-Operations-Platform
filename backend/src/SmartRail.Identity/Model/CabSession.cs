namespace SmartRail.Identity.Model;

// One driver signed in on one cab tablet (ADR-05). Identity owns it (domain model D-14) because the token is only
// as good as the session: every request that carries `crewSessionId` is checked against this row.
// The database allows ONE active session per driver and ONE per tablet (R-19, answer 8 of 15 Oct); a new sign-in ends the old one.
internal class CabSession
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid DeviceId { get; set; }
    public DateTimeOffset StartedAt { get; set; }

    // Null while the session is active. Set by a newer sign-in, by sign-out (the app signs out after End journey, for now),
    // or never (the refresh token's 12 hours then end it).
    public DateTimeOffset? EndedAt { get; set; }

    // Server time minus the tablet's clock at sign-in, in milliseconds (R-09): event time = device time + offset.
    public long ClockOffsetMs { get; set; }
}
