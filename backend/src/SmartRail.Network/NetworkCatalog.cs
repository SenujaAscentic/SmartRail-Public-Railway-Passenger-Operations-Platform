using Microsoft.EntityFrameworkCore;
using SmartRail.Network.Persistence;

namespace SmartRail.Network;

// Answers INetworkCatalog from the network tables. Copies the data into plain records, so callers never see a table.
internal sealed class NetworkCatalog(NetworkDbContext db) : INetworkCatalog
{
    public async Task<IReadOnlyDictionary<Guid, string>> GetStationNamesAsync(IReadOnlyCollection<Guid> stationIds, CancellationToken cancellationToken) =>
        await db.Stations.AsNoTracking()
            .Where(s => stationIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, ServiceDetails>> GetServicesAsync(IReadOnlyCollection<Guid> serviceIds, CancellationToken cancellationToken) =>
        await db.Services.AsNoTracking()
            .Where(s => serviceIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => new ServiceDetails(s.Number, s.TrainType.ToString()), cancellationToken);

    public async Task<TrainSetDetails?> GetTrainSetAsync(Guid trainSetId, CancellationToken cancellationToken)
    {
        var trainSet = await db.TrainSets.AsNoTracking().Include(t => t.Coaches).SingleOrDefaultAsync(t => t.Id == trainSetId, cancellationToken);
        return trainSet is null
            ? null
            : new TrainSetDetails(
                trainSet.Code,
                trainSet.Traction.ToString(),
                trainSet.Coaches.OrderBy(c => c.Position).Select(c => new CoachDetails(c.Position, c.Class.ToString(), c.Seating.ToString())).ToList());
    }
}
