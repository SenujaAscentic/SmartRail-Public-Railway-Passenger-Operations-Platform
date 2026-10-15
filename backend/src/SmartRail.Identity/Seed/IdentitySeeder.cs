using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartRail.Identity.Model;
using SmartRail.Identity.Persistence;
using SmartRail.Network;

namespace SmartRail.Identity.Seed;

// Loads shared/seed/staff.json and cab-devices.json into the identity schema, in one transaction.
// Runs on every start and finds each row by its natural key (staff number, device code), so a restart changes nothing
// unless the files or the secrets changed. The seed is the source of truth: a changed demo password is applied at the next start.
internal sealed class IdentitySeeder(
    IdentityModuleDbContext db,
    UserManager<StaffAccount> users,
    IPasswordHasher<StaffAccount> passwordHasher,
    IStationDirectory stations,
    INetworkSchedule schedule,
    IConfiguration configuration,
    ILogger<IdentitySeeder> log)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task SeedAsync(string seedFolder, CancellationToken cancellationToken)
    {
        var staff = Read<StaffSeedFile>(Path.Combine(seedFolder, "staff.json"));
        var devices = Read<CabDeviceSeedFile>(Path.Combine(seedFolder, "cab-devices.json"));

        // Station codes become station IDs here, once; sign-in then needs no Network call.
        var codes = staff.Accounts.Where(a => !string.IsNullOrWhiteSpace(a.StationCode)).Select(a => a.StationCode!).Distinct().ToList();
        var stationIds = await stations.GetStationIdsAsync(codes, cancellationToken);

        // A cab device registered to a train set the network does not have would never match a journey: fail the start now.
        var trainSetCodes = (await schedule.GetScheduleAsync(cancellationToken)).TrainSets.Select(t => t.Code).ToHashSet();

        var errors = IdentitySeedValidator.Validate(staff, devices, stationIds, trainSetCodes, key => configuration[key]);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "The staff seed data has problems:" + Environment.NewLine +
                string.Join(Environment.NewLine, errors.Select(e => $"- {e.Rule}: {e.Message}")));
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var demoPassword = configuration[staff.PasswordSecretKey]!;   // present: the validator checked it
        foreach (var entry in staff.Accounts)
        {
            await UpsertAccountAsync(entry, demoPassword, stationIds);
        }

        var deviceHasher = new PasswordHasher<CabDevice>();
        foreach (var entry in devices.CabDevices)
        {
            await UpsertDeviceAsync(entry, deviceHasher, configuration[entry.CredentialSecretKey]!, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        log.LogInformation("Identity seed: {Accounts} staff accounts, {Devices} cab devices.", staff.Accounts.Count, devices.CabDevices.Count);
    }

    private async Task UpsertAccountAsync(StaffSeedAccount entry, string demoPassword, IReadOnlyDictionary<string, Guid> stationIds)
    {
        var number = entry.StaffNumber.Trim();
        var account = await users.FindByNameAsync(number);
        var isNew = account is null;
        account ??= new StaffAccount { UserName = number };

        account.Name = entry.Name;
        account.Role = Enum.Parse<StaffRole>(entry.Role);
        account.StationId = string.IsNullOrWhiteSpace(entry.StationCode) ? null : stationIds[entry.StationCode];
        account.IsActive = entry.IsActive;

        // Hash with Identity's own hasher (never custom hashing). Only hash again when the stored hash no longer matches the secret.
        var passwordMatches = account.PasswordHash is not null &&
            passwordHasher.VerifyHashedPassword(account, account.PasswordHash, demoPassword) != PasswordVerificationResult.Failed;
        if (!passwordMatches)
        {
            account.PasswordHash = passwordHasher.HashPassword(account, demoPassword);
        }

        var result = isNew ? await users.CreateAsync(account) : await users.UpdateAsync(account);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not save staff account {number}: {string.Join(", ", result.Errors.Select(e => e.Code))}");
        }
    }

    private async Task UpsertDeviceAsync(CabDeviceSeedEntry entry, PasswordHasher<CabDevice> hasher, string secret, CancellationToken cancellationToken)
    {
        var device = await db.CabDevices.SingleOrDefaultAsync(d => d.Code == entry.Id, cancellationToken);
        if (device is null)
        {
            device = new CabDevice { Code = entry.Id };
            db.CabDevices.Add(device);
        }

        device.TrainSetCode = entry.TrainSetCode;
        device.Active = entry.Active;

        var secretMatches = device.CredentialHash.Length > 0 &&
            hasher.VerifyHashedPassword(device, device.CredentialHash, secret) != PasswordVerificationResult.Failed;
        if (!secretMatches)
        {
            device.CredentialHash = hasher.HashPassword(device, secret);
        }
    }

    private static T Read<T>(string path) =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json) ?? throw new InvalidDataException($"{path}: the file is empty.");
}
