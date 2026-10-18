using SmartRail.LiveOperations.Features.CrewPositions;
using Xunit;

namespace SmartRail.UnitTests.LiveOperations.CrewPositions;

// The shape of a position batch (contract 3.4). Whether a fix is plausible is the Domain's business (R-08), not this validator's.
public class PositionBatchValidatorTests
{
    private static readonly DateTimeOffset T = new(2026, 10, 14, 4, 30, 0, TimeSpan.Zero);

    private static PositionRequest Fix(double? lat = 7.0, double? lng = 80.0, double? speed = 40, double? accuracy = 5, DateTimeOffset? gps = null, DateTimeOffset? device = null) =>
        new(lat, lng, speed, accuracy, gps, device ?? T);

    private static PositionBatchRequest Batch(params PositionRequest[] fixes) => new(Guid.NewGuid(), [.. fixes]);

    [Fact]
    public void A_good_batch_keeps_each_fix_with_its_place_in_the_request()
    {
        var (positions, errors) = PositionBatchValidator.Validate(Batch(Fix(lat: 7.1), Fix(lat: 7.2, gps: T.AddSeconds(1))));

        Assert.Empty(errors);
        Assert.Equal([0, 1], positions!.Select(p => p.Index));
        Assert.Equal(7.2, positions![1].Location.Latitude);
        Assert.Equal(T.AddSeconds(1).UtcDateTime, positions[1].GpsTime);
        Assert.Null(positions[0].GpsTime);
        Assert.Equal(T.UtcDateTime, positions[0].DeviceTime);
    }

    [Fact]
    public void A_missing_body_or_action_id_or_an_empty_list_is_refused()
    {
        Assert.Contains("clientActionId", PositionBatchValidator.Validate(null).Errors.Keys);
        Assert.Contains("clientActionId", PositionBatchValidator.Validate(new PositionBatchRequest(null, [Fix()])).Errors.Keys);
        Assert.Contains("clientActionId", PositionBatchValidator.Validate(new PositionBatchRequest(Guid.Empty, [Fix()])).Errors.Keys);
        Assert.Contains("positions", PositionBatchValidator.Validate(new PositionBatchRequest(Guid.NewGuid(), [])).Errors.Keys);
        Assert.Contains("positions", PositionBatchValidator.Validate(new PositionBatchRequest(Guid.NewGuid(), null)).Errors.Keys);
    }

    [Fact]
    public void At_most_100_fixes_are_allowed()
    {
        Assert.Empty(PositionBatchValidator.Validate(Batch(Enumerable.Repeat(Fix(), 100).ToArray())).Errors);
        Assert.Contains("positions", PositionBatchValidator.Validate(Batch(Enumerable.Repeat(Fix(), 101).ToArray())).Errors.Keys);
    }

    [Theory]
    [InlineData(91.0, 80.0, 40.0, "positions[0].lat")]
    [InlineData(-91.0, 80.0, 40.0, "positions[0].lat")]
    [InlineData(7.0, 181.0, 40.0, "positions[0].lng")]
    [InlineData(7.0, 80.0, -1.0, "positions[0].speedKmh")]
    [InlineData(7.0, 80.0, 401.0, "positions[0].speedKmh")]
    [InlineData(double.NaN, 80.0, 40.0, "positions[0].lat")]
    public void Coordinates_and_speed_must_be_possible_numbers(double lat, double lng, double speed, string field)
    {
        Assert.Contains(field, PositionBatchValidator.Validate(Batch(Fix(lat, lng, speed))).Errors.Keys);
    }

    [Fact]
    public void A_fix_with_no_coordinates_no_speed_or_no_device_time_is_refused_and_the_error_names_which_fix()
    {
        var bad = new PositionRequest(null, null, null, -2, null, null);

        var errors = PositionBatchValidator.Validate(Batch(Fix(), bad)).Errors;

        Assert.Equal(
            ["positions[1].accuracyM", "positions[1].deviceTime", "positions[1].lat", "positions[1].lng", "positions[1].speedKmh"],
            errors.Keys.Order());
    }

    [Fact]
    public void Accuracy_is_optional_but_cannot_be_negative()
    {
        Assert.Empty(PositionBatchValidator.Validate(Batch(Fix(accuracy: null))).Errors);
        Assert.Contains("positions[0].accuracyM", PositionBatchValidator.Validate(Batch(Fix(accuracy: -1))).Errors.Keys);
    }
}
