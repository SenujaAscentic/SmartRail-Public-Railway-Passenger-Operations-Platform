using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Features.GenerateJourneys;
using Xunit;

namespace SmartRail.UnitTests.LiveOperations;

// The driver of a journey: the account id that appears as `sub` in that driver's tokens (R-07 compares against it).
public class DriverAssignmentTests
{
    private static OperationalJourney NewJourney(Guid? driver = null) =>
        OperationalJourney.Plan(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 14), Guid.NewGuid(),
            [new PlannedStop(1, Guid.NewGuid(), CallType.Stop, null, 360, "1")], TimeZoneInfo.Utc, Guid.NewGuid(), driver);

    [Fact]
    public void A_journey_planned_without_a_driver_has_none()
    {
        Assert.Null(NewJourney().DriverId);
    }

    [Fact]
    public void A_journey_planned_with_a_driver_has_that_driver()
    {
        var driver = Guid.NewGuid();

        Assert.Equal(driver, NewJourney(driver).DriverId);
    }

    [Fact]
    public void A_driver_can_be_assigned_where_none_is_set()
    {
        var journey = NewJourney();
        var driver = Guid.NewGuid();

        Assert.True(journey.AssignDriver(driver));
        Assert.Equal(driver, journey.DriverId);
    }

    // The backfill must never take a journey away from the driver it has.
    [Fact]
    public void A_driver_who_is_already_assigned_is_not_replaced()
    {
        var first = Guid.NewGuid();
        var journey = NewJourney(first);

        Assert.False(journey.AssignDriver(Guid.NewGuid()));
        Assert.Equal(first, journey.DriverId);
    }

    [Fact]
    public void The_real_assignments_file_names_a_driver_for_every_service()
    {
        var drivers = ServiceAssignments.ReadDrivers(global::SmartRail.Network.Seed.SeedFolder.Find(configuredPath: null));
        var trainSets = ServiceAssignments.Read(global::SmartRail.Network.Seed.SeedFolder.Find(configuredPath: null));

        Assert.Equal(trainSets.Keys.Order(), drivers.Keys.Order());
        Assert.All(drivers.Values, number => Assert.StartsWith("DRV-", number));
    }
}
