using Diaspora.Contracts.Moderation.V1;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Policies;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Marketplace;

/// <summary>
/// Every marketplace report reason reaches a Trust case as one of the platform's reasons
/// (7 Nov 2026), and the two that list a case on the first report stay that way.
/// </summary>
public sealed class MarketplaceModerationReasonsTests
{
    public static TheoryData<ListingReportReason, ModerationReasonCode> Expected => new()
    {
        { ListingReportReason.Fraud, ModerationReasonCode.FraudOrScam },
        { ListingReportReason.Counterfeit, ModerationReasonCode.FraudOrScam },
        { ListingReportReason.Prohibited, ModerationReasonCode.ProhibitedItem },
        { ListingReportReason.Duplicate, ModerationReasonCode.Duplicate },
        { ListingReportReason.Spam, ModerationReasonCode.Spam },
        { ListingReportReason.Offensive, ModerationReasonCode.OffensiveOrHarassment },
        { ListingReportReason.Miscategorised, ModerationReasonCode.Other },
        { ListingReportReason.Other, ModerationReasonCode.Other }
    };

    [Theory]
    [MemberData(nameof(Expected))]
    public void Each_reason_maps_to_its_platform_reason(
        ListingReportReason reason, ModerationReasonCode expected) =>
        MarketplaceModerationReasons.Map(reason).Should().Be(expected);

    [Fact]
    public void Every_reason_is_mapped_and_none_is_left_to_the_fallback_by_accident()
    {
        var covered = Expected.Select(row => (ListingReportReason)row[0]).ToHashSet();

        covered.Should().BeEquivalentTo(
            Enum.GetValues<ListingReportReason>(),
            "a new marketplace reason needs a deliberate platform reason and a row here");

        foreach (var reason in Enum.GetValues<ListingReportReason>())
        {
            Enum.IsDefined(MarketplaceModerationReasons.Map(reason)).Should().BeTrue();
        }
    }

    /// <summary>
    /// Fraud and counterfeit flagged a listing on the first report in this module; they still list
    /// the Trust case on the first report.
    /// </summary>
    [Theory]
    [InlineData(ListingReportReason.Fraud)]
    [InlineData(ListingReportReason.Counterfeit)]
    public void Fraud_and_counterfeit_list_a_case_on_the_first_report(ListingReportReason reason) =>
        ModerationReasons.QueuesOnFirstReport(MarketplaceModerationReasons.Map(reason)).Should().BeTrue();
}
