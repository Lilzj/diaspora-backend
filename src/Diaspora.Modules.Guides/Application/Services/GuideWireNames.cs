using Diaspora.Modules.Guides.Domain.Enums;

namespace Diaspora.Modules.Guides.Application.Services;

/// <summary>
/// The one place a domain enum becomes a wire string and back.
/// <para>
/// <b>Centralised because a serialised enum is a frozen contract, and a default
/// <c>ToString()</c> is a contract nobody decided on.</b> Everything here is the enum name
/// except the sort keys, which are lower-case kebab because they travel in query strings and
/// the frontend contract pins them that way.
/// </para>
/// <para>
/// Parsing is case-insensitive on the way in and exact on the way out. A client that sends
/// <c>published</c> where the contract says <c>Published</c> is being sloppy, not wrong;
/// emitting two spellings of the same value, on the other hand, breaks every client that
/// compares string literals.
/// </para>
/// </summary>
public static class GuideWireNames
{
    // ── Sort keys: lower-case kebab, matching the query-string vocabulary verbatim ──────────

    public static string ToWire(GuideSortOrder sort) => sort switch
    {
        GuideSortOrder.Newest => "newest",
        GuideSortOrder.MostHelpful => "most-helpful",
        GuideSortOrder.RecentlyUpdated => "recently-updated",
        GuideSortOrder.Popular => "popular",
        _ => "trending"
    };

    /// <summary>Null, empty and unknown all fall back to newest — a browse must not error over a sort key.</summary>
    public static GuideSortOrder ParseSort(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "most-helpful" or "mosthelpful" or "helpful" => GuideSortOrder.MostHelpful,
        "recently-updated" or "recentlyupdated" or "updated" => GuideSortOrder.RecentlyUpdated,
        "popular" => GuideSortOrder.Popular,
        "trending" => GuideSortOrder.Trending,
        _ => GuideSortOrder.Newest
    };

    // ── Everything else: the enum name, which is what the platform's other modules emit ──────

    public static string ToWire(GuideStatus status) => status.ToString();

    public static string ToWire(GuideVisibility visibility) => visibility.ToString();

    public static string ToWire(GuideModerationState state) => state.ToString();

    public static string ToWire(GuideBlockKind kind) => kind.ToString();

    public static string ToWire(GuideDifficulty difficulty) => difficulty.ToString();

    public static string ToWire(GuideMediaRole role) => role.ToString();

    public static string ToWire(GuideVoteKind kind) => kind.ToString();

    public static string ToWire(GuideSuggestionStatus status) => status.ToString();

    public static string ToWire(GuideVerificationState state) => state.ToString();

    public static GuideBlockKind? ParseBlockKind(string? value) => Parse<GuideBlockKind>(value);

    public static GuideVisibility? ParseVisibility(string? value) => Parse<GuideVisibility>(value);

    public static GuideDifficulty? ParseDifficulty(string? value) => Parse<GuideDifficulty>(value);

    public static GuideMediaRole? ParseMediaRole(string? value) => Parse<GuideMediaRole>(value);

    public static GuideVoteKind? ParseVoteKind(string? value) => Parse<GuideVoteKind>(value);

    /// <summary>Null on unknown, so the caller owns the error — the codes differ per field.</summary>
    private static TEnum? Parse<TEnum>(string? value) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value?.Trim(), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : null;
}
