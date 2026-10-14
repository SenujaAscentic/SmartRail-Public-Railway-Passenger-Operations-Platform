namespace SmartRail.Identity.Seed;

// The shapes of shared/seed/staff.json and shared/seed/cab-devices.json.
// The role stays text here so the validator can report a bad value clearly.
// The files hold no secrets, only the NAME of the configuration key where each secret lives.
internal sealed record StaffSeedFile(string PasswordSecretKey, IReadOnlyList<StaffSeedAccount> Accounts);

internal sealed record StaffSeedAccount(string StaffNumber, string Name, string Role, string? StationCode, bool IsActive);

internal sealed record CabDeviceSeedFile(IReadOnlyList<CabDeviceSeedEntry> CabDevices);

internal sealed record CabDeviceSeedEntry(string Id, string TrainSetCode, bool Active, string CredentialSecretKey);
