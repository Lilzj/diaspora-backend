using Diaspora.Contracts.Moderation.V1;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.CommunityLife;

/// <summary>
/// Every reason a member can give for reporting an event or an organisation reaches Trust's case as
/// one of the platform's reasons (7 Nov 2026).
/// <para>
/// The table is the decision; the sweep below is the guard. A reason added to the enum and left out
/// of the mapping would land as <c>Other</c> without anybody choosing that, so a fraud report would
/// stop listing the case at once.
/// </para>
/// </summary>
public sealed class CommunityLifeModerationReasonsTests
{
    public static TheoryData<CommunityLifeReportReason, ModerationReasonCode> Table => new()
    {
        { CommunityLifeReportReason.Spam, ModerationReasonCode.Spam },
        { CommunityLifeReportReason.Duplicate, ModerationReasonCode.Duplicate },
        { CommunityLifeReportReason.Fraud, ModerationReasonCode.FraudOrScam },
        { CommunityLifeReportReason.Impersonation, ModerationReasonCode.Impersonation },
        { CommunityLifeReportReason.InappropriateContent, ModerationReasonCode.OffensiveOrHarassment },
        { CommunityLifeReportReason.HateSpeech, ModerationReasonCode.Discriminatory },
        { CommunityLifeReportReason.Misinformation, ModerationReasonCode.FalseOrMisleading },
        { CommunityLifeReportReason.EventDidNotHappen, ModerationReasonCode.FalseOrMisleading },
        { CommunityLifeReportReason.Other, ModerationReasonCode.Other }
    };

    [Theory]
    [MemberData(nameof(Table))]
    public void Each_reason_maps_to_the_platform_reason_chosen_for_it(
        CommunityLifeReportReason reason, ModerationReasonCode expected) =>
        CommunityLifeModerationReasons.Map(reason).Should().Be(expected);

    [Fact]
    public void Every_reason_in_the_enum_is_in_the_table()
    {
        var mapped = Table.Select(row => (CommunityLifeReportReason)row[0]).ToHashSet();

        Enum.GetValues<CommunityLifeReportReason>().Should().OnlyContain(
            reason => mapped.Contains(reason),
            "a reason left out of the mapping would reach Trust as Other without anybody choosing it");
    }

    [Fact]
    public void Only_the_reason_meaning_something_else_maps_to_other()
    {
        Enum.GetValues<CommunityLifeReportReason>()
            .Where(reason => CommunityLifeModerationReasons.Map(reason) == ModerationReasonCode.Other)
            .Should().Equal(CommunityLifeReportReason.Other);
    }

    [Fact]
    public void A_fraud_report_lists_its_case_on_the_first_report()
    {
        // The module flagged fraud on the first report before the case loop existed; the mapping
        // keeps that, because FraudOrScam is the code Trust lists on one report.
        ModerationReasons.QueuesOnFirstReport(
            CommunityLifeModerationReasons.Map(CommunityLifeReportReason.Fraud)).Should().BeTrue();
    }
}
