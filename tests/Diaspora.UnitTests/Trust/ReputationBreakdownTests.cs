using Diaspora.Modules.Trust.Application.Mapping;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Extensibility;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Trust;

/// <summary>
/// The reputation breakdown: what each dimension's score is made of.
/// <para>
/// The screen used to multiply the profile's counters by figures of its own — thirty a phone,
/// forty a country, minus twenty a violation — none of which the server pays. The rows now come
/// from the scoring history, and each dimension's rows plus its adjustment are its score.
/// </para>
/// </summary>
public sealed class ReputationBreakdownTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private static readonly UserId Member = UserId.From(Guid.Parse("22222222-2222-2222-2222-222222222222"));

    private static readonly ReputationRuleCatalog Rules = new([]);

    [Fact]
    public void Every_default_rule_names_its_row()
    {
        DefaultReputationRules.All.Should().OnlyContain(
            rule => !string.IsNullOrWhiteSpace(rule.Label),
            "a rule Trust ships with should never fall back to its action's name");
    }

    [Fact]
    public void A_row_is_named_by_its_rule()
    {
        Rules.LabelFor(ReputationActions.ViolationRecorded).Should().Be("Policy violations");
        Rules.LabelFor(ReputationActions.ContentRemoved).Should().Be("Content removed by moderation");
    }

    /// <summary>
    /// A retired rule pays nothing new, but what it paid is still in the score and still needs a
    /// name. <see cref="ReputationRuleCatalog.For"/> hides it; the label must not.
    /// </summary>
    [Fact]
    public void A_retired_rule_still_names_its_row()
    {
        var catalog = new ReputationRuleCatalog([new RetiredRuleContributor()]);

        catalog.For("ListingFeatured").Should().BeNull();
        catalog.LabelFor("ListingFeatured").Should().Be("Listings featured");
    }

    [Fact]
    public void An_action_with_no_label_is_named_after_itself()
    {
        Rules.LabelFor("SaleCompleted").Should().Be("Sale completed");
        new ReputationRuleCatalog([new UnlabelledRuleContributor()])
            .LabelFor("SaleCompleted").Should().Be("Sale completed");
    }

    [Fact]
    public void A_new_member_has_three_empty_dimensions()
    {
        var breakdown = ReputationProfile.Open(Member, Now).ToBreakdown([], Rules);

        breakdown.Select(dimension => dimension.Dimension)
            .Should().Equal("contribution", "verification", "trust");

        breakdown.Should().OnlyContain(dimension =>
            dimension.Score == 0 && dimension.Lines.Count == 0 && dimension.Adjustment == 0);
    }

    [Fact]
    public void The_rows_are_the_points_recorded_largest_first()
    {
        var profile = ReputationProfile.Open(Member, Now);
        Apply(profile, ReputationActions.PhoneVerified);
        Apply(profile, ReputationActions.CountryVerified);

        var verification = profile
            .ToBreakdown(
                [
                    new(ReputationDimension.verification, ReputationActions.PhoneVerified, 1, 20),
                    new(ReputationDimension.verification, ReputationActions.CountryVerified, 1, 30)
                ],
                Rules)
            .Single(dimension => dimension.Dimension == "verification");

        verification.Score.Should().Be(50);
        verification.Lines.Select(line => (line.Label, line.Count, line.Points))
            .Should().Equal(("Countries verified", 1, 30), ("Phone verified", 1, 20));
        verification.Adjustment.Should().Be(0);
    }

    /// <summary>
    /// A score is held at zero, so a penalty larger than it records its full amount and moves the
    /// score by less. The rows say what was recorded; the adjustment is the part the floor absorbed,
    /// and together they are still the score.
    /// </summary>
    [Fact]
    public void A_penalty_that_floors_the_score_shows_as_an_adjustment()
    {
        var profile = ReputationProfile.Open(Member, Now);
        Apply(profile, ReputationActions.CommunityConfirmation);
        Apply(profile, ReputationActions.ViolationRecorded);

        var trust = profile
            .ToBreakdown(
                [
                    new(ReputationDimension.trust, ReputationActions.ViolationRecorded, 1, -75),
                    new(ReputationDimension.trust, ReputationActions.CommunityConfirmation, 1, 5)
                ],
                Rules)
            .Single(dimension => dimension.Dimension == "trust");

        trust.Score.Should().Be(0);
        trust.Lines.Select(line => line.Points).Should().Equal(5, -75);
        trust.Adjustment.Should().Be(70);
        (trust.Lines.Sum(line => line.Points) + trust.Adjustment).Should().Be(trust.Score);
    }

    private static void Apply(ReputationProfile profile, string actionType) =>
        profile.Apply(Rules.For(actionType)!.ToAward(), "test", null, null, Now);

    private sealed class RetiredRuleContributor : IReputationRuleContributor
    {
        public string Module => "Marketplace";

        public IEnumerable<ReputationRule> GetRules()
        {
            yield return new ReputationRule
            {
                ActionType = "ListingFeatured",
                Dimension = ReputationDimension.contribution,
                Points = 10,
                Label = "Listings featured",
                IsActive = false
            };
        }
    }

    private sealed class UnlabelledRuleContributor : IReputationRuleContributor
    {
        public string Module => "Marketplace";

        public IEnumerable<ReputationRule> GetRules()
        {
            yield return new ReputationRule
            {
                ActionType = "SaleCompleted",
                Dimension = ReputationDimension.trust,
                Points = 10
            };
        }
    }
}
