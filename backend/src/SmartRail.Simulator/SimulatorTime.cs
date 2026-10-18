using System.Diagnostics;

namespace SmartRail.Simulator;

// The simulator's clock. The run is driven by this, so the same code can run for a demonstration (real waiting) and in a test (none).
public interface ISimulatorTime
{
    // "Now" on the tablet: the server's time at sign-in plus the simulated time that has passed.
    DateTimeOffset Now { get; }

    // Lets `simulated` time pass.
    Task WaitAsync(TimeSpan simulated, CancellationToken cancellationToken);
}

// Waits for real: one simulated second takes 1 / speed real seconds. With the server's demo clock set to the same speed the two agree.
public sealed class RealSimulatorTime(DateTimeOffset serverNowAtStart, double speed) : ISimulatorTime
{
    private readonly long _startTimestamp = Stopwatch.GetTimestamp();

    public DateTimeOffset Now => serverNowAtStart + Stopwatch.GetElapsedTime(_startTimestamp) * speed;

    public Task WaitAsync(TimeSpan simulated, CancellationToken cancellationToken) => Task.Delay(simulated / speed, cancellationToken);
}

// Does not wait: time just moves forward by what was asked. For tests, where a four-hour run must take seconds.
public sealed class InstantSimulatorTime(DateTimeOffset start) : ISimulatorTime
{
    public DateTimeOffset Now { get; private set; } = start;

    public Task WaitAsync(TimeSpan simulated, CancellationToken cancellationToken)
    {
        Now += simulated;
        return Task.CompletedTask;
    }
}
