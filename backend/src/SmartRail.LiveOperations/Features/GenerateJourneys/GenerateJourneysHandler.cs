using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartRail.LiveOperations.Persistence;
using SmartRail.Network;

namespace SmartRail.LiveOperations.Features.GenerateJourneys;

// US-B2: stores the journeys that are missing for today and the next 2 days. It only inserts: a stored journey is never
// changed here, so later timetable edits cannot alter a journey that exists (A-17).
internal sealed class GenerateJourneysHandler(
    INetworkSchedule network,
    LiveOperationsDbContext db,
    JourneyPlanner planner,
    IConfiguration configuration)
{
    // Returns the number of journeys added.
    public async Task<int> GenerateAsync(CancellationToken ct)
    {
        var schedule = await network.GetScheduleAsync(ct);
        var assignments = LoadAssignments(schedule);

        // The table gets about a dozen rows a day in the MVP, so reading all keys is simple and cheap. Revisit if journeys are archived.
        var storedKeys = await db.Journeys.Select(j => new { j.ServiceId, j.ServiceDate }).ToListAsync(ct);
        var existing = storedKeys.Select(k => (k.ServiceId, k.ServiceDate)).ToHashSet();

        var missing = planner.Plan(schedule, assignments, existing);
        if (missing.Count == 0)
        {
            return 0;
        }

        db.Journeys.AddRange(missing);
        await db.SaveChangesAsync(ct);
        return missing.Count;
    }

    // Reads service-assignments.json and refuses it if a service has no train set or names one that does not exist.
    // Called at start-up too, so a wrong file stops the application instead of silently removing trains.
    public IReadOnlyDictionary<string, string> LoadAssignments(NetworkSchedule schedule)
    {
        var assignments = ServiceAssignments.Read(SeedFolder.Find(configuration["Seed:Path"]));
        var problems = ServiceAssignments.Check(schedule, assignments);

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                "service-assignments.json does not match the network:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
        }

        return assignments;
    }
}
