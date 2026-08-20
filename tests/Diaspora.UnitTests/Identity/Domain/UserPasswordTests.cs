using Diaspora.Domain.Identity.Policies;
using Diaspora.Domain.Identity.ValueObjects;
using Diaspora.UnitTests.Identity.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Identity.Domain;

public class UserPasswordTests
{
    private static readonly PasswordHistoryPolicy History = PasswordHistoryPolicy.Create(3, enforced: true);

    [Fact]
    public void Changing_the_password_rotates_the_security_stamp()
    {
        var user = IdentityTestData.LimitedAccessUser();
        var before = user.Security.SecurityStamp;

        user.ChangePassword(PasswordHash.Create("hashed::new"), IdentityTestData.Now, History);

        user.Security.SecurityStamp.Should().BeGreaterThan(before,
            "every token issued before the change must stop validating");
    }

    [Fact]
    public void Changing_the_password_retires_the_old_hash_into_history()
    {
        var user = IdentityTestData.LimitedAccessUser();
        var original = user.PasswordHash;

        user.ChangePassword(PasswordHash.Create("hashed::new"), IdentityTestData.Now, History);

        user.PasswordHistory.Should().ContainSingle()
            .Which.Hash.Should().Be(original);
    }

    [Fact]
    public void Reusing_the_current_password_is_refused()
    {
        var user = IdentityTestData.LimitedAccessUser();

        var result = user.ChangePassword(user.PasswordHash, IdentityTestData.Now, History);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.User.SamePassword");
    }

    [Fact]
    public void History_is_trimmed_to_the_configured_depth()
    {
        var user = IdentityTestData.LimitedAccessUser();

        for (var i = 1; i <= 6; i++)
        {
            user.ChangePassword(
                PasswordHash.Create($"hashed::password-{i}"),
                IdentityTestData.Now.AddMinutes(i),
                History);
        }

        user.PasswordHistory.Should().HaveCount(3);
    }

    [Fact]
    public void Trimming_discards_the_oldest_entries_first()
    {
        var user = IdentityTestData.LimitedAccessUser();

        for (var i = 1; i <= 5; i++)
        {
            user.ChangePassword(
                PasswordHash.Create($"hashed::password-{i}"),
                IdentityTestData.Now.AddMinutes(i),
                History);
        }

        var retained = user.PasswordHistory.Select(entry => entry.Hash.Value).ToArray();

        retained.Should().NotContain("hashed::" + IdentityTestData.ValidPassword);
        retained.Should().Contain("hashed::password-4");
    }

    [Fact]
    public void A_disabled_history_policy_keeps_nothing()
    {
        var user = IdentityTestData.LimitedAccessUser();

        user.ChangePassword(
            PasswordHash.Create("hashed::new"), IdentityTestData.Now, PasswordHistoryPolicy.Disabled);

        user.PasswordHistory.Should().BeEmpty();
    }

    [Fact]
    public void Historical_passwords_are_detectable_through_the_supplied_matcher()
    {
        var user = IdentityTestData.LimitedAccessUser();
        var original = user.PasswordHash.Value;

        user.ChangePassword(PasswordHash.Create("hashed::new"), IdentityTestData.Now, History);

        user.MatchesHistoricalPassword(hash => hash.Value == original).Should().BeTrue();
        user.MatchesHistoricalPassword(hash => hash.Value == "hashed::never-used").Should().BeFalse();
    }

    [Fact]
    public void Upgrading_a_hash_is_not_a_password_change()
    {
        // Rehashing under stronger parameters must not invalidate the member's live
        // sessions or consume a history slot — nothing about their credential changed.
        var user = IdentityTestData.LimitedAccessUser();
        var stamp = user.Security.SecurityStamp;

        user.UpgradePasswordHash(PasswordHash.Create("hashed-v3::" + IdentityTestData.ValidPassword));

        user.Security.SecurityStamp.Should().Be(stamp);
        user.PasswordHistory.Should().BeEmpty();
        user.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Password_expiry_is_disabled_when_no_maximum_age_is_configured()
    {
        var user = IdentityTestData.LimitedAccessUser();

        user.IsPasswordExpired(IdentityTestData.Now.AddYears(10), maxAge: null).Should().BeFalse();
    }

    [Fact]
    public void Password_expiry_triggers_once_the_maximum_age_elapses()
    {
        var user = IdentityTestData.LimitedAccessUser();

        user.IsPasswordExpired(IdentityTestData.Now.AddDays(91), TimeSpan.FromDays(90))
            .Should().BeTrue();
    }
}
