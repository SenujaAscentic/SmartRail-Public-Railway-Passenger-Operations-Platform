using SmartRail.Identity.Seed;
using Xunit;

namespace SmartRail.UnitTests.Identity;

// The staff and cab-device seed files are checked before anything is written, like the network seed.
public class IdentitySeedValidatorTests
{
    private static readonly IReadOnlyDictionary<string, Guid> Stations = new Dictionary<string, Guid> { ["FOT"] = Guid.NewGuid() };

    private static StaffSeedFile Staff(params StaffSeedAccount[] accounts) => new("Seed:DemoPassword", accounts);
    private static CabDeviceSeedFile Devices(params CabDeviceSeedEntry[] devices) => new(devices);

    private static StaffSeedAccount Controller(string number = "CTRL-01") => new(number, "Demo Controller", "Controller", null, true);
    private static StaffSeedAccount Manager(string number = "SM-FOT", string? station = "FOT") => new(number, "Demo Manager", "StationManager", station, true);
    private static CabDeviceSeedEntry Cab(string id = "CAB-01") => new(id, "DPS-01", true, $"Seed:CabDevice:{id}:Token");

    private static string? AllSecrets(string key) => "a-secret";

    private static IReadOnlyList<IdentitySeedError> Validate(StaffSeedFile staff, CabDeviceSeedFile devices, Func<string, string?>? secret = null) =>
        IdentitySeedValidator.Validate(staff, devices, Stations, secret ?? AllSecrets);

    private static void AssertError(IdentitySeedRule rule, StaffSeedFile staff, CabDeviceSeedFile? devices = null, Func<string, string?>? secret = null) =>
        Assert.Contains(Validate(staff, devices ?? Devices(Cab()), secret), e => e.Rule == rule);

    [Fact]
    public void A_valid_seed_has_no_errors()
    {
        Assert.Empty(Validate(Staff(Controller(), Manager()), Devices(Cab())));
    }

    [Fact]
    public void Duplicate_staff_numbers_are_rejected_ignoring_case()
    {
        AssertError(IdentitySeedRule.DuplicateStaffNumber, Staff(Controller("CTRL-01"), Controller("ctrl-01")));
    }

    [Fact]
    public void An_empty_staff_number_is_rejected()
    {
        AssertError(IdentitySeedRule.EmptyStaffNumber, Staff(Controller(" ")));
    }

    [Fact]
    public void An_unknown_role_is_rejected()
    {
        AssertError(IdentitySeedRule.UnknownRole, Staff(Controller() with { Role = "Admin" }));
    }

    [Fact]
    public void A_station_manager_must_have_a_station_code()
    {
        AssertError(IdentitySeedRule.StationManagerNeedsStation, Staff(Manager(station: null)));
    }

    [Fact]
    public void Only_a_station_manager_may_have_a_station_code()
    {
        AssertError(IdentitySeedRule.StationOnlyForStationManager, Staff(Controller() with { StationCode = "FOT" }));
    }

    [Fact]
    public void A_station_code_that_the_network_does_not_know_is_rejected()
    {
        AssertError(IdentitySeedRule.UnknownStationCode, Staff(Manager(station: "XXX")));
    }

    [Fact]
    public void A_missing_demo_password_is_rejected_with_the_command_that_sets_it()
    {
        var errors = Validate(Staff(Controller()), Devices(Cab()), key => key == "Seed:DemoPassword" ? null : "x");

        var error = Assert.Single(errors, e => e.Rule == IdentitySeedRule.MissingSecret);
        Assert.Contains("dotnet user-secrets set \"Seed:DemoPassword\"", error.Message);
    }

    [Fact]
    public void A_missing_cab_device_secret_is_rejected()
    {
        AssertError(IdentitySeedRule.MissingSecret, Staff(Controller()), Devices(Cab()), key => key.StartsWith("Seed:CabDevice") ? "" : "x");
    }

    [Fact]
    public void Duplicate_cab_device_ids_are_rejected()
    {
        AssertError(IdentitySeedRule.DuplicateDeviceId, Staff(Controller()), Devices(Cab("CAB-01"), Cab("CAB-01")));
    }

    [Fact]
    public void A_cab_device_needs_a_train_set_code()
    {
        AssertError(IdentitySeedRule.EmptyTrainSetCode, Staff(Controller()), Devices(Cab() with { TrainSetCode = "" }));
    }

    [Fact]
    public void Error_messages_never_contain_the_secret_value()
    {
        var errors = Validate(Staff(Controller(), Controller()), Devices(Cab()), key => "TOP-SECRET-VALUE");

        Assert.DoesNotContain(errors, e => e.Message.Contains("TOP-SECRET-VALUE"));
    }
}
