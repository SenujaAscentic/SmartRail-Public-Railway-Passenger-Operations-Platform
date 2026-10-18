using System.Globalization;

namespace SmartRail.LiveOperations.Features.SearchJourneys;

internal sealed record SearchJourneysQuery(Guid From, Guid To, DateOnly Date, DateTime? After, int Limit);

// Reads and checks the query string of GET /journeys/search (contract 3.1): from, to, date, after, limit.
internal static class SearchJourneysRequest
{
    public const int DefaultLimit = 10;
    public const int MaxLimit = 50;

    // `today` is the date in Asia/Colombo; it is the default for `date`.
    public static (SearchJourneysQuery? Query, Dictionary<string, string[]> Errors) Parse(
        string? from, string? to, string? date, string? after, string? limit, DateOnly today)
    {
        var errors = new Dictionary<string, string[]>();

        var fromId = StationId(nameof(from), from, errors);
        var toId = StationId(nameof(to), to, errors);
        if (fromId is not null && fromId == toId)
        {
            errors[nameof(to)] = ["Choose a different destination from the departure station."];
        }

        var day = today;
        if (!string.IsNullOrWhiteSpace(date) && !DateOnly.TryParseExact(date.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day))
        {
            errors[nameof(date)] = ["Give the date as yyyy-MM-dd, for example 2026-10-14."];
        }

        DateTime? afterUtc = null;
        if (!string.IsNullOrWhiteSpace(after))
        {
            // Times are UTC ("…Z"). A time without an offset is taken as UTC too.
            if (DateTimeOffset.TryParse(after.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            {
                afterUtc = parsed.UtcDateTime;
            }
            else
            {
                errors[nameof(after)] = ["Give the time as a UTC date and time, for example 2026-10-14T04:30:00Z."];
            }
        }

        var max = DefaultLimit;
        if (!string.IsNullOrWhiteSpace(limit)
            && (!int.TryParse(limit.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out max) || max is < 1 or > MaxLimit))
        {
            errors[nameof(limit)] = [$"The limit is a whole number from 1 to {MaxLimit}."];
        }

        return errors.Count == 0
            ? (new SearchJourneysQuery(fromId!.Value, toId!.Value, day, afterUtc, max), errors)
            : (null, errors);
    }

    private static Guid? StationId(string field, string? text, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            errors[field] = ["Choose a station."];
            return null;
        }

        if (!Guid.TryParse(text.Trim(), out var id))
        {
            errors[field] = ["The station id is not valid."];
            return null;
        }

        return id;
    }

    // Past departures are hidden (contract 3.1): today from now on, any other day from its start in Colombo.
    public static DateTime AfterUtc(SearchJourneysQuery query, DateOnly today, DateTime nowUtc, TimeZoneInfo timeZone)
    {
        if (query.After is { } after)
        {
            return after;
        }

        return query.Date == today
            ? nowUtc
            : TimeZoneInfo.ConvertTimeToUtc(query.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), timeZone);
    }
}
