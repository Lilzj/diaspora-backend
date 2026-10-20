namespace Diaspora.Modules.CommunityProgramme;

/// <summary>
/// Error codes for a community's rules, notices, statistics and a member's preferences — 21 Oct 2026.
/// <para>
/// The same <c>communities.community</c> prefix as <see cref="CommunityErrorCodes"/>, because these
/// are facts about the same aggregate family; a separate class only so the file the closure work
/// lives in does not grow a fourth concern.
/// </para>
/// </summary>
public static class CommunityGovernanceErrorCodes
{
    private const string Prefix = "communities.community";

    // ── Rules ───────────────────────────────────────────────────────────────────────────────
    public const string RuleNotFound = Prefix + ".rule.not-found";
    public const string RuleTitleRequired = Prefix + ".rule.title.required";
    public const string RuleTitleLength = Prefix + ".rule.title.length";
    public const string RuleBodyRequired = Prefix + ".rule.body.required";
    public const string RuleBodyLength = Prefix + ".rule.body.length";
    public const string RuleConsequenceLength = Prefix + ".rule.consequence.length";
    public const string TooManyRules = Prefix + ".rule.too-many";
    public const string CannotManageRules = Prefix + ".rule.cannot-manage";

    // ── Notices ─────────────────────────────────────────────────────────────────────────────
    public const string NoticeNotFound = Prefix + ".notice.not-found";
    public const string NoticeTitleRequired = Prefix + ".notice.title.required";
    public const string NoticeTitleLength = Prefix + ".notice.title.length";
    public const string NoticeBodyRequired = Prefix + ".notice.body.required";
    public const string NoticeBodyLength = Prefix + ".notice.body.length";
    public const string NoticeExpiryInPast = Prefix + ".notice.expiry.in-past";
    public const string CannotPublishNotice = Prefix + ".notice.cannot-publish";
    public const string CannotChangeNotice = Prefix + ".notice.cannot-change";

    // ── Statistics ──────────────────────────────────────────────────────────────────────────
    public const string StatisticsPeriodUnknown = Prefix + ".statistics.period.unknown";
    public const string CompareTooFew = Prefix + ".compare.too-few";
    public const string CompareTooMany = Prefix + ".compare.too-many";

    // ── Role holders ────────────────────────────────────────────────────────────────────────
    public const string RoleFilterUnknown = Prefix + ".role.filter-unknown";

    // ── Preferences ─────────────────────────────────────────────────────────────────────────
    public const string PreferenceScopeUnknown = Prefix + ".preferences.scope.unknown";
    public const string PreferenceCategoryRequired = Prefix + ".preferences.category.required";
    public const string PreferencesTooManyNotifications = Prefix + ".preferences.notifications.too-many";
    public const string PreferenceLanguageShape = Prefix + ".preferences.language.shape";
}
