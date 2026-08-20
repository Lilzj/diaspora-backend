using Diaspora.Modules.Guides.Domain.Enums;

namespace Diaspora.Modules.Guides.Application.Intelligence.Services;

/// <summary>
/// The one place a Country Intelligence enum becomes a wire string and back.
/// <para>
/// <b>PascalCase for the values a client renders, kebab for the ones that travel in a query
/// string.</b> That is B8's convention rather than B8.1's, and the choice is driven by the React
/// Native app this surface has to fit: its <c>UpdateCategory</c>, <c>UrgencyLevel</c> and
/// <c>UpdateStatus</c> unions are all PascalCase string literals with label and icon maps keyed on
/// them, and emitting <c>critical</c> where the app expects <c>Critical</c> would mean either a
/// remapping layer in the client or a breaking change to screens that already ship. Sort keys are
/// kebab because they arrive as <c>?sort=most-viewed</c> and nothing renders them.
/// </para>
/// <para>
/// Parsing is case-insensitive and forgiving on the way in, exact on the way out. A stale client
/// sending a sort key a later deployment removed gets a differently ordered page rather than an
/// error, which on a browse screen is always the right trade.
/// </para>
/// </summary>
public static class IntelligenceWireNames
{
    // ── Rendered values: PascalCase, matching the app's existing string unions ───────────────

    public static string ToWire(NoticeStatus status) => status.ToString();

    public static string ToWire(NoticeSeverity severity) => severity.ToString();

    public static string ToWire(NoticeVerificationState state) => state.ToString();

    public static string ToWire(AuthorityType type) => type.ToString();

    public static string ToWire(NoticeReferenceKind kind) => kind.ToString();

    public static string ToWire(HolidayKind kind) => kind.ToString();

    public static string ToWire(VisaAdvisoryStatus status) => status.ToString();

    public static string ToWire(NoticeAlertFamily family) => family.ToString();

    public static string ToWire(VisaLineKind kind) => kind.ToString();

    // ── Parsing: forgiving, and null on unknown so the caller decides ───────────────────────

    public static NoticeStatus? ParseStatus(string? value) =>
        Parse<NoticeStatus>(value);

    public static NoticeSeverity? ParseSeverity(string? value) =>
        Parse<NoticeSeverity>(value);

    public static AuthorityType? ParseAuthorityType(string? value) =>
        Parse<AuthorityType>(value);

    public static NoticeReferenceKind? ParseReferenceKind(string? value) =>
        Parse<NoticeReferenceKind>(value);

    public static HolidayKind? ParseHolidayKind(string? value) =>
        Parse<HolidayKind>(value);

    public static NoticeAlertFamily? ParseAlertFamily(string? value) =>
        Parse<NoticeAlertFamily>(value);

    // ── Sort keys: kebab, because they travel in a query string ─────────────────────────────

    public static string ToWire(NoticeSortOrder sort) => sort switch
    {
        NoticeSortOrder.Newest => "newest",
        NoticeSortOrder.Oldest => "oldest",
        NoticeSortOrder.EffectiveSoonest => "effective-soonest",
        NoticeSortOrder.ExpiringSoonest => "expiring-soonest",
        NoticeSortOrder.MostViewed => "most-viewed",
        _ => "priority"
    };

    /// <summary>
    /// Unknown falls back to the briefing order rather than erroring.
    /// <para>
    /// A listing filter must not fail over a sort key: an old app version sending one this
    /// deployment removed should get a page, and the briefing order is the one that is never wrong to
    /// give somebody looking at official information.
    /// </para>
    /// </summary>
    public static NoticeSortOrder ParseSort(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "newest" => NoticeSortOrder.Newest,
            "oldest" => NoticeSortOrder.Oldest,
            "effective-soonest" => NoticeSortOrder.EffectiveSoonest,
            "expiring-soonest" => NoticeSortOrder.ExpiringSoonest,
            "most-viewed" => NoticeSortOrder.MostViewed,
            _ => NoticeSortOrder.Priority
        };

    private static TEnum? Parse<TEnum>(string? value)
        where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value?.Trim(), ignoreCase: true, out var parsed)
        && Enum.IsDefined(parsed)
            ? parsed
            : null;
}
