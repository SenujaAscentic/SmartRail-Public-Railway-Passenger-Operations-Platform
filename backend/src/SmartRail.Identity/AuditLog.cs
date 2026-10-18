using SmartRail.Identity.Model;
using SmartRail.Identity.Persistence;

namespace SmartRail.Identity;

internal sealed class AuditLog(IdentityModuleDbContext db) : IAuditLog
{
    public async Task AppendAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        db.AuditEntries.Add(new AuditEntry
        {
            Id = Guid.CreateVersion7(),
            At = record.At,
            ActorId = record.ActorId,
            ActorRole = record.ActorRole,
            Action = record.Action,
            TargetType = record.TargetType,
            TargetId = record.TargetId,
            Reason = record.Reason,
            Summary = record.Summary,
            RequestId = record.RequestId,
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
