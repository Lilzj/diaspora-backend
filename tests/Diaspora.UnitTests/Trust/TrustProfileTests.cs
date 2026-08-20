using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Domain.Events;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Trust;

/// <summary>
/// The trust profile: tier derivation, badges, health, and the interaction between them.
/// <para>
/// The property under test throughout is that <c>Tier</c> is never assigned from outside —
/// it is always a consequence of evidence the profile holds. A test that could set the tier
/// directly would be testing a design that had already failed.
/// </para>
/// </summary>
public sealed class TrustProfileTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    private static readonly UserId Member = UserId.From(Guid.Parse("11111111-1111-1111-1111-111111111111"));

    private static TrustProfile NewProfile() => TrustProfile.Open(Member, Now);

    [Fact]
    public void A_new_profile_is_untrusted_and_healthy()
    {
        var profile = NewProfile();

        profile.Tier.Should().Be(TrustTier.Untrusted);
        profile.Health.Should().Be(AccountHealth.Good);
        profile.CanPublish().Should().BeFalse();
    }

    [Fact]
    public void Phone_verification_alone_reaches_basic_but_not_verified()
    {
        var profile = NewProfile();

        profile.RecordGrant(VerificationType.Phone, null, Now);

        profile.Tier.Should().Be(TrustTier.Basic);
        profile.CanPublish().Should().BeFalse("proving you own a phone is not proving you are diaspora");
    }

    [Fact]
    public void Diaspora_verification_reaches_verified_and_unlocks_publishing()
    {
        var profile = NewProfile();

        profile.RecordGrant(VerificationType.Diaspora, "GB", Now);

        profile.Tier.Should().Be(TrustTier.Verified);
        profile.CanPublish().Should().BeTrue();
        profile.HasBadge(BadgeType.VerifiedMember).Should().BeTrue();
    }

    [Fact]
    public void Country_verification_confirms_residency_and_awards_the_country_badge()
    {
        var profile = NewProfile();

        profile.RecordGrant(VerificationType.Country, "GB", Now);

        profile.HasBadge(BadgeType.CountryVerified, "GB").Should().BeTrue();
        profile.HasBadge(BadgeType.CountryVerified, "MU").Should().BeFalse(
            "a country badge is earned per country");

        var standing = profile.Countries.Single();
        standing.IsVerified.Should().BeTrue();
        standing.Points.Should().Be(ReputationThresholds.VerifiedResidentBaseline);
    }

    [Fact]
    public void Losing_a_grant_takes_back_the_badge_it_implied()
    {
        var profile = NewProfile();
        profile.RecordGrant(VerificationType.Identity, null, Now);
        profile.HasBadge(BadgeType.GovernmentIdVerified).Should().BeTrue();

        profile.RecordGrantLost(VerificationType.Identity, null, "Document expired.", Now);

        profile.HasBadge(BadgeType.GovernmentIdVerified).Should().BeFalse();
    }

    /// <summary>
    /// A member who held a grant and lost it lands on Provisional rather than being reset to
    /// Untrusted — a lapsed permit should be recoverable without starting from zero.
    /// </summary>
    [Fact]
    public void Losing_every_grant_leaves_the_member_provisional_not_untrusted()
    {
        var profile = NewProfile();
        profile.RecordGrant(VerificationType.Diaspora, "GB", Now);

        profile.RecordGrantLost(VerificationType.Diaspora, "GB", "Permit lapsed.", Now);

        profile.Tier.Should().Be(TrustTier.Provisional);
        profile.CanPublish().Should().BeFalse();
    }

    [Fact]
    public void Awarding_a_badge_twice_is_idempotent_and_raises_one_event()
    {
        var profile = NewProfile();

        profile.AwardBadge(BadgeType.EarlyMember, null, "rule", Now);
        profile.AwardBadge(BadgeType.EarlyMember, null, "rule", Now);

        profile.Badges.Should().ContainSingle();
        profile.DomainEvents.OfType<BadgeAwardedDomainEvent>()
            .Should().ContainSingle("a replayed handler must not congratulate a member twice");
    }

    [Fact]
    public void A_revoked_badge_is_reinstated_rather_than_duplicated()
    {
        var profile = NewProfile();
        profile.AwardBadge(BadgeType.Moderator, null, "admin", Now);
        profile.RevokeBadge(BadgeType.Moderator, null, "Stepped down.", Now);

        profile.AwardBadge(BadgeType.Moderator, null, "admin", Now.AddDays(30));

        profile.Badges.Should().ContainSingle("the award history is one row, not two");
        profile.HasBadge(BadgeType.Moderator).Should().BeTrue();
    }

    [Fact]
    public void Revoking_a_badge_the_member_does_not_hold_is_silent()
    {
        var profile = NewProfile();

        var revoke = () => profile.RevokeBadge(BadgeType.TopContributor, null, "n/a", Now);

        revoke.Should().NotThrow();
        profile.DomainEvents.OfType<BadgeRevokedDomainEvent>().Should().BeEmpty();
    }

    [Fact]
    public void Three_violations_suspend_the_account()
    {
        var profile = NewProfile();
        profile.RecordGrant(VerificationType.Diaspora, "GB", Now);

        profile.RecordViolation("Spam.", Now);
        profile.Health.Should().Be(AccountHealth.Restricted);

        profile.RecordViolation("Spam again.", Now);
        profile.RecordViolation("And again.", Now);

        profile.Health.Should().Be(AccountHealth.Suspended);
    }

    /// <summary>
    /// The rule that makes suspension mean something: a suspended member holds no tier,
    /// however much verification evidence sits behind them.
    /// </summary>
    [Fact]
    public void Suspension_strips_the_tier_regardless_of_evidence()
    {
        var profile = NewProfile();
        profile.RecordGrant(VerificationType.Diaspora, "GB", Now);
        profile.RecordGrant(VerificationType.Identity, null, Now);
        profile.Tier.Should().Be(TrustTier.Verified);

        profile.Suspend("Serious violation.", Now);

        profile.Tier.Should().Be(TrustTier.Untrusted);
        profile.CanPublish().Should().BeFalse();
    }

    [Fact]
    public void Reinstatement_restores_the_tier_the_evidence_supports()
    {
        var profile = NewProfile();
        profile.RecordGrant(VerificationType.Diaspora, "GB", Now);
        profile.Suspend("Mistake.", Now);

        profile.Reinstate("Appeal upheld.", Now);

        profile.Health.Should().Be(AccountHealth.Excellent);
        profile.Tier.Should().Be(TrustTier.Verified);
    }

    [Fact]
    public void An_open_appeal_puts_the_account_under_review()
    {
        var profile = NewProfile();

        profile.AppealOpened(Now);
        profile.Health.Should().Be(AccountHealth.UnderReview);

        profile.AppealClosed(Now);
        profile.Health.Should().Be(AccountHealth.Excellent);
    }

    [Fact]
    public void Country_points_never_go_negative()
    {
        var profile = NewProfile();
        profile.AddCountryPoints("GB", 50, Now);

        profile.AddCountryPoints("GB", -500, Now);

        profile.Countries.Single().Points.Should().Be(0,
            "a member can lose standing but not go into debt");
    }

    [Fact]
    public void Country_standing_is_created_once_per_country()
    {
        var profile = NewProfile();

        profile.StandingIn("gb", Now);
        profile.StandingIn("GB", Now);

        profile.Countries.Should().ContainSingle("country codes are compared case-insensitively");
    }

    [Fact]
    public void Years_of_residence_only_count_once_residency_is_verified()
    {
        var profile = NewProfile();
        var asOf = DateOnly.FromDateTime(Now.UtcDateTime);

        profile.StandingIn("GB", Now).YearsOfResidence(asOf)
            .Should().Be(0, "nothing has been verified yet");

        profile.RecordGrant(VerificationType.Country, "GB", Now, new DateOnly(2020, 1, 1));

        profile.Countries.Single().YearsOfResidence(asOf).Should().Be(6);
    }

    [Fact]
    public void A_tier_change_raises_an_event_carrying_its_reason()
    {
        var profile = NewProfile();

        profile.RecordGrant(VerificationType.Diaspora, "GB", Now);

        var changed = profile.DomainEvents.OfType<TrustLevelChangedDomainEvent>().Last();
        changed.From.Should().Be(TrustTier.Untrusted);
        changed.To.Should().Be(TrustTier.Verified);
        changed.Reason.Should().NotBeNullOrWhiteSpace(
            "a member is entitled to be told why their standing moved");
    }
}
