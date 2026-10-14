namespace SmartRail.LiveOperations.Features.GenerateJourneys;

// Finds the seed folder: the configured path, or shared/seed above the running program.
// Network has the same few lines; modules do not share code, so each keeps its own copy.
internal static class SeedFolder
{
    public static string Find(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return configuredPath;
        }

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "shared", "seed");
            if (File.Exists(Path.Combine(candidate, "service-assignments.json")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not find shared/seed above " + AppContext.BaseDirectory + ". Set Seed:Path to the folder with the seed files.");
    }
}
