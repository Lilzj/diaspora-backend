using Diaspora.Contracts.Moderation.V1;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Policies;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Housing;

/// <summary>
/// Every housing report reason reaches a Trust case as one of the platform's reasons
/// (7 Nov 2026), and a scam still lists the case on the first report.
/// </summary>
public sealed class HousingModerationReasonsTests
{
    public static TheoryData<HousingReportReason, ModerationReasonCode> Expected => new()
    {
        { HousingReportReason.Scam, ModerationReasonCode.FraudOrScam },
        { HousingReportReason.Duplicate, ModerationReasonCode.Duplicate },
        { HousingReportReason.Misleading, ModerationReasonCode.FalseOrMisleading },
        { HousingReportReason.AlreadyTaken, ModerationReasonCode.Other },
        { HousingReportReason.WrongLocation, ModerationReasonCode.FalseOrMisleading },
        { HousingReportReason.Discriminatory, ModerationReasonCode.Discriminatory },
        { HousingReportReason.UnsafeProperty, ModerationReasonCode.Other },
        { HousingReportReason.Offensive, ModerationReasonCode.OffensiveOrHarassment },
        { HousingReportReason.NotTheOwner, ModerationReasonCode.Impersonation },
        { HousingReportReason.Other, ModerationReasonCode.Other }
    };

    [Theory]
    [MemberData(nameof(Expected))]
    public void Each_reason_maps_to_its_platform_reason(
        HousingReportReason reason, ModerationReasonCode expected) =>
        HousingModerationReasons.Map(reason).Should().Be(expected);

    [Fact]
    public void Every_reason_is_mapped_and_none_is_left_to_the_fallback_by_accident()
    {
        var covered = Expected.Select(row => (HousingReportReason)row[0]).ToHashSet();

        covered.Should().BeEquivalentTo(
            Enum.GetValues<HousingReportReason>(),
            "a new housing reason needs a deliberate platform reason and a row here");

        foreach (var reason in Enum.GetValues<HousingReportReason>())
        {
            Enum.IsDefined(HousingModerationReasons.Map(reason)).Should().BeTrue();
        }
    }

    [Fact]
    public void A_scam_lists_the_case_on_the_first_report() =>
        ModerationReasons.QueuesOnFirstReport(HousingModerationReasons.Map(HousingReportReason.Scam))
            .Should().BeTrue();
}
