using Diaspora.Contracts.Moderation.V1;
using Diaspora.Modules.Marketplace.Domain.Enums;

namespace Diaspora.Modules.Marketplace.Domain.Policies;

/// <summary>
/// A listing report's reason, as the platform's moderation reasons (7 Nov 2026). The member chooses
/// from the marketplace list; the case, the decision and the notice to the seller use the
/// platform's.
/// </summary>
public static class MarketplaceModerationReasons
{
    public static ModerationReasonCode Map(ListingReportReason reason) => reason switch
    {
        ListingReportReason.Fraud => ModerationReasonCode.FraudOrScam,

        // Passing an item off as a brand it is not deceives the buyer, which is a fraud on them,
        // and this module has always triaged it as one: a counterfeit report flags the listing on
        // the first report, as fraud does. As ProhibitedItem it would need a second report before
        // the case was listed and would never count towards the automatic hide. A moderator who
        // finds it is better described as a prohibited item decides with that code.
        ListingReportReason.Counterfeit => ModerationReasonCode.FraudOrScam,
        ListingReportReason.Prohibited => ModerationReasonCode.ProhibitedItem,
        ListingReportReason.Duplicate => ModerationReasonCode.Duplicate,
        ListingReportReason.Spam => ModerationReasonCode.Spam,
        ListingReportReason.Offensive => ModerationReasonCode.OffensiveOrHarassment,

        // The wrong category is corrected, not removed: not misconduct, and the case decides
        // whether it is anything at all.
        ListingReportReason.Miscategorised => ModerationReasonCode.Other,
        _ => ModerationReasonCode.Other
    };
}
