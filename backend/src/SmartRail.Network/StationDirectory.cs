using Microsoft.EntityFrameworkCore;
using SmartRail.Network.Persistence;

namespace SmartRail.Network;

// Answers IStationDirectory from the stations table. Station IDs stay the same across restarts
// because the seed loader finds each station by its code and updates it (it never recreates it).
internal sealed class StationDirectory(NetworkDbContext db) : IStationDirectory
{
    public async Task<IReadOnlyDictionary<string, Guid>> GetStationIdsAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken) =>
        await db.Stations
            .AsNoTracking()
            .Where(s => codes.Contains(s.Code))
            .ToDictionaryAsync(s => s.Code, s => s.Id, cancellationToken);
}
