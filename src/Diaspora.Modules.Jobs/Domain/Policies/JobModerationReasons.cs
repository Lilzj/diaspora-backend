using Diaspora.Contracts.Moderation.V1;
using Diaspora.Modules.Jobs.Domain.Enums;

namespace Diaspora.Modules.Jobs.Domain.Policies;

/// <summary>
/// A job report's reason, as the platform's moderation reasons (7 Nov 2026). The member chooses from
/// the jobs list; the case, the decision and the notice to the employer use the platform's.
/// </summary>
public static class JobModerationReasons
{
    public static ModerationReasonCode Map(JobReportReason reason) => reason switch
    {
        JobReportReason.Scam => ModerationReasonCode.FraudOrScam,
        JobReportReason.FeeRequested => ModerationReasonCode.AsksForFees,
        JobReportReason.Discriminatory => ModerationReasonCode.Discriminatory,
        JobReportReason.Misleading => ModerationReasonCode.FalseOrMisleading,
        JobReportReason.NotTheEmployer => ModerationReasonCode.Impersonation,
        JobReportReason.Duplicate => ModerationReasonCode.Duplicate,
        JobReportReason.Offensive => ModerationReasonCode.OffensiveOrHarassment,
        // Out of date is not misconduct: the case decides whether it is anything at all.
        JobReportReason.AlreadyFilled => ModerationReasonCode.Other,
        _ => ModerationReasonCode.Other
    };
}
