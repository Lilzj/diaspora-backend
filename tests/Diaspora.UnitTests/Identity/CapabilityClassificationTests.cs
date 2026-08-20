using Diaspora.Application.Identity.Authorization;
using Diaspora.Contracts.Authorization;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Identity;

/// <summary>
/// The capability classification: what a capability is <em>for</em>, as distinct from when it
/// opens.
/// <para>
/// The classification is derived rather than declared, so these tests are also the proof that
/// no frozen contract needed a new property to make B2.0.1 possible.
/// </para>
/// </summary>
public class CapabilityClassificationTests
{
    private const string Module = "TestModule";

    // ── Feature gates, by tier ───────────────────────────────────────────

    [Theory]
    [InlineData(AccessTier.Onboarding, CapabilityCategory.Onboarding)]
    [InlineData(AccessTier.Limited, CapabilityCategory.Member)]
    [InlineData(AccessTier.PendingVerification, CapabilityCategory.Member)]
    [InlineData(AccessTier.Verified, CapabilityCategory.Trusted)]
    public void A_gate_classifies_from_the_tier_it_opens_at(
        AccessTier tier, CapabilityCategory expected)
    {
        Gate(tier).Classify().Should().Be(expected);
    }

    [Fact]
    public void A_staff_only_gate_is_administrative_whatever_tier_it_names()
    {
        // Order matters: a moderation queue at Verified is administrative, not trusted.
        CapabilityClassifier
            .Classify(new FeatureDefinition("m.moderate", Module, "")
            {
                MinimumTier = AccessTier.Verified,
                StaffOnly = true
            })
            .Should().Be(CapabilityCategory.Administrative);

        CapabilityClassifier
            .Classify(new FeatureDefinition("m.moderate.early", Module, "")
            {
                MinimumTier = AccessTier.Limited,
                StaffOnly = true
            })
            .Should().Be(CapabilityCategory.Administrative);
    }

    // ── Permissions, by role and audience ────────────────────────────────

    [Fact]
    public void A_service_permission_is_administrative_whatever_its_tier_says()
    {
        // ADR-002: a service permission reaches a token only through IServiceTokenIssuer, and
        // no tier or role resolves to one. That outranks everything else in the classifier.
        CapabilityClassifier
            .Classify(new PermissionDefinition("m:apply-decision", Module, "")
            {
                MinimumTier = AccessTier.Limited,
                Audience = PermissionAudience.Service
            })
            .Should().Be(CapabilityCategory.Administrative);
    }

    [Fact]
    public void A_permission_restricted_to_staff_roles_is_administrative()
    {
        CapabilityClassifier
            .Classify(new PermissionDefinition("m:moderate", Module, "")
            {
                MinimumTier = AccessTier.Verified,
                Roles = new HashSet<string> { "Moderator", "Admin", "SuperAdmin" }
            })
            .Should().Be(CapabilityCategory.Administrative);
    }

    [Fact]
    public void A_permission_restricted_to_a_non_staff_role_is_a_contributor_capability()
    {
        // Nothing on the platform is in this category today. The category exists so the first
        // genuinely role-scoped, non-staff capability has somewhere correct to go.
        CapabilityClassifier
            .Classify(new PermissionDefinition("m:manage-own-business", Module, "")
            {
                MinimumTier = AccessTier.Limited,
                Roles = new HashSet<string> { "ServiceProvider" }
            })
            .Should().Be(CapabilityCategory.Contributor);
    }

    [Fact]
    public void A_mixed_role_set_containing_a_non_staff_role_is_a_contributor_capability()
    {
        CapabilityClassifier
            .Classify(new PermissionDefinition("m:mixed", Module, "")
            {
                Roles = new HashSet<string> { "ServiceProvider", "Admin" }
            })
            .Should().Be(CapabilityCategory.Contributor);
    }

    [Fact]
    public void A_permission_no_tier_grants_and_no_role_names_is_administrative()
    {
        CapabilityClassifier
            .Classify(new PermissionDefinition("m:unreachable", Module, "")
            {
                MinimumTier = null
            })
            .Should().Be(CapabilityCategory.Administrative);
    }

    // ── Tier and category must agree ─────────────────────────────────────

    [Theory]
    [InlineData(AccessTier.Onboarding, CapabilityCategory.Onboarding, true)]
    [InlineData(AccessTier.Limited, CapabilityCategory.Member, true)]
    [InlineData(AccessTier.PendingVerification, CapabilityCategory.Member, true)]
    [InlineData(AccessTier.Verified, CapabilityCategory.Trusted, true)]
    [InlineData(AccessTier.Onboarding, CapabilityCategory.Member, false)]
    [InlineData(AccessTier.Limited, CapabilityCategory.Trusted, false)]
    [InlineData(AccessTier.Verified, CapabilityCategory.Onboarding, false)]
    public void A_tier_is_consistent_with_a_category_only_within_its_band(
        AccessTier tier, CapabilityCategory category, bool expected)
    {
        CapabilityClassifier.TierMatchesCategory(tier, category).Should().Be(expected);
    }

    [Theory]
    [InlineData(CapabilityCategory.Contributor)]
    [InlineData(CapabilityCategory.Administrative)]
    public void A_role_scoped_category_is_consistent_with_every_tier(CapabilityCategory category)
    {
        // The role is the gate, so the tier carries no information.
        foreach (var tier in Enum.GetValues<AccessTier>())
        {
            CapabilityClassifier.TierMatchesCategory(tier, category).Should().BeTrue();
        }
    }

    // ── Reachability ─────────────────────────────────────────────────────

    [Fact]
    public void An_onboarding_subject_reaches_onboarding_capabilities_only()
    {
        CapabilityClassifier
            .IsReachable(AccessTier.Onboarding, CapabilityCategory.Onboarding)
            .Should().BeTrue();

        CapabilityClassifier
            .IsReachable(AccessTier.Onboarding, CapabilityCategory.Member)
            .Should().BeFalse();
    }

    [Fact]
    public void A_limited_subject_reaches_onboarding_and_member_capabilities()
    {
        CapabilityClassifier
            .IsReachable(AccessTier.Limited, CapabilityCategory.Onboarding)
            .Should().BeTrue();

        CapabilityClassifier
            .IsReachable(AccessTier.Limited, CapabilityCategory.Member)
            .Should().BeTrue();

        CapabilityClassifier
            .IsReachable(AccessTier.Limited, CapabilityCategory.Trusted)
            .Should().BeFalse();
    }

    // ── The membership-vocabulary guard ──────────────────────────────────

    [Theory]
    [InlineData("community.feed")]
    [InlineData("community.posting")]
    [InlineData("community:comment")]
    [InlineData("community:react")]
    [InlineData("community:follow")]
    [InlineData("messaging.send")]
    [InlineData("guides.publish")]
    [InlineData("marketplace.browse")]
    [InlineData("jobs.apply")]
    public void Membership_vocabulary_is_recognised(string name)
    {
        CapabilityClassifier.ReadsAsMembershipPrivilege(name).Should().BeTrue();
    }

    [Theory]
    [InlineData("media.upload")]
    [InlineData("media.upload.photo")]
    [InlineData("media.upload.document")]
    [InlineData("media.signed-urls")]
    [InlineData("identity:profile:read")]
    [InlineData("location:catalog:read")]
    public void The_sanctioned_onboarding_capabilities_are_not_membership_privileges(string name)
    {
        CapabilityClassifier.ReadsAsMembershipPrivilege(name).Should().BeFalse();
    }

    [Fact]
    public void The_guard_matches_whole_segments_rather_than_substrings()
    {
        // "post" hiding inside a longer word must not trip the guard, or the check becomes
        // noise that somebody eventually suppresses.
        CapabilityClassifier.ReadsAsMembershipPrivilege("media.upload.postcard-scan")
            .Should().BeFalse();

        CapabilityClassifier.ReadsAsMembershipPrivilege("community.post").Should().BeTrue();
    }

    private static FeatureDefinition Gate(AccessTier tier) =>
        new("m.thing", Module, "") { MinimumTier = tier };
}

file static class ClassificationExtensions
{
    public static CapabilityCategory Classify(this FeatureDefinition feature) =>
        CapabilityClassifier.Classify(feature);
}
