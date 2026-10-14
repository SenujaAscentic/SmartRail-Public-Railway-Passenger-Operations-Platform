using SmartRail.Identity.Tokens;
using Xunit;

namespace SmartRail.UnitTests.Identity;

// The refresh token is a long random secret in a cookie. Only its hash is stored, so a leaked database cannot be used to sign in.
public class RefreshTokensTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 4, 30, 0, TimeSpan.Zero);

    [Fact]
    public void A_new_token_expires_30_minutes_after_it_is_issued_which_is_the_idle_limit()
    {
        var (_, row) = RefreshTokens.Create(Guid.NewGuid(), Guid.NewGuid(), Now);

        Assert.Equal(Now.AddMinutes(30), row.ExpiresAt);
        Assert.Equal(Now, row.CreatedAt);
    }

    [Fact]
    public void The_stored_value_is_the_hash_of_the_secret_never_the_secret()
    {
        var (secret, row) = RefreshTokens.Create(Guid.NewGuid(), Guid.NewGuid(), Now);

        Assert.NotEqual(secret, row.TokenHash);
        Assert.Equal(RefreshTokens.Hash(secret), row.TokenHash);
    }

    [Fact]
    public void Two_tokens_are_never_the_same()
    {
        var (first, _) = RefreshTokens.Create(Guid.NewGuid(), Guid.NewGuid(), Now);
        var (second, _) = RefreshTokens.Create(Guid.NewGuid(), Guid.NewGuid(), Now);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void The_same_secret_always_gives_the_same_hash_so_it_can_be_looked_up()
    {
        Assert.Equal(RefreshTokens.Hash("abc"), RefreshTokens.Hash("abc"));
        Assert.NotEqual(RefreshTokens.Hash("abc"), RefreshTokens.Hash("abd"));
    }

    [Fact]
    public void A_token_is_usable_until_it_expires_and_not_after()
    {
        var (_, row) = RefreshTokens.Create(Guid.NewGuid(), Guid.NewGuid(), Now);

        Assert.True(row.IsUsableAt(Now.AddMinutes(29)));
        Assert.False(row.IsUsableAt(Now.AddMinutes(30)));
    }

    [Fact]
    public void A_token_that_was_already_used_or_revoked_is_not_usable()
    {
        var (_, used) = RefreshTokens.Create(Guid.NewGuid(), Guid.NewGuid(), Now);
        used.UsedAt = Now.AddMinutes(1);
        var (_, revoked) = RefreshTokens.Create(Guid.NewGuid(), Guid.NewGuid(), Now);
        revoked.RevokedAt = Now.AddMinutes(1);

        Assert.False(used.IsUsableAt(Now.AddMinutes(2)));
        Assert.False(revoked.IsUsableAt(Now.AddMinutes(2)));
    }
}
