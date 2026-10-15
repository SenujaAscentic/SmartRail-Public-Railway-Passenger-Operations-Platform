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

    // ---- crew sessions: an absolute 12 hours from sign-in, bound to a cab session when there is one ----

    [Fact]
    public void A_crew_token_expires_at_the_absolute_time_it_is_given_and_not_30_minutes_later()
    {
        var absolute = Now.AddHours(12);
        var cabSession = Guid.NewGuid();

        var (_, row) = RefreshTokens.Create(Guid.NewGuid(), Guid.NewGuid(), Now, absolute, cabSession);

        Assert.Equal(absolute, row.ExpiresAt);
        Assert.Equal(absolute, row.AbsoluteExpiresAt);
        Assert.Equal(cabSession, row.CabSessionId);
    }

    [Fact]
    public void The_next_token_of_a_crew_session_keeps_the_same_absolute_end_and_cab_session()
    {
        var (_, first) = RefreshTokens.Create(Guid.NewGuid(), Guid.NewGuid(), Now, Now.AddHours(12), Guid.NewGuid());

        var (_, next) = RefreshTokens.Next(first, Now.AddHours(5));

        Assert.Equal(first.FamilyId, next.FamilyId);
        Assert.Equal(first.AccountId, next.AccountId);
        Assert.Equal(first.CabSessionId, next.CabSessionId);
        Assert.Equal(Now.AddHours(12), next.ExpiresAt);   // refreshing does not extend the shift
    }

    [Fact]
    public void The_next_token_of_a_staff_session_gets_a_fresh_30_minutes()
    {
        var (_, first) = RefreshTokens.Create(Guid.NewGuid(), Guid.NewGuid(), Now);

        var (_, next) = RefreshTokens.Next(first, Now.AddMinutes(20));

        Assert.Equal(Now.AddMinutes(50), next.ExpiresAt);
        Assert.Null(next.AbsoluteExpiresAt);
        Assert.Null(next.CabSessionId);
    }

    // Grace window: two browser tabs may send the same refresh token a moment apart. The token just replaced
    // may be used ONCE more within 10 seconds; after that, or a third time, it counts as copied (threat T-14).
    [Fact]
    public void A_token_that_was_just_replaced_can_be_used_once_more_within_10_seconds()
    {
        var (_, row) = RefreshTokens.Create(Guid.NewGuid(), Guid.NewGuid(), Now);
        row.UsedAt = Now.AddMinutes(1);

        Assert.True(row.CanBeReplayedAt(Now.AddMinutes(1).AddSeconds(5)));
        Assert.True(row.CanBeReplayedAt(Now.AddMinutes(1).AddSeconds(10)));    // the limit itself still counts
        Assert.False(row.CanBeReplayedAt(Now.AddMinutes(1).AddSeconds(11)));
    }

    [Fact]
    public void A_replay_is_allowed_only_once()
    {
        var (_, row) = RefreshTokens.Create(Guid.NewGuid(), Guid.NewGuid(), Now);
        row.UsedAt = Now.AddMinutes(1);
        row.ReplayedAt = Now.AddMinutes(1).AddSeconds(2);

        Assert.False(row.CanBeReplayedAt(Now.AddMinutes(1).AddSeconds(3)));
    }

    [Fact]
    public void A_token_that_was_never_used_or_that_is_revoked_is_not_a_replay()
    {
        var (_, fresh) = RefreshTokens.Create(Guid.NewGuid(), Guid.NewGuid(), Now);
        var (_, revoked) = RefreshTokens.Create(Guid.NewGuid(), Guid.NewGuid(), Now);
        revoked.UsedAt = Now.AddMinutes(1);
        revoked.RevokedAt = Now.AddMinutes(1).AddSeconds(1);

        Assert.False(fresh.CanBeReplayedAt(Now.AddMinutes(1)));   // unused: it is simply usable, not a replay
        Assert.False(revoked.CanBeReplayedAt(Now.AddMinutes(1).AddSeconds(2)));
    }
}
