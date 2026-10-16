using SmartRail.LiveOperations.Features.StreamEvents;
using Xunit;

namespace SmartRail.UnitTests.LiveOperations.Stream;

// GET /stream?journeyIds=&stationIds=&lineIds= (contract section 4), the Last-Event-ID header, and how events are framed.
public class StreamRequestTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();

    // ---- the filters ----

    [Fact]
    public void Comma_separated_ids_become_a_filter()
    {
        var (filter, errors) = StreamRequest.Parse($"{A},{B}", stationIds: A.ToString(), lineIds: null);

        Assert.Empty(errors);
        Assert.Equal(new[] { A, B }.Order(), filter!.JourneyIds.Order());
        Assert.Equal([A], filter.StationIds);
        Assert.Empty(filter.LineIds);
    }

    [Fact]
    public void Spaces_empty_items_and_repeats_are_forgiven()
    {
        var (filter, errors) = StreamRequest.Parse($" {A} ,, {A},{B} ,", null, null);

        Assert.Empty(errors);
        Assert.Equal(new[] { A, B }.Order(), filter!.JourneyIds.Order());
    }

    // The stream must not hand out the whole network to anyone who asks (threat T-10, flooding).
    [Theory]
    [InlineData(null, null, null)]
    [InlineData("", " ", ",")]
    public void At_least_one_filter_is_required(string? journeys, string? stations, string? lines)
    {
        var (filter, errors) = StreamRequest.Parse(journeys, stations, lines);

        Assert.Null(filter);
        Assert.Contains("filters", errors.Keys);
    }

    [Fact]
    public void An_id_that_is_not_a_guid_is_an_error_on_its_own_field()
    {
        var (filter, errors) = StreamRequest.Parse(journeyIds: A.ToString(), stationIds: "not-an-id", lineIds: null);

        Assert.Null(filter);
        Assert.Contains("stationIds", errors.Keys);
        Assert.DoesNotContain("journeyIds", errors.Keys);
    }

    [Fact]
    public void Too_many_ids_in_one_list_is_an_error()
    {
        var many = string.Join(",", Enumerable.Range(0, StreamRequest.MaxIdsPerList + 1).Select(_ => Guid.NewGuid()));

        var (filter, errors) = StreamRequest.Parse(many, null, null);

        Assert.Null(filter);
        Assert.Contains("journeyIds", errors.Keys);
    }

    // ---- Last-Event-ID ----

    [Theory]
    [InlineData("42", 42L)]
    [InlineData(" 7 ", 7L)]
    [InlineData("0", 0L)]
    public void A_valid_last_event_id_is_read(string header, long expected)
    {
        Assert.Equal(expected, StreamRequest.ParseLastEventId(header));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("1.5")]
    public void Anything_else_counts_as_no_last_event_id(string? header)
    {
        Assert.Null(StreamRequest.ParseLastEventId(header));
    }

    // ---- resume or resync ----

    [Fact]
    public void Without_a_last_event_id_the_connection_is_fresh()
    {
        Assert.Equal(ResumeAction.Fresh, ResumeDecision.Of(null, oldestRetained: 1, newest: 100));
    }

    [Theory]
    [InlineData(50L)]    // somewhere in the middle
    [InlineData(100L)]   // the newest: nothing missed, nothing to replay
    [InlineData(9L)]     // the one just before the oldest kept row: nothing in between was deleted
    public void A_last_event_id_inside_what_is_kept_resumes(long lastEventId)
    {
        Assert.Equal(ResumeAction.Resume, ResumeDecision.Of(lastEventId, oldestRetained: 10, newest: 100));
    }

    [Theory]
    [InlineData(8L)]     // events between it and the oldest kept row may have been deleted
    [InlineData(101L)]   // ahead of anything this server has sent (for example after a database reset)
    public void A_last_event_id_outside_what_is_kept_needs_a_resync(long lastEventId)
    {
        Assert.Equal(ResumeAction.Resync, ResumeDecision.Of(lastEventId, oldestRetained: 10, newest: 100));
    }

    [Fact]
    public void When_nothing_is_kept_a_last_event_id_needs_a_resync()
    {
        Assert.Equal(ResumeAction.Resync, ResumeDecision.Of(5, oldestRetained: null, newest: null));
    }

    // ---- framing ----

    [Fact]
    public void An_event_is_framed_as_id_event_data_and_a_blank_line()
    {
        Assert.Equal("id: 12\nevent: DelayChanged\ndata: {\"a\":1}\n\n", SseFormat.Event(12, "DelayChanged", "{\"a\":1}"));
    }

    [Fact]
    public void An_event_without_an_id_leaves_the_id_line_out()
    {
        Assert.Equal("event: resync\ndata: {}\n\n", SseFormat.Event(null, "resync", "{}"));
    }

    [Fact]
    public void The_heartbeat_is_a_comment_line()
    {
        Assert.StartsWith(":", SseFormat.Heartbeat);
        Assert.EndsWith("\n\n", SseFormat.Heartbeat);
    }

    // A new line inside the data would end the frame early and let content pose as another field or event.
    [Theory]
    [InlineData("DelayChanged", "{\"a\":\"x\ny\"}")]
    [InlineData("DelayChanged", "line1\r\nline2")]
    [InlineData("Delay\nChanged", "{}")]
    public void New_lines_in_the_type_or_data_are_refused(string type, string data)
    {
        Assert.Throws<ArgumentException>(() => SseFormat.Event(1, type, data));
    }
}
