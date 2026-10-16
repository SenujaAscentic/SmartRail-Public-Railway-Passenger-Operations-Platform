using System.Text.Json.Nodes;
using SmartRail.LiveOperations.Stream;
using Xunit;

namespace SmartRail.UnitTests.LiveOperations.Stream;

public class StreamHubTests
{
    private static readonly Guid JourneyA = Guid.NewGuid();
    private static readonly Guid JourneyB = Guid.NewGuid();
    private static readonly Guid StationX = Guid.NewGuid();
    private static readonly Guid StationY = Guid.NewGuid();
    private static readonly Guid Line = Guid.NewGuid();

    private static IReadOnlySet<Guid> Set(params Guid[] ids) => ids.ToHashSet();

    private static StreamFilter Filter(Guid[]? journeys = null, Guid[]? stations = null, Guid[]? lines = null) =>
        new(Set(journeys ?? []), Set(stations ?? []), Set(lines ?? []));

    private static PublicEvent Event(Guid journey, Guid[] stations, Guid? line = null, long sequence = 1) =>
        new(Guid.NewGuid(), sequence, "DelayChanged", 1, DateTime.UtcNow, journey, new DateOnly(2026, 10, 14), stations, line ?? Guid.NewGuid(), new JsonObject());

    // ---- the filter ----

    [Fact]
    public void A_filter_matches_on_journey_station_or_line()
    {
        var byJourney = Filter(journeys: [JourneyA]);
        var byStation = Filter(stations: [StationX]);
        var byLine = Filter(lines: [Line]);
        var evt = Event(JourneyA, [StationX, StationY], Line);

        Assert.True(byJourney.Matches(evt));
        Assert.True(byStation.Matches(evt));
        Assert.True(byLine.Matches(evt));
    }

    [Fact]
    public void A_filter_does_not_match_an_unrelated_event()
    {
        var evt = Event(JourneyB, [StationY]);

        Assert.False(Filter(journeys: [JourneyA]).Matches(evt));
        Assert.False(Filter(stations: [StationX]).Matches(evt));
        Assert.False(Filter(lines: [Line]).Matches(evt));
    }

    [Fact]
    public void Several_lists_in_one_filter_are_alternatives_not_a_combination()
    {
        var filter = Filter(journeys: [JourneyA], stations: [StationX]);

        Assert.True(filter.Matches(Event(JourneyA, [StationY])));   // journey matches, station does not
        Assert.True(filter.Matches(Event(JourneyB, [StationX])));   // station matches, journey does not
        Assert.False(filter.Matches(Event(JourneyB, [StationY])));
    }

    [Fact]
    public void An_empty_filter_matches_nothing()
    {
        var filter = Filter();

        Assert.True(filter.IsEmpty);
        Assert.False(filter.Matches(Event(JourneyA, [StationX])));
    }

    // ---- the hub ----

    [Fact]
    public void A_subscriber_gets_matching_events_in_order_and_nothing_else()
    {
        var hub = new StreamHub();
        using var subscription = hub.Subscribe(Filter(journeys: [JourneyA]));

        hub.Publish(Event(JourneyA, [StationX], sequence: 1));
        hub.Publish(Event(JourneyB, [StationX], sequence: 2));
        hub.Publish(Event(JourneyA, [StationX], sequence: 3));

        Assert.True(subscription.Reader.TryRead(out var first));
        Assert.True(subscription.Reader.TryRead(out var second));
        Assert.False(subscription.Reader.TryRead(out _));
        Assert.Equal([1L, 3L], [first!.Sequence, second!.Sequence]);
    }

    [Fact]
    public void Every_matching_subscriber_gets_the_event()
    {
        var hub = new StreamHub();
        using var one = hub.Subscribe(Filter(journeys: [JourneyA]));
        using var two = hub.Subscribe(Filter(stations: [StationX]));

        hub.Publish(Event(JourneyA, [StationX]));

        Assert.True(one.Reader.TryRead(out _));
        Assert.True(two.Reader.TryRead(out _));
    }

    [Fact]
    public async Task A_disposed_subscription_gets_nothing_more_and_its_reader_completes()
    {
        var hub = new StreamHub();
        var subscription = hub.Subscribe(Filter(journeys: [JourneyA]));

        subscription.Dispose();
        hub.Publish(Event(JourneyA, [StationX]));

        Assert.False(await subscription.Reader.WaitToReadAsync(TestContext.Current.CancellationToken));
    }

    // A client that stops reading must not hold the dispatcher or use memory without limit: it is cut off, and resumes later.
    [Fact]
    public async Task A_subscriber_that_does_not_keep_up_is_cut_off_with_an_error()
    {
        var hub = new StreamHub(bufferSize: 3);
        using var slow = hub.Subscribe(Filter(journeys: [JourneyA]));

        for (var i = 1; i <= 4; i++)
        {
            hub.Publish(Event(JourneyA, [StationX], sequence: i));
        }

        for (var i = 0; i < 3; i++)
        {
            Assert.True(slow.Reader.TryRead(out _));   // what fitted is still delivered
        }
        await Assert.ThrowsAsync<SubscriberTooSlowException>(async () => await slow.Reader.WaitToReadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void One_slow_subscriber_does_not_stop_the_others()
    {
        var hub = new StreamHub(bufferSize: 2);
        using var slow = hub.Subscribe(Filter(journeys: [JourneyA]));
        using var fast = hub.Subscribe(Filter(journeys: [JourneyA]));

        for (var i = 1; i <= 5; i++)
        {
            hub.Publish(Event(JourneyA, [StationX], sequence: i));
            Assert.True(fast.Reader.TryRead(out var seen));   // the fast one drains each event at once
            Assert.Equal(i, seen!.Sequence);
        }
    }
}
