using System.Globalization;
using SmartRail.LiveOperations.Domain;

namespace SmartRail.LiveOperations.Features.CrewPositions;

// One GPS fix from the cab tablet (contract 3.4): where, how fast, how exact, and when. gpsTime is the time the GPS gave; deviceTime
// is the tablet's own clock (R-09 uses the GPS time when there is one, otherwise the device time corrected by the measured offset).
public sealed record PositionRequest(double? Lat, double? Lng, double? SpeedKmh, double? AccuracyM, DateTimeOffset? GpsTime, DateTimeOffset? DeviceTime);

// A batch of fixes, sent every few seconds, or all at once after a tunnel. The clientActionId makes sending it twice harmless.
public sealed record PositionBatchRequest(Guid? ClientActionId, List<PositionRequest>? Positions);

// A fix that was not used and why: OlderThanLastAccepted, OffRoute or ImpliedSpeedTooHigh (R-08). `Index` is its place in the request.
public sealed record DiscardedPosition(int Index, string Reason);

public sealed record PositionBatchResult(int Accepted, IReadOnlyList<DiscardedPosition> Discarded);

// A fix that passed the checks on its shape.
internal sealed record ValidPosition(int Index, GeoPoint Location, double SpeedKmh, DateTime? GpsTime, DateTime DeviceTime);

internal static class PositionBatchValidator
{
    public const int MaxPositions = 100;
    private const double MaxSpeedKmh = 400;   // far above any train; only keeps nonsense out

    // The shape of the request, nothing about trains: the Domain judges plausibility (R-08).
    public static (List<ValidPosition>? Positions, Dictionary<string, string[]> Errors) Validate(PositionBatchRequest? request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request?.ClientActionId is null || request.ClientActionId == Guid.Empty)
        {
            errors["clientActionId"] = ["Send a clientActionId (a new UUID for each batch)."];
        }

        var positions = request?.Positions;
        if (positions is null || positions.Count == 0)
        {
            errors["positions"] = ["Send at least one position."];
        }
        else if (positions.Count > MaxPositions)
        {
            errors["positions"] = [$"Send at most {MaxPositions} positions in one batch."];
        }
        else
        {
            for (var i = 0; i < positions.Count; i++)
            {
                CheckPosition(positions[i], i, errors);
            }
        }

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        return (positions!.Select((p, i) => new ValidPosition(
            i, new GeoPoint(p.Lat!.Value, p.Lng!.Value), p.SpeedKmh!.Value, p.GpsTime?.UtcDateTime, p.DeviceTime!.Value.UtcDateTime)).ToList(), errors);
    }

    private static void CheckPosition(PositionRequest p, int index, Dictionary<string, string[]> errors)
    {
        void Error(string field, string message) => errors[string.Create(CultureInfo.InvariantCulture, $"positions[{index}].{field}")] = [message];

        if (p.Lat is not { } lat || lat is < -90 or > 90 || double.IsNaN(lat)) Error("lat", "Latitude is a number from -90 to 90.");
        if (p.Lng is not { } lng || lng is < -180 or > 180 || double.IsNaN(lng)) Error("lng", "Longitude is a number from -180 to 180.");
        if (p.SpeedKmh is not { } speed || speed < 0 || speed > MaxSpeedKmh || double.IsNaN(speed)) Error("speedKmh", $"Speed is a number from 0 to {MaxSpeedKmh} km/h.");
        if (p.AccuracyM is < 0) Error("accuracyM", "Accuracy cannot be negative.");
        if (p.DeviceTime is null) Error("deviceTime", "Send the tablet's own time.");
    }
}
