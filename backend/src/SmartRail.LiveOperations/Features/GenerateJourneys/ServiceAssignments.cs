using System.Text.Json;
using SmartRail.Network;

namespace SmartRail.LiveOperations.Features.GenerateJourneys;

// Which train set runs each service, from shared/seed/service-assignments.json.
// The file also names a driver per service; it stays unused until journeys get their crew (the guard role is deferred, 14 Oct).
public static class ServiceAssignments
{
    private sealed record File(List<Row> Assignments);
    private sealed record Row(string ServiceNumber, string TrainSetCode);

    // Returns service number -> train set code.
    public static IReadOnlyDictionary<string, string> Read(string seedFolder)
    {
        var path = Path.Combine(seedFolder, "service-assignments.json");
        var file = JsonSerializer.Deserialize<File>(System.IO.File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))
                   ?? throw new InvalidDataException($"{path}: the file is empty.");

        var byService = new Dictionary<string, string>();
        foreach (var row in file.Assignments)
        {
            if (!byService.TryAdd(row.ServiceNumber, row.TrainSetCode))
            {
                throw new InvalidDataException($"{path}: service {row.ServiceNumber} appears more than once.");
            }
        }

        return byService;
    }

    // Everything that would stop journeys being generated correctly. An empty list means the assignments are usable.
    public static IReadOnlyList<string> Check(NetworkSchedule schedule, IReadOnlyDictionary<string, string> assignments)
    {
        var problems = new List<string>();
        var serviceNumbers = schedule.Services.Select(s => s.Number).ToHashSet();
        var trainSetCodes = schedule.TrainSets.Select(t => t.Code).ToHashSet();

        problems.AddRange(serviceNumbers.Where(n => !assignments.ContainsKey(n)).Order()
            .Select(n => $"Service {n} has no train set assigned."));

        foreach (var (service, trainSet) in assignments)
        {
            if (!serviceNumbers.Contains(service))
            {
                problems.Add($"The assignment for service {service} refers to a service that does not exist.");
            }

            if (!trainSetCodes.Contains(trainSet))
            {
                problems.Add($"Service {service} is assigned to train set {trainSet}, which does not exist.");
            }
        }

        return problems;
    }
}
