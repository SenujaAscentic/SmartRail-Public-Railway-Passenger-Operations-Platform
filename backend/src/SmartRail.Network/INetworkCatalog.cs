namespace SmartRail.Network;

// The Network module's third public door (after INetworkSchedule and IStationDirectory): the little LiveOperations needs to
// describe a journey to a passenger. Plain records, no table types; the enum values are given as their names.
public interface INetworkCatalog
{
    // Station names by id. An id that does not exist is left out, so the caller can answer 404.
    Task<IReadOnlyDictionary<Guid, string>> GetStationNamesAsync(IReadOnlyCollection<Guid> stationIds, CancellationToken cancellationToken);

    // What a journey shows about its service: number and train type. An id that does not exist is left out.
    Task<IReadOnlyDictionary<Guid, ServiceDetails>> GetServicesAsync(IReadOnlyCollection<Guid> serviceIds, CancellationToken cancellationToken);

    // The stations of a service's route in order, with their coordinates, for judging where a GPS position is (R-08) and how far
    // along the route the train is. Empty if the service is unknown. The route is these stations joined by straight lines: the seed
    // has no track shape (logged 18 Oct), which is fine for the simulator and for short sections.
    Task<IReadOnlyList<RoutePoint>> GetRouteAsync(Guid serviceId, CancellationToken cancellationToken);

    // The platforms of a station with their number and whether they are in service (R-13). Empty if the station is unknown.
    Task<IReadOnlyList<PlatformDetails>> GetPlatformsAsync(Guid stationId, CancellationToken cancellationToken);

    // The stations that have catering (the seed marks them), by id and code. The code is also the provider's reference of the station in the MVP.
    Task<IReadOnlyList<CateringStationRef>> GetCateringStationsAsync(CancellationToken cancellationToken);

    // A train set with its coaches, or null.
    Task<TrainSetDetails?> GetTrainSetAsync(Guid trainSetId, CancellationToken cancellationToken);
}

public sealed record RoutePoint(Guid StationId, double Latitude, double Longitude);

public sealed record ServiceDetails(string Number, string TrainType);

public sealed record PlatformDetails(string Number, bool InService);

public sealed record CateringStationRef(Guid StationId, string Code, string Name);

public sealed record TrainSetDetails(string Code, string Traction, IReadOnlyList<CoachDetails> Coaches);

public sealed record CoachDetails(int Position, string Class, string Seating);
