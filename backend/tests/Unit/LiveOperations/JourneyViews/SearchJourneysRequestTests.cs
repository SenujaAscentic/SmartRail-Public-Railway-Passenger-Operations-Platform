using SmartRail.LiveOperations.Features.SearchJourneys;
using Xunit;

namespace SmartRail.UnitTests.LiveOperations.JourneyViews;

// GET /journeys/search?from=&to=&date=&after=&limit= (contract 3.1).
public class SearchJourneysRequestTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 10, 14);
    private static readonly TimeZoneInfo Colombo = TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo");

    private static (SearchJourneysQuery? Query, Dictionary<string, string[]> Errors) Parse(
        string? from, string? to, string? date = null, string? after = null, string? limit = null) =>
        SearchJourneysRequest.Parse(from, to, date, after, limit, Today);

    [Fact]
    public void Only_from_and_to_are_needed_and_the_rest_has_defaults()
    {
        var (query, errors) = Parse(A.ToString(), B.ToString());

        Assert.Empty(errors);
        Assert.Equal(new SearchJourneysQuery(A, B, Today, null, SearchJourneysRequest.DefaultLimit), query);
    }

    [Fact]
    public void All_the_parameters_are_read()
    {
        var (query, errors) = Parse(A.ToString(), B.ToString(), "2026-10-20", "2026-10-20T04:30:00Z", "3");

        Assert.Empty(errors);
        Assert.Equal(new SearchJourneysQuery(A, B, new DateOnly(2026, 10, 20), new DateTime(2026, 10, 20, 4, 30, 0, DateTimeKind.Utc), 3), query);
    }

    [Theory]
    [InlineData("2026-10-20T10:00:00+05:30", "2026-10-20T04:30:00")]   // an offset is converted to UTC
    [InlineData("2026-10-20T04:30:00", "2026-10-20T04:30:00")]         // no offset: taken as UTC
    public void The_after_time_is_kept_in_utc(string text, string expectedUtc)
    {
        var (query, _) = Parse(A.ToString(), B.ToString(), after: text);

        Assert.Equal(DateTime.Parse(expectedUtc, null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal), query!.After);
    }

    [Theory]
    [InlineData(null, null, "from", "to")]
    [InlineData("", "  ", "from", "to")]
    public void Missing_stations_are_errors_on_their_own_fields(string? from, string? to, string fromField, string toField)
    {
        var (query, errors) = Parse(from, to);

        Assert.Null(query);
        Assert.Contains(fromField, errors.Keys);
        Assert.Contains(toField, errors.Keys);
    }

    [Fact]
    public void A_station_id_that_is_not_a_guid_is_an_error()
    {
        var (query, errors) = Parse("kandy", B.ToString());

        Assert.Null(query);
        Assert.Equal(["from"], errors.Keys);
    }

    [Fact]
    public void The_same_station_twice_is_an_error_on_to()
    {
        var (query, errors) = Parse(A.ToString(), A.ToString());

        Assert.Null(query);
        Assert.Equal(["to"], errors.Keys);
    }

    [Theory]
    [InlineData("14/10/2026")]
    [InlineData("2026-13-01")]
    [InlineData("tomorrow")]
    public void A_date_must_be_yyyy_MM_dd(string date)
    {
        var (query, errors) = Parse(A.ToString(), B.ToString(), date);

        Assert.Null(query);
        Assert.Equal(["date"], errors.Keys);
    }

    [Fact]
    public void An_after_that_is_not_a_time_is_an_error()
    {
        var (query, errors) = Parse(A.ToString(), B.ToString(), after: "soon");

        Assert.Null(query);
        Assert.Equal(["after"], errors.Keys);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("51")]
    [InlineData("-1")]
    [InlineData("many")]
    [InlineData("2.5")]
    public void The_limit_is_a_whole_number_from_1_to_50(string limit)
    {
        var (query, errors) = Parse(A.ToString(), B.ToString(), limit: limit);

        Assert.Null(query);
        Assert.Equal(["limit"], errors.Keys);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("50", 50)]
    public void The_limit_edges_are_allowed(string limit, int expected)
    {
        Assert.Equal(expected, Parse(A.ToString(), B.ToString(), limit: limit).Query!.Limit);
    }

    // ---- past departures are hidden ----

    private static SearchJourneysQuery Query(DateOnly date, DateTime? after = null) => new(A, B, date, after, 10);

    [Fact]
    public void Today_the_window_starts_now()
    {
        var now = new DateTime(2026, 10, 14, 4, 30, 0, DateTimeKind.Utc);

        Assert.Equal(now, SearchJourneysRequest.AfterUtc(Query(Today), Today, now, Colombo));
    }

    [Fact]
    public void Another_day_starts_at_midnight_in_Colombo_which_is_the_evening_before_in_UTC()
    {
        var now = new DateTime(2026, 10, 14, 4, 30, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 10, 14, 18, 30, 0, DateTimeKind.Utc),
            SearchJourneysRequest.AfterUtc(Query(new DateOnly(2026, 10, 15)), Today, now, Colombo));
    }

    [Fact]
    public void An_explicit_after_wins()
    {
        var now = new DateTime(2026, 10, 14, 4, 30, 0, DateTimeKind.Utc);
        var after = new DateTime(2026, 10, 14, 8, 0, 0, DateTimeKind.Utc);

        Assert.Equal(after, SearchJourneysRequest.AfterUtc(Query(Today, after), Today, now, Colombo));
    }
}
