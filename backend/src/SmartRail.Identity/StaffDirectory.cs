using Microsoft.EntityFrameworkCore;
using SmartRail.Identity.Model;
using SmartRail.Identity.Persistence;

namespace SmartRail.Identity;

// Answers IStaffDirectory from the accounts table. Account ids stay the same across restarts because the seed loader finds each
// account by its staff number and updates it (it never recreates it).
internal sealed class StaffDirectory(IdentityModuleDbContext db) : IStaffDirectory
{
    public async Task<IReadOnlyDictionary<string, Guid>> GetDriverIdsAsync(IReadOnlyCollection<string> staffNumbers, CancellationToken cancellationToken) =>
        await db.Users
            .AsNoTracking()
            .Where(u => staffNumbers.Contains(u.UserName!) && u.Role == StaffRole.Driver && u.IsActive)
            .ToDictionaryAsync(u => u.UserName!, u => u.Id, cancellationToken);
}
