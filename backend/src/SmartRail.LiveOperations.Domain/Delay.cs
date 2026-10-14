namespace SmartRail.LiveOperations.Domain;

// Signed whole minutes; negative = early (domain model 4.1).
public readonly record struct Delay(int Minutes)
{
    public bool IsLate => Minutes > 0;
    public bool IsEarly => Minutes < 0;
    public bool IsOnTime => Minutes == 0;

    // R-01: actual - scheduled, whole minutes. Integer division on ticks drops the part-minute, so 59 s late is 0 and
    // 119 s late is 1. It drops towards zero, so 59 s early is also on time. No clock is read: both times are passed in.
    public static Delay Between(DateTime scheduled, DateTime actual) =>
        new((int)((actual - scheduled).Ticks / TimeSpan.TicksPerMinute));
}
