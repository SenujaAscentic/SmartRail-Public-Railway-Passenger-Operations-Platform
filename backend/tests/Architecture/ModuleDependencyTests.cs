using System.Reflection;
using Xunit;

namespace SmartRail.ArchitectureTests;

// Checks which assemblies each project really uses (CLAUDE.md section 3).
// The compiler only records an assembly reference when the code uses a type from it,
// so these tests fail when code crosses a boundary, not merely because a .csproj line exists.
public class ModuleDependencyTests
{
    // Each module is one project, plus a Domain project for LiveOperations and Disruptions.
    private static readonly string[] ModuleAssemblies =
    [
        "SmartRail.Network",
        "SmartRail.LiveOperations", "SmartRail.LiveOperations.Domain",
        "SmartRail.Disruptions", "SmartRail.Disruptions.Domain",
        "SmartRail.Identity",
        "SmartRail.Notifications",
        "SmartRail.CateringIntegration",
    ];

    private static IEnumerable<string> ReferencedAssemblies(string assemblyName) =>
        Assembly.Load(assemblyName).GetReferencedAssemblies().Select(a => a.Name!);

    // "SmartRail.LiveOperations.Domain" -> "LiveOperations"
    private static string ModuleOf(string assemblyName) => assemblyName.Split('.')[1];

    [Theory]
    [InlineData("SmartRail.LiveOperations.Domain")]
    [InlineData("SmartRail.Disruptions.Domain")]
    public void Domain_project_depends_only_on_System_assemblies(string domainAssembly)
    {
        var notSystem = ReferencedAssemblies(domainAssembly)
            .Where(name => name != "System" && !name.StartsWith("System.", StringComparison.Ordinal))
            .ToList();

        Assert.True(notSystem.Count == 0,
            $"{domainAssembly} must reference only System assemblies, but references: {string.Join(", ", notSystem)}");
    }

    [Fact]
    public void LiveOperations_does_not_depend_on_Disruptions()
    {
        foreach (var assembly in new[] { "SmartRail.LiveOperations", "SmartRail.LiveOperations.Domain" })
        {
            var toDisruptions = ReferencedAssemblies(assembly)
                .Where(name => name.StartsWith("SmartRail.Disruptions", StringComparison.Ordinal))
                .ToList();

            Assert.True(toDisruptions.Count == 0,
                $"{assembly} must not depend on Disruptions, but references: {string.Join(", ", toDisruptions)}");
        }
    }

    [Fact]
    public void No_module_depends_on_another_module_except_Disruptions_on_LiveOperations()
    {
        var violations = new List<string>();

        foreach (var assembly in ModuleAssemblies)
        {
            foreach (var reference in ReferencedAssemblies(assembly)
                         .Where(name => name.StartsWith("SmartRail.", StringComparison.Ordinal)))
            {
                var from = ModuleOf(assembly);
                var to = ModuleOf(reference);
                var sameModule = from == to;
                var allowed = from == "Disruptions" && to == "LiveOperations";

                if (!sameModule && !allowed)
                {
                    violations.Add($"{assembly} -> {reference}");
                }
            }
        }

        Assert.True(violations.Count == 0,
            $"Modules may only depend on other modules as Disruptions -> LiveOperations. Violations: {string.Join("; ", violations)}");
    }
}
