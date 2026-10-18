using SmartRail.Network.Seed;
using Xunit;

namespace SmartRail.UnitTests.Network.Seed;

public class SeedReaderTests
{
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

    // stations.csv marks them in stop_types ("all trains; catering station"); Network turns that into a flag the app can show.
    [Fact]
    public void Catering_stations_are_read_from_stop_types()
    {
        var data = SeedReader.Read(SeedFolder.Find(configuredPath: null));

        Assert.Equal(["PDA", "PLG"], data.Stations.Where(s => s.IsCateringStation).Select(s => s.Code).Order());
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
