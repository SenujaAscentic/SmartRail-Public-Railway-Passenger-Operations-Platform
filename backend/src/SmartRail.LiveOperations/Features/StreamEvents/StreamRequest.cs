using System.Globalization;
using SmartRail.LiveOperations.Stream;

namespace SmartRail.LiveOperations.Features.StreamEvents;

// Reads and checks the query string and the Last-Event-ID header of GET /stream (contract section 4).
internal static class StreamRequest
{
    // A cap on each list: it bounds the snapshot query and stops one request from asking for the whole network (T-10).
    public const int MaxIdsPerList = 50;

    // Returns the filter, or the field errors (contract section 6: VALIDATION_FAILED with errors per field).
    public static (StreamFilter? Filter, Dictionary<string, string[]> Errors) Parse(string? journeyIds, string? stationIds, string? lineIds)
    {
        var errors = new Dictionary<string, string[]>();
        var journeys = ParseList(nameof(journeyIds), journeyIds, errors);
        var stations = ParseList(nameof(stationIds), stationIds, errors);
        var lines = ParseList(nameof(lineIds), lineIds, errors);

        if (errors.Count == 0 && journeys.Count == 0 && stations.Count == 0 && lines.Count == 0)
        {
            errors["filters"] = ["Give at least one of journeyIds, stationIds or lineIds."];
        }

        return errors.Count == 0 ? (new StreamFilter(journeys, stations, lines), errors) : (null, errors);
    }

    // Digits only. A missing, empty or odd header means the client has no usable last event: it is treated as a fresh connection.
    public static long? ParseLastEventId(string? header) =>
        long.TryParse(header?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;

    private static HashSet<Guid> ParseList(string field, string? text, Dictionary<string, string[]> errors)
    {
        var ids = new HashSet<Guid>();
        var items = (text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (items.Length > MaxIdsPerList)
        {
            errors[field] = [$"Give at most {MaxIdsPerList} ids."];
            return ids;
        }

        foreach (var item in items)
        {
            if (!Guid.TryParse(item, out var id))
            {
                errors[field] = ["Every id must be a GUID."];   // the bad value is not echoed back
                return [];
            }

            ids.Add(id);
        }

        return ids;
    }
}
