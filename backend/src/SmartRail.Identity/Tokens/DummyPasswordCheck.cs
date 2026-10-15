using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using SmartRail.Identity.Model;

namespace SmartRail.Identity.Tokens;

// When the staff number is unknown there is no stored hash to check, so the answer would come back faster than for a
// wrong password, and the speed would reveal which staff numbers exist. This check does the same work against a
// throw-away hash, so both cases take the same time.
internal static class DummyPasswordCheck
{
    // A hash of a random password, made once, with Identity's own hasher so it costs the same as a real one.
    private static readonly Lazy<string> Hash = new(() =>
        new PasswordHasher<StaffAccount>().HashPassword(new StaffAccount(), Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))));

    // Called at start-up, so the first unknown number does not pay for creating the hash (and stand out).
    public static void Prepare()
    {
        _ = Hash.Value;
        _ = DeviceHash.Value;
    }

    public static void Run(IPasswordHasher<StaffAccount> hasher, string password) =>
        hasher.VerifyHashedPassword(new StaffAccount(), Hash.Value, password);

    // The same for an unknown cab device: one hash check against a throw-away hash. Always answers false.
    public static bool RunDevice(string secret)
    {
        new PasswordHasher<CabDevice>().VerifyHashedPassword(new CabDevice(), DeviceHash.Value, secret);
        return false;
    }

    private static readonly Lazy<string> DeviceHash = new(() =>
        new PasswordHasher<CabDevice>().HashPassword(new CabDevice(), Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))));
}
