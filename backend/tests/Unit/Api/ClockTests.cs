using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Time.Testing;
using SmartRail.Api.Clock;
using Xunit;

namespace SmartRail.UnitTests.Api;

// The demo clock (Development only): where it starts, how fast it runs, and that nothing outside Development can turn it on.
public class ClockTests
{
    private static readonly DateTimeOffset RealNow = new(2026, 10, 18, 9, 0, 0, TimeSpan.FromHours(5.5));
    private static readonly DateTimeOffset DemoStart = new(2026, 10, 20, 5, 55, 0, TimeSpan.FromHours(5.5));

    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static IConfiguration Settings(string? start = null, string? speed = null)
    {
        var values = new Dictionary<string, string?>();
        if (start is not null) values["Clock:Start"] = start;
        if (speed is not null) values["Clock:Speed"] = speed;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    // ---- the clock ----

    [Fact]
    public void The_demo_clock_starts_at_the_chosen_moment_and_then_runs_at_real_speed()
    {
        var real = new FakeTimeProvider(RealNow);
        var demo = new DemoClock(DemoStart, speed: 1, real);

        Assert.Equal(DemoStart, demo.GetUtcNow());
        real.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(DemoStart.AddMinutes(5), demo.GetUtcNow());
    }

    // The start is written with an offset (+05:30, Colombo), but a TimeProvider's GetUtcNow() is UTC: the database refuses any other offset.
    [Fact]
    public void The_demo_clock_always_answers_in_UTC_whatever_offset_the_start_was_written_with()
    {
        var demo = new DemoClock(DemoStart, speed: 1, new FakeTimeProvider(RealNow));

        Assert.Equal(TimeSpan.Zero, demo.GetUtcNow().Offset);
        Assert.Equal(new DateTimeOffset(2026, 10, 20, 0, 25, 0, TimeSpan.Zero), demo.GetUtcNow());
    }

    [Fact]
    public void At_speed_10_one_real_second_is_ten_seconds_of_demo_time()
    {
        var real = new FakeTimeProvider(RealNow);
        var demo = new DemoClock(DemoStart, speed: 10, real);

        real.Advance(TimeSpan.FromSeconds(3));

        Assert.Equal(DemoStart.AddSeconds(30), demo.GetUtcNow());
    }

    [Fact]
    public void A_timer_fires_when_the_demo_clock_has_moved_by_its_period_not_the_real_one()
    {
        var real = new FakeTimeProvider(RealNow);
        var demo = new DemoClock(DemoStart, speed: 10, real);
        var fired = 0;
        using var timer = demo.CreateTimer(_ => fired++, null, dueTime: TimeSpan.FromSeconds(30), period: Timeout.InfiniteTimeSpan);   // 30 demo seconds

        real.Advance(TimeSpan.FromSeconds(2.9));
        Assert.Equal(0, fired);
        real.Advance(TimeSpan.FromSeconds(0.2));
        Assert.Equal(1, fired);
    }

    [Fact]
    public async Task A_periodic_timer_on_the_demo_clock_ticks_every_period_of_demo_time()
    {
        var real = new FakeTimeProvider(RealNow);
        var demo = new DemoClock(DemoStart, speed: 5, real);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15), demo);   // every 15 demo seconds = 3 real seconds

        var tick = timer.WaitForNextTickAsync(TestContext.Current.CancellationToken).AsTask();
        real.Advance(TimeSpan.FromSeconds(2.9));
        Assert.False(tick.IsCompleted);
        real.Advance(TimeSpan.FromSeconds(0.2));

        Assert.True(await tick.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Elapsed_time_measurement_stays_real()
    {
        var real = new FakeTimeProvider(RealNow);
        var demo = new DemoClock(DemoStart, speed: 10, real);

        Assert.Equal(real.GetTimestamp(), demo.GetTimestamp());
        Assert.Equal(real.TimestampFrequency, demo.TimestampFrequency);
    }

    // ---- the settings ----

    [Fact]
    public void Without_settings_the_application_uses_the_real_clock()
    {
        var real = new FakeTimeProvider(RealNow);

        Assert.Same(real, ClockSetup.Create(new Environment("Development"), Settings(), real));
        Assert.Same(real, ClockSetup.Create(new Environment("Production"), Settings(), real));   // nothing set, nothing refused
    }

    [Fact]
    public void In_Development_a_start_gives_a_demo_clock_at_real_speed_by_default()
    {
        var real = new FakeTimeProvider(RealNow);

        var clock = ClockSetup.Create(new Environment("Development"), Settings(start: "2026-10-20T05:55:00+05:30"), real);

        var demo = Assert.IsType<DemoClock>(clock);
        Assert.Equal(1, demo.Speed);                  // the default
        Assert.Equal(DemoStart, clock.GetUtcNow());
    }

    [Fact]
    public void In_Development_a_speed_alone_starts_from_the_real_moment()
    {
        var real = new FakeTimeProvider(RealNow);

        var clock = ClockSetup.Create(new Environment("Development"), Settings(speed: "10"), real);

        Assert.Equal(10, Assert.IsType<DemoClock>(clock).Speed);
        Assert.Equal(RealNow, clock.GetUtcNow());
    }

    // The guarantee that matters: a system that is not in Development cannot be given a shifted clock.
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("production")]
    [InlineData("")]
    public void Outside_Development_any_Clock_setting_is_refused(string environment)
    {
        var real = new FakeTimeProvider(RealNow);

        var byStart = Assert.Throws<InvalidOperationException>(() => ClockSetup.Create(new Environment(environment), Settings(start: "2026-10-20T05:55:00+05:30"), real));
        var bySpeed = Assert.Throws<InvalidOperationException>(() => ClockSetup.Create(new Environment(environment), Settings(speed: "1"), real));   // even the harmless value

        Assert.Contains("Development", byStart.Message);
        Assert.Contains("Development", bySpeed.Message);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("101")]
    [InlineData("fast")]
    public void An_unusable_speed_stops_the_start(string speed)
    {
        Assert.Throws<InvalidOperationException>(() =>
            ClockSetup.Create(new Environment("Development"), Settings(speed: speed), new FakeTimeProvider(RealNow)));
    }

    [Theory]
    [InlineData("tomorrow evening")]
    public void An_unusable_start_stops_the_start(string start)
    {
        Assert.Throws<InvalidOperationException>(() =>
            ClockSetup.Create(new Environment("Development"), Settings(start: start), new FakeTimeProvider(RealNow)));
    }

    [Fact]
    public async Task The_start_up_guard_refuses_the_settings_outside_Development_and_accepts_them_in_it()
    {
        await new ClockGuard(new Environment("Development"), Settings(start: "2026-10-20T05:55:00+05:30")).StartAsync(CancellationToken.None);
        await new ClockGuard(new Environment("Production"), Settings()).StartAsync(CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ClockGuard(new Environment("Production"), Settings(start: "2026-10-20T05:55:00+05:30")).StartAsync(CancellationToken.None));
    }
}
