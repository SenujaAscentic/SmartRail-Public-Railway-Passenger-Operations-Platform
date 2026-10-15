using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SmartRail.Identity.Model;
using Xunit;

namespace SmartRail.IntegrationTests.Identity;

// An unknown staff number must cost the server the same work as a wrong password, otherwise the answer time
// reveals which staff numbers exist (open point 17). Time itself is too noisy to assert, so the test counts the work:
// every sign-in attempt must run exactly one password check.
[Collection("Api")]
public class SignInTimingTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // The real hasher, with a counter on password checks.
    private sealed class CountingPasswordHasher : IPasswordHasher<StaffAccount>
    {
        private readonly PasswordHasher<StaffAccount> _inner = new();
        private int _checks;
        public int Checks => _checks;
        public void Reset() => Interlocked.Exchange(ref _checks, 0);

        public string HashPassword(StaffAccount user, string password) => _inner.HashPassword(user, password);

        public PasswordVerificationResult VerifyHashedPassword(StaffAccount user, string hashedPassword, string providedPassword)
        {
            Interlocked.Increment(ref _checks);
            return _inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
        }
    }

    private async Task<int> ChecksForAsync(string staffNumber, string password)
    {
        var counter = new CountingPasswordHasher();
        using var factory = api.CreateFactory(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IPasswordHasher<StaffAccount>>(counter)));
        _ = factory.Server;
        counter.Reset();   // the start-up seeding also checks passwords; only the sign-in counts

        await factory.NewClient().SignInAsync(staffNumber, password, Ct);
        return counter.Checks;
    }

    [Fact]
    public async Task A_wrong_password_for_a_known_number_runs_exactly_one_password_check()
    {
        Assert.Equal(1, await ChecksForAsync("CTRL-01", "not-the-password"));
    }

    [Fact]
    public async Task An_unknown_staff_number_runs_exactly_one_password_check_too()
    {
        Assert.Equal(1, await ChecksForAsync("NOBODY-99", "whatever"));
    }
}
