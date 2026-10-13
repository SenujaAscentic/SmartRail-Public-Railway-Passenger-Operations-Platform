namespace SmartRail.Network.Features.SearchStations;

internal static class SearchStationsValidator
{
    public const int MaxQueryLength = 50;

    // Returns the message to show for the "query" field, or null when the query is acceptable.
    public static string? Validate(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "Enter at least one letter to search for a station.";
        }

        if (query.Trim().Length > MaxQueryLength)
        {
            return $"The search text can be at most {MaxQueryLength} characters.";
        }

        return null;
    }
}
