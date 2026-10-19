namespace SmartRail.CateringIntegration.Menus;

// The small rules of the catering answer. Pure (time is passed in), so they are unit-tested.
internal static class CateringRules
{
    // Colombo has no daylight saving, so a fixed offset is exact. Opening hours are a time of day in Colombo (contract 3.6): a daily
    // time, not an instant, which is the one deliberate exception to "UTC everywhere".
    public static readonly TimeSpan ColomboOffset = TimeSpan.FromHours(5.5);

    // A menu not refreshed for this long (three missed 5-minute pulls) is no longer shown: stale prices and "available" flags could mislead.
    public static readonly TimeSpan MaxCacheAge = TimeSpan.FromMinutes(15);

    // Is the hotline open at `now`? Opens at `opens`, closed from `closes`. A closing time before the opening time means it runs past midnight.
    public static bool IsOpenNow(DateTimeOffset now, string opens, string closes)
    {
        if (!TimeOnly.TryParseExact(opens, "HH:mm", out var from) || !TimeOnly.TryParseExact(closes, "HH:mm", out var to))
        {
            return false;   // unreadable hours: do not claim it is open
        }

        var local = TimeOnly.FromDateTime(now.ToOffset(ColomboOffset).DateTime);
        return from <= to ? local >= from && local < to : local >= from || local < to;
    }

    // The menu can be shown when it is recent enough and the provider says it is taking orders there.
    public static bool IsAvailable(DateTime fetchedAtUtc, bool providerAvailable, DateTimeOffset now) =>
        providerAvailable && now.UtcDateTime - fetchedAtUtc <= MaxCacheAge;
}
