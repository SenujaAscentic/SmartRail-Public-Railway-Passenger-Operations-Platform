using Microsoft.Extensions.DependencyInjection;
using SmartRail.Identity;
using Xunit;

namespace SmartRail.IntegrationTests.Identity;

// IStaffDirectory is the door LiveOperations uses to turn a driver's staff number into the account id that is the `sub` of
// their tokens.
[Collection("Api")]
public class StaffDirectoryTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<IReadOnlyDictionary<string, Guid>> Lookup(params string[] numbers)
    {
        using var scope = api.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IStaffDirectory>().GetDriverIdsAsync(numbers, Ct);
    }

    [Fact]
    public async Task The_drivers_of_the_seed_are_found()
    {
        var ids = await Lookup("DRV-01", "DRV-02", "DRV-03", "DRV-04");

        Assert.Equal(4, ids.Count);
        Assert.Equal(4, ids.Values.Distinct().Count());
    }

    // R-07 compares this id with the token's `sub`, so the two must be the same value.
    [Fact]
    public async Task The_id_is_the_sub_of_that_drivers_token()
    {
        var id = (await Lookup("DRV-01"))["DRV-01"];
        var client = api.Factory.NewClient();

        var token = await (await client.CrewSignInAsync("DRV-01", api.DemoPassword, Ct)).AccessTokenAsync(Ct);

        Assert.Equal(id.ToString(), AuthTestHelpers.Read(token).GetClaim("sub").Value);
    }

    [Fact]
    public async Task Staff_who_are_not_drivers_and_unknown_numbers_are_left_out()
    {
        var ids = await Lookup("DRV-01", "CTRL-01", "SM-FOT", "NOBODY-99");

        Assert.Equal(["DRV-01"], ids.Keys);
    }
}
