using Xunit;

namespace SmartRail.IntegrationTests;

// backend/openapi.yaml is committed (ADR-09). This test fails when the running application describes
// itself differently from the committed file, so an API change cannot be committed without the file changing too.
// To accept an intended change, run the tests once with UPDATE_OPENAPI=1, then read the git diff of the file.
[Collection("Api")]
public class OpenApiDocumentTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_committed_openapi_yaml_matches_the_running_application()
    {
        var served = Normalise(await api.Factory.CreateClient().GetStringAsync("/openapi/v1.yaml", Ct));
        var file = Path.Combine(BackendFolder(), "openapi.yaml");

        if (Environment.GetEnvironmentVariable("UPDATE_OPENAPI") == "1")
        {
            await File.WriteAllTextAsync(file, served, Ct);
            return;
        }

        Assert.True(File.Exists(file), "backend/openapi.yaml does not exist. Run the tests once with UPDATE_OPENAPI=1 to create it.");
        Assert.Equal(Normalise(await File.ReadAllTextAsync(file, Ct)), served);
    }

    private async Task<string> ServedYaml() => await api.Factory.CreateClient().GetStringAsync("/openapi/v1.yaml", Ct);

    // Contract section 6: every error is Problem Details with a stable code, and field errors and a request id.
    [Fact]
    public async Task The_error_schema_describes_code_errors_and_requestId()
    {
        var yaml = await ServedYaml();
        var start = yaml.IndexOf("    ProblemDetails:", StringComparison.Ordinal);
        Assert.True(start >= 0, "The description has no ProblemDetails schema.");
        var end = yaml.IndexOf("\n    StationSummary:", start, StringComparison.Ordinal);
        var schema = yaml[start..(end < 0 ? yaml.Length : end)];

        Assert.Contains("        code:", schema);
        Assert.Contains("        errors:", schema);
        Assert.Contains("        requestId:", schema);
    }

    [Fact]
    public async Task The_query_parameter_is_described_as_required()
    {
        var yaml = await ServedYaml();
        var at = yaml.IndexOf("name: query", StringComparison.Ordinal);
        Assert.True(at >= 0, "The description has no query parameter.");

        // The parameter block is "name, in, required, schema": required comes right after "in: query".
        Assert.Contains("required: true", yaml[at..Math.Min(yaml.Length, at + 80)]);
    }

    [Fact]
    public async Task The_description_has_no_servers_entry()
    {
        // The server URL would be copied from whichever request produced the file; clients choose their own.
        Assert.DoesNotContain("servers:", await ServedYaml());
    }

    // Line endings must not matter (Windows and Linux), and the file ends with exactly one newline.
    private static string Normalise(string text) => text.Replace("\r\n", "\n").TrimEnd('\n') + "\n";

    // The folder that holds SmartRail.sln, found by walking up from the test program.
    private static string BackendFolder()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SmartRail.sln")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not find SmartRail.sln above " + AppContext.BaseDirectory);
    }
}
