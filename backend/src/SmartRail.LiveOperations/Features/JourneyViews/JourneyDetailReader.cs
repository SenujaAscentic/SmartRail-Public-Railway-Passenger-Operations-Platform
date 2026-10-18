using Microsoft.Extensions.Logging;
using SmartRail.LiveOperations.Domain;
using SmartRail.Network;

namespace SmartRail.LiveOperations.Features.JourneyViews;

// Builds the public journey detail for a loaded journey: asks Network for the names, the service and the train set, then lets
// JourneyViewBuilder do the (pure) work. Used by GET /journeys/{id} and by every controller decision, whose answer is the same
// detail (so the ops web shows exactly what the passengers see, with the new version).
internal sealed class JourneyDetailReader(INetworkCatalog catalog, ILogger<JourneyDetailReader> log)
{
    // `tolerant`: the stream snapshot must not fail for every client because one journey points at something Network does not have.
    // It then shows empty names and "?" for the service, and the fault is logged (ids only). GET /journeys/{id} stays strict.
    public async Task<JourneyDetailResponse> BuildAsync(OperationalJourney journey, CancellationToken ct, bool tolerant = false)
    {
        var names = await catalog.GetStationNamesAsync(journey.Stops.Select(s => s.StationId).ToList(), ct);
        var service = (await catalog.GetServicesAsync([journey.ServiceId], ct)).GetValueOrDefault(journey.ServiceId);
        if (tolerant && (service is null || journey.Stops.Any(s => !names.ContainsKey(s.StationId))))
        {
            log.LogWarning("Journey {JourneyId} refers to a service or station that is not in the network; its snapshot shows empty names.", journey.Id);
            names = journey.Stops.ToDictionary(s => s.StationId, s => names.GetValueOrDefault(s.StationId, ""));
            service ??= new ServiceDetails("?", "?");
        }

        if (service is null || journey.Stops.Any(s => !names.ContainsKey(s.StationId)))
        {
            // The journey points at a service or station that Network no longer has: a data fault, not something to guess around.
            throw new InvalidOperationException($"Journey {journey.Id} refers to a service or station that is not in the network.");
        }

        var trainSet = await catalog.GetTrainSetAsync(journey.TrainSetId, ct);
        return JourneyViewBuilder.Detail(journey, names, service, trainSet);
    }
}
