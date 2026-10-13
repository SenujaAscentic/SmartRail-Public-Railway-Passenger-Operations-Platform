using Microsoft.EntityFrameworkCore;
using SmartRail.Network.Persistence;

namespace SmartRail.Network.Features.SearchStations;

internal static class SearchStationsHandler
{
    public const int MaxResults = 10;

    // Stations whose name or code contains the text, ignoring upper/lower case.
    // Names that start with the text come first, then alphabetical order. At most 10 rows.
    public static async Task<List<StationSummary>> HandleAsync(NetworkDbContext db, string query, CancellationToken ct)
    {
        var text = EscapeLikePattern(query.Trim());
        var contains = "%" + text + "%";
        var startsWith = text + "%";

        return await db.Stations
            .AsNoTracking()
            .Where(s => EF.Functions.ILike(s.Name, contains, "\\") || EF.Functions.ILike(s.Code, contains, "\\"))
            .OrderBy(s => EF.Functions.ILike(s.Name, startsWith, "\\") ? 0 : 1)
            .ThenBy(s => s.Name)
            .Take(MaxResults)
            .Select(s => new StationSummary(s.Id, s.Code, s.Name))
            .ToListAsync(ct);
    }

    // The user's text must match literally: "%" and "_" are wildcards in LIKE, so they are escaped with a backslash.
    private static string EscapeLikePattern(string text) =>
        text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
