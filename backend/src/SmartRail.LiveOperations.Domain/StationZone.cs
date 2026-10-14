namespace SmartRail.LiveOperations.Domain;

// Where the train is relative to one station's zone. Outside = not entered yet; Finished = Departed or Passed was reported.
public enum ZonePhase { Outside, Inside, Arrived, Finished }

public enum ZoneEvent { None, Arrived, Departed, Passed }

// What the detector remembers between positions. SlowSince is when the current unbroken slow spell inside the zone began.
public sealed record ZoneState(ZonePhase Phase, DateTime? SlowSince)
{
    public static ZoneState Start { get; } = new(ZonePhase.Outside, null);
}

// One accepted position, already reduced to what the rule needs. The caller measures the distance to the station
// (R-07 / R-08 bring the geometry); keeping it out of here keeps this rule free of maps.
public readonly record struct ZoneSample(DateTime Time, double DistanceMetres, double SpeedKmh);

public readonly record struct ZoneStep(ZoneState State, ZoneEvent Event);

public static class StationZone
{
    public const double RadiusMetres = 300;
    public const double SlowSpeedKmh = 5;
    public static readonly TimeSpan SlowFor = TimeSpan.FromSeconds(20);

    // R-04. A pure function (old state + one sample -> new state + event), so it needs no clock and is easy to test.
    // Samples must arrive oldest first; discarding older ones is R-08.
    public static ZoneStep Observe(ZoneState state, ZoneSample sample)
    {
        var inside = sample.DistanceMetres <= RadiusMetres;

        switch (state.Phase)
        {
            case ZonePhase.Outside when !inside:
                return new ZoneStep(state, ZoneEvent.None);

            case ZonePhase.Outside:
            case ZonePhase.Inside:
                if (!inside)
                {
                    return new ZoneStep(new ZoneState(ZonePhase.Finished, null), ZoneEvent.Passed);   // never stopped
                }

                // The slow spell starts at the first slow sample and breaks as soon as the train is not slow.
                var slowSince = sample.SpeedKmh < SlowSpeedKmh ? state.SlowSince ?? sample.Time : (DateTime?)null;
                if (slowSince is not null && sample.Time - slowSince.Value >= SlowFor)
                {
                    return new ZoneStep(new ZoneState(ZonePhase.Arrived, null), ZoneEvent.Arrived);
                }
                return new ZoneStep(new ZoneState(ZonePhase.Inside, slowSince), ZoneEvent.None);

            case ZonePhase.Arrived when !inside:
                return new ZoneStep(new ZoneState(ZonePhase.Finished, null), ZoneEvent.Departed);

            default:   // Arrived and still inside, or Finished: nothing new to report
                return new ZoneStep(state, ZoneEvent.None);
        }
    }
}
