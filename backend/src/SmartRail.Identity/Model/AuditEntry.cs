namespace SmartRail.Identity.Model;

// One row of the audit trail (domain model section 7, US-O10): who did what to which thing, and when. Append-only: nothing in the
// application updates or deletes a row (IAuditLog has no such method). Names, notes and other personal data are never written here;
// the actor is an id.
internal class AuditEntry
{
    public Guid Id { get; set; }
    public DateTime At { get; set; }
    public Guid ActorId { get; set; }
    public string ActorRole { get; set; } = "";
    public string Action { get; set; } = "";
    public string TargetType { get; set; } = "";
    public Guid TargetId { get; set; }
    public string? Reason { get; set; }
    public string Summary { get; set; } = "";
    public string RequestId { get; set; } = "";
}
