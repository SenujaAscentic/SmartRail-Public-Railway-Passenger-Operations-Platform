using SmartRail.Identity.Model;

namespace SmartRail.Identity.Seed;

internal enum IdentitySeedRule
{
    EmptyStaffNumber,
    DuplicateStaffNumber,
    UnknownRole,
    StationManagerNeedsStation,
    StationOnlyForStationManager,
    UnknownStationCode,
    MissingSecret,
    DuplicateDeviceId,
    EmptyTrainSetCode,
    UnknownTrainSetCode,
}

internal sealed record IdentitySeedError(IdentitySeedRule Rule, string Message);

// Pure code, no database and no configuration: checks the staff and cab-device seed and collects ALL problems at once.
// Messages name the account or key, never a secret value.
internal static class IdentitySeedValidator
{
    // stationIds and trainSetCodes: what the Network module knows. secret: looks up a configuration key (null or empty = not set).
    public static IReadOnlyList<IdentitySeedError> Validate(
        StaffSeedFile staff,
        CabDeviceSeedFile devices,
        IReadOnlyDictionary<string, Guid> stationIds,
        IReadOnlySet<string> trainSetCodes,
        Func<string, string?> secret)
    {
        var errors = new List<IdentitySeedError>();

        RequireSecret(staff.PasswordSecretKey, secret, errors);

        var seenNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var account in staff.Accounts)
        {
            var number = account.StaffNumber?.Trim() ?? "";
            if (number.Length == 0)
            {
                errors.Add(new(IdentitySeedRule.EmptyStaffNumber, "A staff account has no staff number."));
                continue;
            }

            if (!seenNumbers.Add(number))
            {
                errors.Add(new(IdentitySeedRule.DuplicateStaffNumber, $"Staff number {number} appears more than once."));
            }

            var roleKnown = Enum.TryParse<StaffRole>(account.Role, out var role) && Enum.IsDefined(role);
            if (!roleKnown)
            {
                errors.Add(new(IdentitySeedRule.UnknownRole, $"{number}: role \"{account.Role}\" is not one of Controller, StationManager, Driver."));
                continue;
            }

            var hasStation = !string.IsNullOrWhiteSpace(account.StationCode);
            if (role == StaffRole.StationManager && !hasStation)
            {
                errors.Add(new(IdentitySeedRule.StationManagerNeedsStation, $"{number}: a station manager needs a stationCode."));
            }
            else if (role != StaffRole.StationManager && hasStation)
            {
                errors.Add(new(IdentitySeedRule.StationOnlyForStationManager, $"{number}: only a station manager has a stationCode."));
            }
            else if (hasStation && !stationIds.ContainsKey(account.StationCode!))
            {
                errors.Add(new(IdentitySeedRule.UnknownStationCode, $"{number}: station code {account.StationCode} does not exist in the network."));
            }
        }

        var seenDevices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var device in devices.CabDevices)
        {
            if (!seenDevices.Add(device.Id))
            {
                errors.Add(new(IdentitySeedRule.DuplicateDeviceId, $"Cab device {device.Id} appears more than once."));
            }

            if (string.IsNullOrWhiteSpace(device.TrainSetCode))
            {
                errors.Add(new(IdentitySeedRule.EmptyTrainSetCode, $"Cab device {device.Id} has no trainSetCode."));
            }
            else if (!trainSetCodes.Contains(device.TrainSetCode))
            {
                errors.Add(new(IdentitySeedRule.UnknownTrainSetCode,
                    $"Cab device {device.Id}: train set {device.TrainSetCode} does not exist in the network."));
            }

            RequireSecret(device.CredentialSecretKey, secret, errors);
        }

        return errors;
    }

    // The message gives the exact command, like the connection-string message in the Network module.
    private static void RequireSecret(string key, Func<string, string?> secret, List<IdentitySeedError> errors)
    {
        if (string.IsNullOrEmpty(secret(key)))
        {
            errors.Add(new(IdentitySeedRule.MissingSecret,
                $"The secret \"{key}\" is not set. Set it with: dotnet user-secrets set \"{key}\" \"<value>\" --project backend/src/SmartRail.Api"));
        }
    }
}
