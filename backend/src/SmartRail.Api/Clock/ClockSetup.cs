using System.Globalization;

namespace SmartRail.Api.Clock;

// Which clock the application runs on: the real one, or (Development only) the demo clock from the settings
//   Clock:Start   the moment the application should believe it is when it starts, for example 2026-10-20T05:55:00+05:30
//   Clock:Speed   how fast time runs from there; 1 (the default) is real speed, anything else is for rehearsals and tests
// Outside Development these settings are not ignored but REFUSED (ClockGuard stops the start): a production system must never be
// able to run on a shifted clock, by accident or on purpose.
internal static class ClockSetup
{
    public const double MaxSpeed = 100;

    public static bool IsConfigured(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["Clock:Start"]) || !string.IsNullOrWhiteSpace(configuration["Clock:Speed"]);

    // Throws when a Clock:* setting is present outside Development, or is not usable.
    public static TimeProvider Create(IHostEnvironment environment, IConfiguration configuration) =>
        Create(environment, configuration, TimeProvider.System);

    public static TimeProvider Create(IHostEnvironment environment, IConfiguration configuration, TimeProvider real)
    {
        if (!IsConfigured(configuration))
        {
            return real;
        }

        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                $"The settings Clock:Start and Clock:Speed move the application's clock and are allowed only in the Development environment, not in {environment.EnvironmentName}. Remove them.");
        }

        var startText = configuration["Clock:Start"];
        var start = real.GetUtcNow();
        if (!string.IsNullOrWhiteSpace(startText)
            && !DateTimeOffset.TryParse(startText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out start))
        {
            throw new InvalidOperationException($"Clock:Start is not a date and time with an offset (for example 2026-10-20T05:55:00+05:30): '{startText}'.");
        }

        var speed = 1.0;
        var speedText = configuration["Clock:Speed"];
        if (!string.IsNullOrWhiteSpace(speedText)
            && (!double.TryParse(speedText, NumberStyles.Float, CultureInfo.InvariantCulture, out speed) || speed <= 0 || speed > MaxSpeed))
        {
            throw new InvalidOperationException($"Clock:Speed must be a number above 0 and at most {MaxSpeed}: '{speedText}'.");
        }

        return new DemoClock(start, speed, real);
    }
}

// Runs first when the application starts and refuses a Clock:* setting outside Development. A separate check from creating the clock,
// so it holds even when something replaces the registered TimeProvider (tests do).
internal sealed class ClockGuard(IHostEnvironment environment, IConfiguration configuration) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = ClockSetup.Create(environment, configuration);   // throws if the settings are not allowed or not usable
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
