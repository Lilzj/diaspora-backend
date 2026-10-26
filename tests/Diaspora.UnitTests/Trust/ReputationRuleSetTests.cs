using Diaspora.Modules.Trust.Domain;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Trust;

/// <summary>
/// The rules are versioned, and the version is on every event they price (REP-16, D-10).
/// <para>
/// The values live in code, a reviewed change like the level thresholds. Without a version on the
/// event, "why did this pay 25 and that one 50" could not be answered once a value moved, and the
/// points a rule pays (D-2, D-12) are a schedule that will move.
/// </para>
/// </summary>
public sealed class ReputationRuleSetTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    private static readonly UserId Member = UserId.From(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"));

    [Fact]
    public void Every_rule_prices_its_award_under_the_current_version()
    {
        DefaultReputationRules.All.Select(rule => rule.ToAward().RuleSetVersion)
            .Should().OnlyContain(version => version == DefaultReputationRules.Version);
    }

    [Fact]
    public void An_applied_award_records_the_version_that_priced_it()
    {
        var profile = ReputationProfile.Open(Member, Now);
        var rule = DefaultReputationRules.All.Single(item => item.ActionType == ReputationActions.GuidePublished);

        var recorded = profile.Apply(rule.ToAward(), "guide", "guide-1", null, Now);

        recorded.RuleSetVersion.Should().Be(DefaultReputationRules.Version);
    }

    /// <summary>A reversal undoes what its original's rules priced, so it carries that version.</summary>
    [Fact]
    public void A_reversal_carries_its_originals_version()
    {
        var profile = ReputationProfile.Open(Member, Now);
        var original = profile.Apply(
            ReputationAward.Of(ReputationActions.GuidePublished, ReputationDimension.contribution, 50) with
            {
                RuleSetVersion = 7
            },
            "guide", "guide-1", null, Now);

        var reversal = profile.Reverse(original, "Guide deleted.", Now, ReputationAwardKey.Reversal(original.Id));

        reversal.RuleSetVersion.Should().Be(7);
    }

    [Fact]
    public void An_award_no_rule_priced_carries_no_version()
    {
        var profile = ReputationProfile.Open(Member, Now);

        profile.Apply(ReputationAward.Of("Bespoke", ReputationDimension.trust, 5), null, null, null, Now)
            .RuleSetVersion.Should().BeNull();
    }

    /// <summary>D-2 and Q2: points only for confirmed outcomes, and nothing else pays them.</summary>
    [Fact]
    public void Only_confirmed_outcomes_pay_points()
    {
        DefaultReputationRules.All
            .Where(rule => rule.CreditPoints is not null)
            .ToDictionary(rule => rule.ActionType, rule => rule.CreditPoints)
            .Should().BeEquivalentTo(new Dictionary<string, int?>
            {
                [ReputationActions.GuideApproved] = 250,
                [ReputationActions.UpdateVerified] = 200,
                [ReputationActions.ReferralQualified] = 250
            });
    }
}
