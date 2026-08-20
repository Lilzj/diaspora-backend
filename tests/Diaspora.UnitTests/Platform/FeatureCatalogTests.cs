using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Authorization;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Platform;

public class FeatureCatalogTests
{
    private static FeatureDefinition Feature(
        string name,
        AccessTier minimumTier = AccessTier.Limited,
        bool staffOnly = false,
        bool openWhenRestricted = false) =>
        new(name, "TestModule", name)
        {
            MinimumTier = minimumTier,
            StaffOnly = staffOnly,
            OpenWhenRestricted = openWhenRestricted
        };

    private static AuthorizationSubject Subject(
        AccessTier tier = AccessTier.Limited,
        bool isStaff = false,
        bool isRestricted = false) =>
        new(tier, "CommunityUser", isStaff, isRestricted);

    [Fact]
    public void It_composes_features_from_every_contributor()
    {
        var catalog = new FeatureCatalog(
        [
            new TestFeatureContributor("A", Feature("a.browse")),
            new TestFeatureContributor("B", Feature("b.browse"))
        ]);

        catalog.All.Select(f => f.Name).Should().BeEquivalentTo("a.browse", "b.browse");
    }

    [Fact]
    public void Duplicate_feature_names_fail_the_composition()
    {
        var act = () => new FeatureCatalog(
        [
            new TestFeatureContributor("A", Feature("shared.gate")),
            new TestFeatureContributor("B", Feature("shared.gate"))
        ]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*shared.gate*");
    }

    [Fact]
    public void A_restricted_account_is_blocked_for_account_reasons_before_anything_else()
    {
        var catalog = new FeatureCatalog(
            [new TestFeatureContributor("M", Feature("m.contribute", minimumTier: AccessTier.Verified))]);

        var evaluation = catalog.Evaluate(Subject(AccessTier.Verified, isRestricted: true));

        evaluation.Granted.Should().BeEmpty();
        evaluation.Blocked.Should().ContainSingle()
            .Which.Code.Should().Be(FeatureBlockCode.AccountRestricted);
    }

    [Fact]
    public void A_low_tier_is_told_to_progress_not_that_a_staff_gate_exists()
    {
        // Tier is evaluated before the staff flag, so a limited member sees an actionable
        // "reach a higher tier" rather than a dead-end "staff only".
        var catalog = new FeatureCatalog(
            [new TestFeatureContributor("M", Feature("m.admin", minimumTier: AccessTier.Verified, staffOnly: true))]);

        var block = catalog.Evaluate(Subject(AccessTier.Limited)).Blocked.Single();

        block.Code.Should().Be(FeatureBlockCode.RequiresHigherTier);
        block.UnlocksAtTier.Should().Be(AccessTier.Verified);
    }

    [Fact]
    public void A_staff_gate_is_closed_to_a_non_staff_member_who_has_reached_the_tier()
    {
        var catalog = new FeatureCatalog(
            [new TestFeatureContributor("M", Feature("m.moderate", minimumTier: AccessTier.Limited, staffOnly: true))]);

        catalog.IsOpen("m.moderate", Subject(isStaff: false)).Should().BeFalse();
        catalog.IsOpen("m.moderate", Subject(isStaff: true)).Should().BeTrue();
    }

    [Fact]
    public void Every_gate_gets_an_answer_and_the_two_sets_are_disjoint_and_complete()
    {
        var catalog = new FeatureCatalog(
        [
            new TestFeatureContributor("M",
                Feature("m.browse"),
                Feature("m.contribute", minimumTier: AccessTier.Verified))
        ]);

        var evaluation = catalog.Evaluate(Subject(AccessTier.Limited));

        evaluation.Granted.Should().Contain("m.browse");
        evaluation.Blocked.Select(b => b.Feature).Should().Contain("m.contribute");
        (evaluation.Granted.Count + evaluation.Blocked.Count).Should().Be(catalog.All.Count);
    }
}
