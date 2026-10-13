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
