using SmartRail.Network.Seed;
using Xunit;

namespace SmartRail.UnitTests.Network.Seed;

public class SeedReaderTests
{
    [Fact]
    public void Csv_line_with_a_quoted_comma_is_split_into_the_right_columns()
    {
        var columns = CsvLine.Split("11,KDT,Kandy,\"Senuja (coords, 12 Oct)\"");

        Assert.Equal(["11", "KDT", "Kandy", "Senuja (coords, 12 Oct)"], columns);
    }

    [Fact]
    public void Csv_line_keeps_empty_columns()
    {
        Assert.Equal(["a", "", "c"], CsvLine.Split("a,,c"));
    }

    [Fact]
    public void Real_seed_files_load_and_pass_all_invariants()
    {
        var data = SeedReader.Read(SeedFolder.Find(configuredPath: null));

        var result = SeedValidator.Validate(data);

        Assert.Empty(result.Errors);
        // Kandy's distance is an ESTIMATE in stations.csv, so there is exactly one warning, and it names Kandy.
        var warning = Assert.Single(result.Warnings);
        Assert.Contains("KDT", warning.Message);
    }

    [Fact]
    public void Real_seed_files_contain_the_agreed_amount_of_data()
    {
        var data = SeedReader.Read(SeedFolder.Find(configuredPath: null));

        Assert.Equal(11, data.Stations.Count);
        Assert.Single(data.Lines);
        Assert.Equal(2, data.Routes.Count);
        Assert.All(data.Routes, r => Assert.Equal(11, r.Stations.Count));
        Assert.Equal(8, data.Services.Count);
        Assert.Equal(2, data.Calendars.Count);
        Assert.Equal(4, data.TrainSets.Count);
    }
}
