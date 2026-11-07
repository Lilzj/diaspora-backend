using Diaspora.Contracts.Moderation.V1;
using Diaspora.Modules.Housing.Domain.Enums;

namespace Diaspora.Modules.Housing.Domain.Policies;

/// <summary>
/// A housing report's reason, as the platform's moderation reasons (7 Nov 2026). The member
/// chooses from the housing list; the case, the decision and the notice to the landlord use the
/// platform's.
/// </summary>
public static class HousingModerationReasons
{
    public static ModerationReasonCode Map(HousingReportReason reason) => reason switch
    {
        HousingReportReason.Scam => ModerationReasonCode.FraudOrScam,
        HousingReportReason.Misleading => ModerationReasonCode.FalseOrMisleading,

        // Where a home is, is the claim a seeker acts on: they travel to it, or pay a deposit for
        // it. A location that is not true is false information, whatever the cause.
        HousingReportReason.WrongLocation => ModerationReasonCode.FalseOrMisleading,
        HousingReportReason.Discriminatory => ModerationReasonCode.Discriminatory,
        HousingReportReason.NotTheOwner => ModerationReasonCode.Impersonation,
        HousingReportReason.Duplicate => ModerationReasonCode.Duplicate,
        HousingReportReason.Offensive => ModerationReasonCode.OffensiveOrHarassment,

        // Out of date is not misconduct: the case decides whether it is anything at all.
        HousingReportReason.AlreadyTaken => ModerationReasonCode.Other,

        // About the building, not the advert: no platform reason describes a property's condition,
        // and the reporter's own words are what the moderator reads.
        HousingReportReason.UnsafeProperty => ModerationReasonCode.Other,
        _ => ModerationReasonCode.Other
    };
}
