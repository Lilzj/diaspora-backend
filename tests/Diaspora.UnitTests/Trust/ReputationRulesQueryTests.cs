using Diaspora.Modules.Trust.Application.Reputation;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Extensibility;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Trust;

/// <summary>
/// <c>GET /trust/reputation/rules</c>: what a member is told the platform pays for.
/// <para>
/// A member reads the list as promises, so it is the rules something actually scores. Four of
/// Trust's defaults are priced and paid by nothing — <c>UpdateVerified</c>, <c>ReportResolved</c>,
/// <c>ProfilePhotoAdded</c>, <c>ProfileCompleted</c> — and listing them would be the hard-coded
/// "How to earn" sentence again, just served from further away.
/// </para>
/// </summary>
public sealed class ReputationRulesQueryTests
{
    [Fact]
    public void The_rules_nothing_pays_are_the_four_marked_as_such()
    {
        DefaultReputationRules.All
            .Where(rule => !rule.IsAwarded)
            .Select(rule => rule.ActionType)
            .Should().BeEquivalentTo(
                ReputationActions.UpdateVerified,
                ReputationActions.ReportResolved,
                ReputationActions.ProfilePhotoAdded,
                ReputationActions.ProfileCompleted);
    }

    [Fact]
    public async Task The_list_leaves_out_unpaid_and_switched_off_rules()
    {
        var rules = await ListAsync(new ReputationRuleCatalog([new MarketplaceRules()]));

        rules.Select(rule => rule.ActionType).Should()
            .NotContain(
                [
                    ReputationActions.UpdateVerified,
                    ReputationActions.ReportResolved,
                    ReputationActions.ProfilePhotoAdded,
                    ReputationActions.ProfileCompleted,
                    "ListingFeatured",
                    "SaleDisputed"
                ])
            .And.Contain("SaleCompleted");

        rules.Should().HaveCount(DefaultReputationRules.All.Count - 4 + 1);
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

        rules.Last().ActionType.Should().Be(
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
