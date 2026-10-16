using SmartRail.LiveOperations.Features.GenerateJourneys;
using SmartRail.Network;
using SmartRail.Network.Seed;
using Xunit;

namespace SmartRail.UnitTests.LiveOperations.GenerateJourneys;

public class ServiceAssignmentsTests
{
    private static readonly NetworkSchedule Schedule = new(
        [
            new ScheduledService(Guid.NewGuid(), "1001", Guid.NewGuid(), [], Guid.NewGuid()),
            new ScheduledService(Guid.NewGuid(), "1002", Guid.NewGuid(), [], Guid.NewGuid()),
        ],
        [],
        [new TrainSetInfo(Guid.NewGuid(), "DPS-01")]);

    [Fact]
    public void Assignments_that_cover_every_service_with_a_known_train_set_have_no_problems()
    {
        var assignments = new Dictionary<string, string> { ["1001"] = "DPS-01", ["1002"] = "DPS-01" };

        Assert.Empty(ServiceAssignments.Check(Schedule, assignments));
    }

    [Fact]
    public void A_service_without_an_assignment_is_reported()
    {
        var assignments = new Dictionary<string, string> { ["1001"] = "DPS-01" };

        Assert.Contains(ServiceAssignments.Check(Schedule, assignments), p => p.Contains("1002"));
    }

    [Fact]
    public void An_assignment_for_an_unknown_service_is_reported()
    {
        var assignments = new Dictionary<string, string> { ["1001"] = "DPS-01", ["1002"] = "DPS-01", ["9999"] = "DPS-01" };

        Assert.Contains(ServiceAssignments.Check(Schedule, assignments), p => p.Contains("9999"));
    }

    [Fact]
    public void An_assignment_to_an_unknown_train_set_is_reported()
    {
        var assignments = new Dictionary<string, string> { ["1001"] = "DPS-01", ["1002"] = "NO-SUCH-SET" };

        Assert.Contains(ServiceAssignments.Check(Schedule, assignments), p => p.Contains("NO-SUCH-SET"));
    }

    [Fact]
    public void The_real_assignment_file_lists_the_8_seed_services()
    {
        var assignments = ServiceAssignments.Read(SeedFolder.Find(configuredPath: null));

        Assert.Equal(8, assignments.Count);
        Assert.Equal("DPS-01", assignments["1001"]);
    }
}
