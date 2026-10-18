namespace SmartRail.Identity;

// What a module reports for the audit trail. `Reason` is a code, `Summary` a short sentence made of ids, codes and numbers: no names,
// no free-text notes (security rules: no personal data in logs).
public sealed record AuditRecord(
    DateTime At, Guid ActorId, string ActorRole, string Action, string TargetType, Guid TargetId,
    string? Reason, string Summary, string RequestId);

// Identity's public door for the audit trail (architecture Q3). It can only ADD an entry: there is deliberately no way to change or
// remove one (the database user's insert-only rights, domain model O-10, are a deployment matter; the code never needs more).
public interface IAuditLog
{
    Task AppendAsync(AuditRecord record, CancellationToken cancellationToken);
}
