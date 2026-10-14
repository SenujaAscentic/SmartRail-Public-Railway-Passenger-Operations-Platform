namespace SmartRail.Identity.Seed;

// Finds the folder with staff.json: the configured path if there is one (Seed:Path, the same setting the Network module uses),
// otherwise shared/seed in the repository, found by walking up from the running program.
// A copy of Network's finder on purpose: using Network's class would reach into its internals (CLAUDE.md section 3).
internal static class IdentitySeedFolder
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
            if (File.Exists(Path.Combine(candidate, "staff.json")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not find shared/seed above " + AppContext.BaseDirectory + ". Set Seed:Path to the folder with the seed files.");
    }
}
