using Microsoft.Extensions.DependencyInjection;
using SmartRail.Network;
using Xunit;

namespace SmartRail.IntegrationTests.Network;

// IStationDirectory is the door Identity uses to turn a station code from the staff seed into the station's ID (once, at seeding).
[Collection("Api")]
public class StationDirectoryTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<IReadOnlyDictionary<string, Guid>> Lookup(params string[] codes)
    {
        using var scope = api.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IStationDirectory>().GetStationIdsAsync(codes, Ct);
    }

    [Fact]
    public async Task Known_station_codes_are_answered_with_their_ids()
    {
        var ids = await Lookup("FOT", "KDT");

        Assert.Equal(2, ids.Count);
        Assert.NotEqual(Guid.Empty, ids["FOT"]);
        Assert.NotEqual(ids["FOT"], ids["KDT"]);
    }

    [Fact]
    public async Task An_unknown_code_is_left_out_so_the_caller_can_report_it()
    {
        var ids = await Lookup("FOT", "XXX");

        Assert.Equal(["FOT"], ids.Keys);
    }
}
