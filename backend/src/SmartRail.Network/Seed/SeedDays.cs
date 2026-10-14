namespace SmartRail.Network.Seed;

// The day names used in calendars.json.
internal static class SeedDays
{
    public static readonly IReadOnlyDictionary<string, DayOfWeek> ByName = new Dictionary<string, DayOfWeek>
    {
        ["Mon"] = DayOfWeek.Monday,
        ["Tue"] = DayOfWeek.Tuesday,
        ["Wed"] = DayOfWeek.Wednesday,
        ["Thu"] = DayOfWeek.Thursday,
        ["Fri"] = DayOfWeek.Friday,
        ["Sat"] = DayOfWeek.Saturday,
        ["Sun"] = DayOfWeek.Sunday,
    };
}
