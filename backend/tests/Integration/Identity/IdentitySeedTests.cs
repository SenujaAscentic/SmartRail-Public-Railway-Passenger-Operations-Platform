using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartRail.Identity.Model;
using SmartRail.Identity.Persistence;
using SmartRail.Network;
using Xunit;

namespace SmartRail.IntegrationTests.Identity;

// The staff and cab-device seed against a real PostgreSQL: what is written, what a restart does, what stops the application.
[Collection("Api")]
public class IdentitySeedTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<T> InDb<T>(Func<IdentityModuleDbContext, Task<T>> query)
    {
        using var scope = api.Factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>());
    }

    private static IEnumerable<string> AllMessages(Exception? exception)
    {
        for (; exception is not null; exception = exception.InnerException)
        {
            yield return exception.Message;
        }
    }

    [Fact]
    public async Task Starting_the_application_creates_the_eight_staff_accounts_and_no_guard()
    {
        var accounts = await InDb(db => db.Users.AsNoTracking().ToListAsync(Ct));

        Assert.Equal(8, accounts.Count);
        Assert.Equal(1, accounts.Count(a => a.Role == StaffRole.Controller));
        Assert.Equal(3, accounts.Count(a => a.Role == StaffRole.StationManager));
        Assert.Equal(4, accounts.Count(a => a.Role == StaffRole.Driver));
        Assert.DoesNotContain(accounts, a => a.UserName!.StartsWith("GRD-", StringComparison.Ordinal));
        Assert.All(accounts, a => Assert.True(a.IsActive));
    }

    [Fact]
    public async Task A_station_manager_gets_the_station_id_from_the_network_and_other_roles_get_none()
    {
        using var scope = api.Factory.Services.CreateScope();
        var expected = (await scope.ServiceProvider.GetRequiredService<IStationDirectory>().GetStationIdsAsync(["FOT"], Ct))["FOT"];

        var fot = await InDb(db => db.Users.AsNoTracking().SingleAsync(a => a.UserName == "SM-FOT", Ct));
        var controller = await InDb(db => db.Users.AsNoTracking().SingleAsync(a => a.UserName == "CTRL-01", Ct));
        var driver = await InDb(db => db.Users.AsNoTracking().SingleAsync(a => a.UserName == "DRV-01", Ct));

        Assert.Equal(expected, fot.StationId);
        Assert.Null(controller.StationId);
        Assert.Null(driver.StationId);
    }

    [Fact]
    public async Task Passwords_are_stored_as_hashes_and_the_demo_password_matches_them()
    {
        var account = await InDb(db => db.Users.AsNoTracking().SingleAsync(a => a.UserName == "CTRL-01", Ct));

        Assert.NotNull(account.PasswordHash);
        Assert.DoesNotContain(api.DemoPassword, account.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success,
            new PasswordHasher<StaffAccount>().VerifyHashedPassword(account, account.PasswordHash, api.DemoPassword));
    }

    [Fact]
    public async Task The_four_cab_devices_are_stored_with_hashed_credentials_and_their_train_set_code()
    {
        var devices = await InDb(db => db.CabDevices.AsNoTracking().OrderBy(d => d.Code).ToListAsync(Ct));

        Assert.Equal(["CAB-01", "CAB-02", "CAB-03", "CAB-04"], devices.Select(d => d.Code));
        Assert.Equal("DPS-01", devices[0].TrainSetCode);
        Assert.All(devices, d => Assert.True(d.Active));

        var hasher = new PasswordHasher<CabDevice>();
        foreach (var device in devices)
        {
            var secret = api.CabDeviceSecrets[device.Code];
            Assert.DoesNotContain(secret, device.CredentialHash);
            Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(device, device.CredentialHash, secret));
        }
    }

    [Fact]
    public async Task A_second_application_start_does_not_duplicate_accounts_or_change_their_ids()
    {
        var before = await InDb(db => db.Users.AsNoTracking().OrderBy(a => a.UserName).Select(a => a.Id).ToListAsync(Ct));

        using var second = api.CreateFactory();
        _ = second.Server;

        var after = await InDb(db => db.Users.AsNoTracking().OrderBy(a => a.UserName).Select(a => a.Id).ToListAsync(Ct));
        Assert.Equal(before, after);
        Assert.Equal(4, await InDb(db => db.CabDevices.CountAsync(Ct)));
    }

    [Fact]
    public async Task A_changed_demo_password_is_applied_at_the_next_start_because_the_seed_is_the_source_of_truth()
    {
        const string newPassword = "Changed-demo-password-1";
        try
        {
            using var restarted = api.CreateFactory(builder =>
                builder.ConfigureAppConfiguration((_, config) =>
                    config.AddInMemoryCollection(new Dictionary<string, string?> { ["Seed:DemoPassword"] = newPassword })));
            _ = restarted.Server;

            var account = await InDb(db => db.Users.AsNoTracking().SingleAsync(a => a.UserName == "DRV-04", Ct));
            Assert.Equal(PasswordVerificationResult.Success,
                new PasswordHasher<StaffAccount>().VerifyHashedPassword(account, account.PasswordHash!, newPassword));
        }
        finally
        {
            using var restored = api.CreateFactory();   // back to the fixture's password for the other tests
            _ = restored.Server;
        }
    }

    [Fact]
    public void Starting_without_the_demo_password_stops_the_application_and_names_the_command()
    {
        using var broken = api.CreateFactory(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["Seed:DemoPassword"] = "" })));

        var failure = Assert.ThrowsAny<Exception>(() => broken.Server);

        Assert.Contains(AllMessages(failure), m => m.Contains("dotnet user-secrets set \"Seed:DemoPassword\""));
    }

    [Fact]
    public void A_cab_device_registered_to_a_train_set_the_network_does_not_have_stops_the_application_and_names_both()
    {
        var folder = Path.Combine(Path.GetTempPath(), "smartrail-seed-" + Guid.NewGuid().ToString("N"));
        try
        {
            CopyRepositorySeedTo(folder);
            var devices = Path.Combine(folder, "cab-devices.json");
            File.WriteAllText(devices, File.ReadAllText(devices).Replace("\"DPS-01\"", "\"XX-99\""));

            using var broken = api.CreateFactory(builder =>
                builder.ConfigureAppConfiguration((_, config) =>
                    config.AddInMemoryCollection(new Dictionary<string, string?> { ["Seed:Path"] = folder })));

            var failure = Assert.ThrowsAny<Exception>(() => broken.Server);

            Assert.Contains(AllMessages(failure), m => m.Contains("UnknownTrainSetCode") && m.Contains("CAB-01") && m.Contains("XX-99"));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static void CopyRepositorySeedTo(string target)
    {
        var source = global::SmartRail.Network.Seed.SeedFolder.Find(configuredPath: null);   // shared/seed in the repository
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }
    }

    [Fact]
    public void Starting_without_the_jwt_signing_key_stops_the_application_and_names_the_command()
    {
        using var broken = api.CreateFactory(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = "too-short" })));

        var failure = Assert.ThrowsAny<Exception>(() => broken.Server);

        Assert.Contains(AllMessages(failure), m => m.Contains("dotnet user-secrets set \"Jwt:SigningKey\""));
    }
}
