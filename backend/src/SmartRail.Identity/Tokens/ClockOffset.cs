namespace SmartRail.Identity.Tokens;

// R-09: measures how far a cab tablet's clock is from the server's, once, at sign-in.
// Later device times are corrected by this offset (event time = device time + offset) when GPS time is missing.
internal static class ClockOffset
{
    // A tablet more than a day off is broken or being tampered with; refusing it also keeps the number small and sane.
    public static readonly TimeSpan MaxAbsolute = TimeSpan.FromHours(24);

    // The offset is server minus device: positive when the tablet runs slow. The network delay of this one request is ignored.
    public static bool TryMeasure(DateTimeOffset serverTime, DateTimeOffset deviceTime, out long offsetMs)
    {
        var offset = serverTime - deviceTime;
        offsetMs = (long)offset.TotalMilliseconds;
        return offset.Duration() <= MaxAbsolute;
    }
}
