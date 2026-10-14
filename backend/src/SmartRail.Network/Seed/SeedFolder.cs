namespace SmartRail.Network.Seed;

// Finds the folder with the seed files: the configured path if there is one,
// otherwise shared/seed in the repository, found by walking up from the running program.
public static class SeedFolder
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
            if (File.Exists(Path.Combine(candidate, "stations.csv")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not find shared/seed above " + AppContext.BaseDirectory + ". Set Seed:Path to the folder with the seed files.");
    }
}
