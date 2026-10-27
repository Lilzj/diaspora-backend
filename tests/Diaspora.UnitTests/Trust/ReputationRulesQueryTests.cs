using Diaspora.Modules.Trust.Application.Reputation;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Extensibility;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Trust;

/// <summary>
/// <c>GET /trust/reputation/rules</c>: what a member is told the platform pays for.
/// <para>
/// A member reads the list as promises, so it is the rules something actually scores. One of
/// Trust's defaults is priced and paid by nothing — <c>ReportResolved</c> — and listing it would be
/// the hard-coded "How to earn" sentence again, just served from further away.
/// <c>UpdateVerified</c> and <c>ProfilePhotoAdded</c> were in that list until their consumers were
/// wired (REP-13), <c>ProfileCompleted</c> until it was retired (D-11), and <c>ReportResolved</c>
/// until the reporting modules said when a report was upheld (REP-13, step 3e).
/// </para>
/// </summary>
public sealed class ReputationRulesQueryTests
{
    [Fact]
    public void Every_default_rule_is_paid()
    {
        DefaultReputationRules.All
            .Where(rule => !rule.IsAwarded)
            .Select(rule => rule.ActionType)
            .Should().BeEmpty("ReportResolved, the last unpaid rule, is paid since step 3e");
    }

    /// <summary>
    /// D-11: "profile completed" stays unpaid and its rule is retired. Email and photo already pay
    /// their own awards. The action and the profile flag stay, so any history still reads.
    /// </summary>
    [Fact]
    public void Profile_completed_is_retired_and_its_history_still_has_a_name()
    {
        DefaultReputationRules.All.Should().NotContain(rule => rule.ActionType == ReputationActions.ProfileCompleted);

        new ReputationRuleCatalog([]).LabelFor(ReputationActions.ProfileCompleted).Should().Be("Profile completed");
    }

    [Fact]
    public async Task The_list_leaves_out_unpaid_and_switched_off_rules()
    {
        var rules = await ListAsync(new ReputationRuleCatalog([new MarketplaceRules()]));

        rules.Select(rule => rule.ActionType).Should()
            .NotContain(
                [
                    ReputationActions.ProfileCompleted,
                    "ListingFeatured",
                    "SaleDisputed"
                ])
            .And.Contain("SaleCompleted")
            .And.Contain(ReputationActions.UpdateVerified)
            .And.Contain(ReputationActions.ProfilePhotoAdded)
            .And.Contain(ReputationActions.ReportResolved);

        rules.Should().HaveCount(DefaultReputationRules.All.Count + 1);
    }

    [Fact]
    public async Task The_list_is_by_dimension_then_largest_first()
    {
        var rules = await ListAsync(new ReputationRuleCatalog([]));

        rules.Select(rule => Enum.Parse<ReputationDimension>(rule.Dimension))
            .Should().BeInAscendingOrder();

        foreach (var dimension in rules.GroupBy(rule => rule.Dimension))
        {
            dimension.Select(rule => rule.Points).Should().BeInDescendingOrder();
        }

        rules[^1].ActionType.Should().Be(
            ReputationActions.ViolationRecorded, "the largest penalty is the last trust rule");
    }

    [Fact]
    public async Task A_rule_carries_its_label_and_its_caps()
    {
        var rules = await ListAsync(new ReputationRuleCatalog([new MarketplaceRules()]));

        rules.Single(rule => rule.ActionType == ReputationActions.CommentWritten).Should().BeEquivalentTo(new
        {
            Label = "Comments written",
            Dimension = "contribution",
            Points = 2,
            MaxOccurrences = (int?)250,
            CooldownHours = (int?)0,
            IsCountryScoped = false
        });

        rules.Single(rule => rule.ActionType == "SaleCompleted").Label
            .Should().Be("Sale completed", "a contributed rule with no label is named after its action");
    }

    private static async Task<IReadOnlyList<Diaspora.Modules.Trust.Contracts.V1.Responses.ReputationRuleResponse>>
        ListAsync(ReputationRuleCatalog catalog)
    {
        var result = await new ListReputationRulesQueryHandler(catalog)
            .Handle(new ListReputationRulesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    private sealed class MarketplaceRules : IReputationRuleContributor
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

            yield return new ReputationRule
            {
                ActionType = "ListingFeatured",
                Dimension = ReputationDimension.contribution,
                Points = 10,
                IsActive = false
            };

            yield return new ReputationRule
            {
                ActionType = "SaleDisputed",
                Dimension = ReputationDimension.trust,
                Points = -10,
                IsAwarded = false
            };
        }
    }
}
