using SmartRail.LiveOperations.Domain;
using SmartRail.Network;

namespace SmartRail.LiveOperations.Features.JourneyViews;

// Builds the public journey detail for a loaded journey: asks Network for the names, the service and the train set, then lets
// JourneyViewBuilder do the (pure) work. Used by GET /journeys/{id} and by every controller decision, whose answer is the same
// detail (so the ops web shows exactly what the passengers see, with the new version).
internal sealed class JourneyDetailReader(INetworkCatalog catalog)
{
    public async Task<JourneyDetailResponse> BuildAsync(OperationalJourney journey, CancellationToken ct)
    {
        var names = await catalog.GetStationNamesAsync(journey.Stops.Select(s => s.StationId).ToList(), ct);
        var service = (await catalog.GetServicesAsync([journey.ServiceId], ct)).GetValueOrDefault(journey.ServiceId);
        if (service is null || journey.Stops.Any(s => !names.ContainsKey(s.StationId)))
        {
            // The journey points at a service or station that Network no longer has: a data fault, not something to guess around.
            throw new InvalidOperationException($"Journey {journey.Id} refers to a service or station that is not in the network.");
        }

        var trainSet = await catalog.GetTrainSetAsync(journey.TrainSetId, ct);
        return JourneyViewBuilder.Detail(journey, names, service, trainSet);
    }
}
