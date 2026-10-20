using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityProgramme.Domain.Errors;

/// <summary>
/// What a community's rules, notices, statistics and a member's preferences refuse — 21 Oct 2026.
/// Every sentence is shown to the member as written.
/// </summary>
public static class CommunityGovernanceErrors
{
    // ── Rules ───────────────────────────────────────────────────────────────────────────────

    public static readonly Error RuleNotFound = Error.NotFound(
        CommunityGovernanceErrorCodes.RuleNotFound, "This community has no such rule.");

    public static readonly Error RuleTitleRequired = Error.Validation(
        CommunityGovernanceErrorCodes.RuleTitleRequired, "A rule needs a title.");

    public static readonly Error RuleTitleLength = Error.Validation(
        CommunityGovernanceErrorCodes.RuleTitleLength, "A rule's title is at most 120 characters.");

    public static readonly Error RuleBodyRequired = Error.Validation(
        CommunityGovernanceErrorCodes.RuleBodyRequired, "Say what the rule asks of members.");

    public static readonly Error RuleBodyLength = Error.Validation(
        CommunityGovernanceErrorCodes.RuleBodyLength, "A rule is at most 2,000 characters.");

    public static readonly Error RuleConsequenceLength = Error.Validation(
        CommunityGovernanceErrorCodes.RuleConsequenceLength,
        "A rule's consequence is at most 500 characters.");

    public static readonly Error TooManyRules = Error.Conflict(
        CommunityGovernanceErrorCodes.TooManyRules,
        "A community can publish at most 50 rules. Combine or remove some first.");

    public static readonly Error CannotManageRules = Error.Forbidden(
        CommunityGovernanceErrorCodes.CannotManageRules,
        "Only the people who run this community can change its rules.");

    // ── Notices ─────────────────────────────────────────────────────────────────────────────

    public static readonly Error NoticeNotFound = Error.NotFound(
        CommunityGovernanceErrorCodes.NoticeNotFound, "This community has no such notice.");

    public static readonly Error NoticeTitleRequired = Error.Validation(
        CommunityGovernanceErrorCodes.NoticeTitleRequired, "A notice needs a title.");

    public static readonly Error NoticeTitleLength = Error.Validation(
        CommunityGovernanceErrorCodes.NoticeTitleLength, "A notice's title is at most 140 characters.");

    public static readonly Error NoticeBodyRequired = Error.Validation(
        CommunityGovernanceErrorCodes.NoticeBodyRequired, "Say what the notice is about.");

    public static readonly Error NoticeBodyLength = Error.Validation(
        CommunityGovernanceErrorCodes.NoticeBodyLength, "A notice is at most 4,000 characters.");

    public static readonly Error NoticeExpiryInPast = Error.Validation(
        CommunityGovernanceErrorCodes.NoticeExpiryInPast,
        "A notice cannot expire before it is published.");

    public static readonly Error CannotPublishNotice = Error.Forbidden(
        CommunityGovernanceErrorCodes.CannotPublishNotice,
        "Only the people who run this community can publish notices to it.");

    public static readonly Error CannotChangeNotice = Error.Forbidden(
        CommunityGovernanceErrorCodes.CannotChangeNotice,
        "Only the person who published this notice, or somebody who runs the community, can change it.");

    // ── Statistics ──────────────────────────────────────────────────────────────────────────

    public static readonly Error StatisticsPeriodUnknown = Error.Validation(
        CommunityGovernanceErrorCodes.StatisticsPeriodUnknown,
        "Choose a period of 7d, 30d, 90d, 12m or all.");

    public static readonly Error CompareTooFew = Error.Validation(
        CommunityGovernanceErrorCodes.CompareTooFew,
        "A comparison needs at least two communities.");

    public static readonly Error CompareTooMany = Error.Validation(
        CommunityGovernanceErrorCodes.CompareTooMany,
        "Compare at most four communities at a time.");

    // ── Role holders ────────────────────────────────────────────────────────────────────────

    public static readonly Error RoleFilterUnknown = Error.Validation(
        CommunityGovernanceErrorCodes.RoleFilterUnknown,
        "Choose a role of Member, Moderator, Ambassador, Administrator or Owner.");

    // ── Preferences ─────────────────────────────────────────────────────────────────────────

    public static readonly Error PreferenceScopeUnknown = Error.Validation(
        CommunityGovernanceErrorCodes.PreferenceScopeUnknown,
        "A notification scope is primary-only, my-communities, interested-included, "
        + "all-communities, specific or none.");

    public static readonly Error PreferenceCategoryRequired = Error.Validation(
        CommunityGovernanceErrorCodes.PreferenceCategoryRequired,
        "Every notification preference names the category it applies to.");

    public static readonly Error PreferencesTooManyNotifications = Error.Validation(
        CommunityGovernanceErrorCodes.PreferencesTooManyNotifications,
        "At most 50 notification preferences, each naming at most 50 communities.");

    public static readonly Error PreferenceLanguageShape = Error.Validation(
        CommunityGovernanceErrorCodes.PreferenceLanguageShape,
        "A content language is a language tag such as en or fr-CA.");
}
