using SmartRail.LiveOperations;

namespace SmartRail.CateringIntegration.Relay;

// What the provider is told (domain model 8.3, US-F2). The event as the provider receives it, before it is signed.
internal sealed record ProviderEvent(
    Guid EventId, string Type, DateTime OccurredAt, string ServiceNumber, DateOnly ServiceDate, string StationRef,
    DateTime? ScheduledArrival, DateTime? ExpectedArrival, string? Change, string? Reason)
{
    public const string EtaUpdated = "provider.eta-updated";
    public const string JourneyChanged = "provider.journey-changed";
}

// Which provider events a journey event causes. Pure, so it is unit-tested. Only catering stations count (US-F2 AC1), and no passenger
// data is involved (AC5). The journey's state NOW is used, not the payload of the event: an event says "something changed", and the
// provider wants what is true.
internal static class ProviderEventRules
{
    // ADR-07: an ETA is sent only when it differs by at least this much from the last one the provider was told.
    public static readonly TimeSpan MinimumChange = TimeSpan.FromMinutes(1);

    public const string DelayChanged = "DelayChanged";
    public const string JourneyCancelled = "JourneyCancelled";
    public const string JourneyTerminatedEarly = "JourneyTerminatedEarly";

    public static bool IsRelevant(string eventType) => eventType is DelayChanged or JourneyCancelled or JourneyTerminatedEarly;

    // `cateringRefs`: station id -> the provider's reference (the station code). `lastSent`: station ref -> the ETA last told for this journey.
    public static IReadOnlyList<ProviderEvent> Create(
        JourneyFeedEvent source, JourneyState state, string serviceNumber, IReadOnlyDictionary<Guid, string> cateringRefs,
        IReadOnlyDictionary<string, DateTime> lastSent, Func<Guid> newId)
    {
        ProviderEvent Event(string type, string stationRef, DateTime? scheduled, DateTime? expected, string? change, string? reason) =>
            new(newId(), type, source.OccurredAt, serviceNumber, state.ServiceDate, stationRef, scheduled, expected, change, reason);

        var catering = state.Calls.Where(c => c.Stops && cateringRefs.ContainsKey(c.StationId)).ToList();

        switch (source.Type)
        {
            case DelayChanged:
                // A stop the train has not reached yet. The provider assumes the schedule until told otherwise.
                return catering
                    .Where(c => c.Status == "Scheduled" && c.ExpectedArrival is not null && c.ScheduledArrival is not null)
                    .Select(c => (Call: c, Ref: cateringRefs[c.StationId]))
                    .Where(x => (x.Call.ExpectedArrival!.Value - (lastSent.TryGetValue(x.Ref, out var told) ? told : x.Call.ScheduledArrival!.Value)).Duration() >= MinimumChange)
                    .Select(x => Event(ProviderEvent.EtaUpdated, x.Ref, x.Call.ScheduledArrival, x.Call.ExpectedArrival, null, null))
                    .ToList();

            case JourneyCancelled when state.Status == "Cancelled":
                return Changed("Cancelled");

            case JourneyTerminatedEarly when state.Status == "TerminatedEarly":
                return Changed("Terminated");

            default:
                return [];   // another event type, or the journey has changed again since (reinstated): nothing to say
        }

        // The catering stops the train will no longer reach: the controller skipped them by cancelling or by ending the journey early.
        IReadOnlyList<ProviderEvent> Changed(string change) => catering
            .Where(c => c.Status == "Skipped")
            .Select(c => Event(ProviderEvent.JourneyChanged, cateringRefs[c.StationId], c.ScheduledArrival, c.ExpectedArrival, change, ReasonText(state)))
            .ToList();
    }

    // "TrainFault: Engine failure" or just the code. Plain text; the provider shows it as text only.
    public static string? ReasonText(JourneyState state) =>
        state.ReasonCode is null ? null : state.ReasonNote is { Length: > 0 } note ? $"{state.ReasonCode}: {note}" : state.ReasonCode;
}
