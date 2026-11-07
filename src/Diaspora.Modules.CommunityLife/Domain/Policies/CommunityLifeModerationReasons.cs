using Diaspora.Contracts.Moderation.V1;
using Diaspora.Modules.CommunityLife.Domain.Enums;

namespace Diaspora.Modules.CommunityLife.Domain.Policies;

/// <summary>
/// An event or organisation report's reason, as the platform's moderation reasons (7 Nov 2026). The
/// member chooses from the community life list; the case, the decision and the notice to the
/// organiser use the platform's.
/// </summary>
public static class CommunityLifeModerationReasons
{
    public static ModerationReasonCode Map(CommunityLifeReportReason reason) => reason switch
    {
        CommunityLifeReportReason.Spam => ModerationReasonCode.Spam,
        CommunityLifeReportReason.Duplicate => ModerationReasonCode.Duplicate,
        CommunityLifeReportReason.Fraud => ModerationReasonCode.FraudOrScam,
        CommunityLifeReportReason.Impersonation => ModerationReasonCode.Impersonation,
        CommunityLifeReportReason.InappropriateContent => ModerationReasonCode.OffensiveOrHarassment,

        // Hate speech attacks people for who they are, which is what the platform's Discriminatory
        // names; offensive or harassing is the inappropriate-content reason above. Keeping them apart
        // also keeps the two reports distinguishable on the case. Both count as serious.
        CommunityLifeReportReason.HateSpeech => ModerationReasonCode.Discriminatory,
        CommunityLifeReportReason.Misinformation => ModerationReasonCode.FalseOrMisleading,

        // An event advertised that never took place is a false statement about it, whatever the
        // organiser meant by it.
        CommunityLifeReportReason.EventDidNotHappen => ModerationReasonCode.FalseOrMisleading,
        _ => ModerationReasonCode.Other
    };
}
