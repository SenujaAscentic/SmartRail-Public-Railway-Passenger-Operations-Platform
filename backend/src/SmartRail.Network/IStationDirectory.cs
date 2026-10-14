namespace SmartRail.Network;

// A second public door of the Network module, kept apart from INetworkSchedule (which is the whole plan).
// Identity uses it once, while seeding staff, to turn a station code ("FOT") into the station's ID.
public interface IStationDirectory
{
    // Returns the ID for every code that exists. A code that does not exist is left out, so the caller can report it.
    Task<IReadOnlyDictionary<string, Guid>> GetStationIdsAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken);
}
