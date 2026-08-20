using Diaspora.Application.Identity.Authorization;
using Diaspora.Contracts.Authorization;
using Diaspora.Domain.Identity.AccessStates;
using Diaspora.Domain.Identity.Users;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Identity;

/// <summary>
/// The canonical meaning of every access tier, asserted rather than documented.
/// <para>
/// B2.0.1 exists because two modules held incompatible readings of
/// <see cref="AccessTier.Onboarding"/> and nothing in the codebase could adjudicate. These
/// tests are the adjudication.
/// </para>
/// </summary>
public class AccessTierSemanticsTests
{
    // ── Anonymous ────────────────────────────────────────────────────────

    [Fact]
    public void Anonymous_admits_nothing_at_all()
    {
        AccessTierSemantics.AdmissibleCategories(AccessTier.Anonymous).Should().BeEmpty();
    }

    [Fact]
    public void An_anonymous_subject_holds_nothing()
    {
        AuthorizationSubject.Anonymous.Tier.Should().Be(AccessTier.Anonymous);
        AuthorizationSubject.Anonymous.IsStaff.Should().BeFalse();
        AuthorizationSubject.Anonymous.IsRestricted.Should().BeFalse();
    }

    // ── Onboarding ───────────────────────────────────────────────────────

    [Fact]
    public void Onboarding_admits_onboarding_capabilities_and_nothing_else()
    {
        AccessTierSemantics.AdmissibleCategories(AccessTier.Onboarding)
            .Should().BeEquivalentTo([CapabilityCategory.Onboarding]);
    }

    [Fact]
    public void Onboarding_does_not_admit_member_capabilities()
    {
        // The assertion the platform previously disagreed with itself about.
        AccessTierSemantics.Admits(AccessTier.Onboarding, CapabilityCategory.Member)
            .Should().BeFalse();

        AccessTierSemantics.Admits(AccessTier.Onboarding, CapabilityCategory.Trusted)
            .Should().BeFalse();
    }

    [Fact]
    public void Onboarding_is_where_an_unverified_phone_number_lands()
    {
        AccessTierMap.ToTier(AccessState.RegistrationStarted)
            .Should().Be(AccessTier.Onboarding);

        AccessTierMap.ToTier(AccessState.PhoneVerificationPending)
            .Should().Be(AccessTier.Onboarding);
    }

    // ── Limited ──────────────────────────────────────────────────────────

    [Fact]
    public void Limited_is_the_first_tier_of_community_participation()
    {
        AccessTierSemantics.AdmissibleCategories(AccessTier.Limited)
            .Should().BeEquivalentTo([CapabilityCategory.Onboarding, CapabilityCategory.Member]);
    }

    [Fact]
    public void Limited_still_admits_onboarding_capabilities()
    {
        // Progression is cumulative. A member who has verified their phone has not lost the
        // ability to add a profile photograph.
        AccessTierSemantics.Admits(AccessTier.Limited, CapabilityCategory.Onboarding)
            .Should().BeTrue();
    }

    [Fact]
    public void Limited_does_not_admit_trusted_capabilities()
    {
        AccessTierSemantics.Admits(AccessTier.Limited, CapabilityCategory.Trusted)
            .Should().BeFalse();
    }

    [Fact]
    public void Limited_is_where_a_verified_phone_number_lands()
    {
        AccessTierMap.ToTier(AccessState.LimitedAccess).Should().Be(AccessTier.Limited);
    }

    // ── PendingVerification ──────────────────────────────────────────────

    [Fact]
    public void Pending_verification_holds_exactly_what_limited_holds()
    {
        // Waiting on the Trust Engine is neither promotion nor demotion.
        AccessTierSemantics.AdmissibleCategories(AccessTier.PendingVerification)
            .Should().BeEquivalentTo(
                AccessTierSemantics.AdmissibleCategories(AccessTier.Limited));
    }

    [Fact]
    public void Pending_verification_is_where_a_request_in_flight_lands()
    {
        AccessTierMap.ToTier(AccessState.VerificationPending)
            .Should().Be(AccessTier.PendingVerification);

        AccessTierMap.ToTier(AccessState.VerificationUnderReview)
            .Should().Be(AccessTier.PendingVerification);
    }

    // ── Verified ─────────────────────────────────────────────────────────

    [Fact]
    public void Verified_admits_every_progression_category()
    {
        AccessTierSemantics.AdmissibleCategories(AccessTier.Verified)
            .Should().BeEquivalentTo([
                CapabilityCategory.Onboarding,
                CapabilityCategory.Member,
                CapabilityCategory.Trusted
            ]);
    }

    [Fact]
    public void Verified_is_where_a_granted_trust_decision_lands()
    {
        AccessTierMap.ToTier(AccessState.Verified).Should().Be(AccessTier.Verified);
    }

    // ── Progression vs privilege ─────────────────────────────────────────

    [Theory]
    [InlineData(AccessTier.Anonymous)]
    [InlineData(AccessTier.Onboarding)]
    [InlineData(AccessTier.Limited)]
    [InlineData(AccessTier.PendingVerification)]
    [InlineData(AccessTier.Verified)]
    public void No_tier_grants_a_role_scoped_category(AccessTier tier)
    {
        // There is no "Administrator" tier on this platform, and there must not be one: a
        // moderator at Limited is a real and correct subject. Elevated authority is a role.
        AccessTierSemantics.Admits(tier, CapabilityCategory.Administrative).Should().BeFalse();
        AccessTierSemantics.Admits(tier, CapabilityCategory.Contributor).Should().BeFalse();
    }

    [Fact]
    public void The_ladder_is_cumulative_and_ordered()
    {
        var tiers = new[]
        {
            AccessTier.Anonymous,
            AccessTier.Onboarding,
            AccessTier.Limited,
            AccessTier.PendingVerification,
            AccessTier.Verified
        };

        for (var index = 1; index < tiers.Length; index++)
        {
            var lower = AccessTierSemantics.AdmissibleCategories(tiers[index - 1]);
            var higher = AccessTierSemantics.AdmissibleCategories(tiers[index]);

            higher.IsSupersetOf(lower).Should().BeTrue(
                $"{tiers[index]} must keep everything {tiers[index - 1]} could do");
        }
    }

    // ── Restriction is a separate axis ───────────────────────────────────

    [Theory]
    [InlineData("Suspended")]
    [InlineData("Locked")]
    [InlineData("Disabled")]
    public void A_restricted_member_keeps_a_nominal_tier_rather_than_collapsing_to_anonymous(
        string stateName)
    {
        var state = AccessState.All.Single(candidate => candidate.Name == stateName);

        AccessTierMap.ToTier(state).Should().NotBe(
            AccessTier.Anonymous,
            "a suspended member is still a member — they can reach the appeal screen");

        AccessTierMap.ToSubject(state, UserRole.CommunityUser)
            .IsRestricted.Should().BeTrue("restriction is carried by the flag, not by the tier");
    }

    // ── Every tier is specified ──────────────────────────────────────────

    [Theory]
    [InlineData(AccessTier.Anonymous)]
    [InlineData(AccessTier.Onboarding)]
    [InlineData(AccessTier.Limited)]
    [InlineData(AccessTier.PendingVerification)]
    [InlineData(AccessTier.Verified)]
    public void Every_tier_states_who_belongs_and_how_a_subject_leaves(AccessTier tier)
    {
        AccessTierSemantics.Describe(tier).Should().NotBe("Unknown tier.");
        AccessTierSemantics.DescribeTransition(tier).Should().NotBe("Unknown tier.");
    }

    [Fact]
    public void Every_access_state_projects_onto_a_specified_tier()
    {
        // Identity may add a tenth state; it must land on a rung that has a meaning.
        foreach (var state in AccessState.All)
        {
            var tier = AccessTierMap.ToTier(state);

            AccessTierSemantics.Describe(tier)
                .Should().NotBe("Unknown tier.", $"{state.Name} projects onto {tier}");
        }
    }
}
