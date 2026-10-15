namespace SmartRail.LiveOperations.Domain;

public readonly record struct GeoPoint(double Latitude, double Longitude)
{
    private const double EarthRadiusMetres = 6_371_000;

    // Great-circle (haversine) distance. Accurate to well under 1% over the few km between two GPS fixes, which is
    // far better than the 500 m and 120 km/h limits it is compared with.
    public double DistanceMetresTo(GeoPoint other)
    {
        var lat1 = ToRadians(Latitude);
        var lat2 = ToRadians(other.Latitude);
        var dLat = lat2 - lat1;
        var dLon = ToRadians(other.Longitude - Longitude);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * EarthRadiusMetres * Math.Asin(Math.Sqrt(a));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}

// The last position the journey accepted, with its corrected time (R-09).
public readonly record struct AcceptedPosition(GeoPoint Location, DateTime Time);

public enum PositionVerdict { Accepted, OlderThanLastAccepted, OffRoute, ImpliedSpeedTooHigh }

public static class PositionPlausibility
{
    public const double MaxDistanceFromRouteMetres = 500;
    public const double MaxSpeedKmh = 120;

    // R-08. The caller measures distanceFromRouteMetres (the route lives in the Network module); this rule only judges it.
    // Checks run cheapest first; the first failure is the reason the position is counted and logged as discarded.
    public static PositionVerdict Check(AcceptedPosition? last, GeoPoint location, DateTime time, double distanceFromRouteMetres)
    {
        // "Not newer" rather than "older": an equal time is a duplicate, and would make the speed below divide by zero.
        if (last is { } previous && time <= previous.Time)
        {
            return PositionVerdict.OlderThanLastAccepted;
        }

        if (distanceFromRouteMetres > MaxDistanceFromRouteMetres)
        {
            return PositionVerdict.OffRoute;
        }

        if (last is { } before)
        {
            var seconds = (time - before.Time).TotalSeconds;
            var impliedKmh = before.Location.DistanceMetresTo(location) / seconds * 3.6;
            if (impliedKmh > MaxSpeedKmh)
            {
                return PositionVerdict.ImpliedSpeedTooHigh;
            }
        }

        return PositionVerdict.Accepted;
    }
}

public static class ClockCorrection
{
    // R-09. clockOffset = server time minus device time, measured once at cab sign-in. GPS time is already correct,
    // so it is used as it is; without it the tablet's clock is corrected by the offset.
    public static DateTime EventTime(DateTime? gpsTime, DateTime deviceTime, TimeSpan clockOffset) =>
        gpsTime ?? deviceTime + clockOffset;
}
