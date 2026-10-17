namespace SmartRail.Network;

// The Network module's third public door (after INetworkSchedule and IStationDirectory): the little LiveOperations needs to
// describe a journey to a passenger. Plain records, no table types; the enum values are given as their names.
public interface INetworkCatalog
{
    // Station names by id. An id that does not exist is left out, so the caller can answer 404.
    Task<IReadOnlyDictionary<Guid, string>> GetStationNamesAsync(IReadOnlyCollection<Guid> stationIds, CancellationToken cancellationToken);

    // What a journey shows about its service: number and train type. An id that does not exist is left out.
    Task<IReadOnlyDictionary<Guid, ServiceDetails>> GetServicesAsync(IReadOnlyCollection<Guid> serviceIds, CancellationToken cancellationToken);

    // A train set with its coaches, or null.
    Task<TrainSetDetails?> GetTrainSetAsync(Guid trainSetId, CancellationToken cancellationToken);
}

public sealed record ServiceDetails(string Number, string TrainType);

public sealed record TrainSetDetails(string Code, string Traction, IReadOnlyList<CoachDetails> Coaches);

public sealed record CoachDetails(int Position, string Class, string Seating);
