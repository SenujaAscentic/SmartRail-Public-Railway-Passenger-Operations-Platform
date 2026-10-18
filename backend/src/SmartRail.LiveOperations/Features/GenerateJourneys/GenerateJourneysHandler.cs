using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartRail.Identity;
using SmartRail.LiveOperations.Persistence;
using SmartRail.Network;

namespace SmartRail.LiveOperations.Features.GenerateJourneys;

// US-B2: stores the journeys that are missing for today and the next 2 days. It only inserts: a stored journey is never
// changed here, so later timetable edits cannot alter a journey that exists (A-17).
// ONE narrow exception: a stored journey that has no driver yet gets the driver of its service (never replacing one that is set).
// That is not a timetable edit; it lets journeys made before the drivers existed, or before a driver's account did, be filled in.
internal sealed class GenerateJourneysHandler(
    INetworkSchedule network,
    IStaffDirectory staff,
    LiveOperationsDbContext db,
    JourneyPlanner planner,
    IConfiguration configuration,
    ILogger<GenerateJourneysHandler> log)
{
    // Returns the number of journeys added.
    public async Task<int> GenerateAsync(CancellationToken ct)
    {
        var schedule = await network.GetScheduleAsync(ct);
        var assignments = LoadAssignments(schedule);
        var drivers = await LoadDriversAsync(ct);

        // The table gets about a dozen rows a day in the MVP, so reading all keys is simple and cheap. Revisit if journeys are archived.
        var storedKeys = await db.Journeys.Select(j => new { j.ServiceId, j.ServiceDate }).ToListAsync(ct);
        var existing = storedKeys.Select(k => (k.ServiceId, k.ServiceDate)).ToHashSet();

        var missing = planner.Plan(schedule, assignments, existing, drivers);
        if (missing.Count > 0)
        {
            db.Journeys.AddRange(missing);
            await db.SaveChangesAsync(ct);
        }

        await FillMissingDriversAsync(schedule, drivers, ct);
        return missing.Count;
    }

    // Service number -> the account id of its driver, from service-assignments.json and Identity. A driver who cannot be found (no
    // such account, not a driver, deactivated) is logged and left out: the journey then has no driver and R-07 refuses positions for
    // it, which is safer than guessing.
    private async Task<IReadOnlyDictionary<string, Guid>> LoadDriversAsync(CancellationToken ct)
    {
        var staffNumbers = ServiceAssignments.ReadDrivers(SeedFolder.Find(configuration["Seed:Path"]));
        var ids = await staff.GetDriverIdsAsync(staffNumbers.Values.Distinct().ToList(), ct);

        var drivers = new Dictionary<string, Guid>();
        foreach (var (service, staffNumber) in staffNumbers)
        {
            if (ids.TryGetValue(staffNumber, out var id))
            {
                drivers[service] = id;
            }
            else
            {
                log.LogWarning("Service {Service} names driver {StaffNumber}, who is not an active driver account; its journeys have no driver.", service, staffNumber);
            }
        }

        return drivers;
    }

    private async Task FillMissingDriversAsync(NetworkSchedule schedule, IReadOnlyDictionary<string, Guid> drivers, CancellationToken ct)
    {
        var numberByService = schedule.Services.ToDictionary(s => s.Id, s => s.Number);
        var withoutDriver = await db.Journeys.Where(j => j.DriverId == null).ToListAsync(ct);

        var filled = 0;
        foreach (var journey in withoutDriver)
        {
            if (numberByService.TryGetValue(journey.ServiceId, out var number) && drivers.TryGetValue(number, out var driverId)
                && journey.AssignDriver(driverId))
            {
                filled++;
            }
        }

        if (filled > 0)
        {
            await db.SaveChangesAsync(ct);
            log.LogInformation("Drivers filled in on {Count} existing journeys.", filled);
        }
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
