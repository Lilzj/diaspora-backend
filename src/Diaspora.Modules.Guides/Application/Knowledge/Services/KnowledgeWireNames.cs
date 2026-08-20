using Diaspora.Modules.Guides.Domain.Enums;

namespace Diaspora.Modules.Guides.Application.Knowledge.Services;

/// <summary>
/// The one place a Knowledge Directory enum becomes a wire string and back.
/// <para>
/// <b>Lower-case kebab throughout, and that differs from B8 deliberately.</b> B8 emits enum names
/// for statuses and kebab only for the sort keys that travel in query strings. Every value here
/// travels in a query string or is a section key a client switches on and renders as a heading
/// anchor, so all of it is kebab and none of it is <c>ToString()</c>. Mixing the two conventions
/// inside one module would be worse than either; picking the one that fits what this surface
/// actually carries is the reason it is picked.
/// </para>
/// <para>
/// Parsing is case-insensitive and forgiving on the way in, exact on the way out. A stale client
/// sending a sort key a later deployment removed gets a differently ordered page rather than an
/// error, which on a browse screen is always the right trade.
/// </para>
/// </summary>
public static class KnowledgeWireNames
{
    // ── Section keys ────────────────────────────────────────────────────────────────────────

    public static string ToWire(KnowledgeSectionKind kind) => kind switch
    {
        KnowledgeSectionKind.Overview => "overview",
        KnowledgeSectionKind.Featured => "featured",
        KnowledgeSectionKind.MostHelpful => "most-helpful",
        KnowledgeSectionKind.RecentlyUpdated => "recently-updated",
        KnowledgeSectionKind.Official => "official",
        KnowledgeSectionKind.Community => "community",
        KnowledgeSectionKind.RecommendedNext => "recommended-next",
        _ => "popular-questions"
    };

    // ── Learning path status ────────────────────────────────────────────────────────────────

    public static string ToWire(LearningPathStatus status) => status switch
    {
        LearningPathStatus.Draft => "draft",
        LearningPathStatus.Published => "published",
        _ => "archived"
    };

    /// <summary>Unknown falls back to published: a listing filter must not error over a status.</summary>
    public static LearningPathStatus ParsePathStatus(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "draft" => LearningPathStatus.Draft,
            "archived" => LearningPathStatus.Archived,
            _ => LearningPathStatus.Published
        };

    // ── Country context counters ────────────────────────────────────────────────────────────

    public static string ToWire(CountryContextKind kind) => kind switch
    {
        CountryContextKind.Guides => "guides",
        CountryContextKind.Jobs => "jobs",
        CountryContextKind.Housing => "housing",
        CountryContextKind.Marketplace => "marketplace",
        CountryContextKind.Events => "events",
        CountryContextKind.Organizations => "organizations",
        _ => "providers"
    };

    // ── Difficulty, reused from B8 rather than redeclared ───────────────────────────────────

    /// <summary>
    /// The wire name for a guide difficulty, in this surface's kebab vocabulary.
    /// <para>
    /// The <em>values</em> are B8's — the enum is B8's, and a second difficulty scale on one
    /// platform would be indefensible. Only the spelling differs, because a discovery filter
    /// arrives as <c>?difficulty=beginner</c> in a URL and not as <c>Beginner</c>.
    /// </para>
    /// </summary>
    public static string DifficultyToWire(GuideDifficulty difficulty) =>
        difficulty.ToString().ToLowerInvariant();

    /// <summary>Null on unknown, so the caller decides whether an unknown filter is an error.</summary>
    public static GuideDifficulty? ParseDifficulty(string? value) =>
        Enum.TryParse<GuideDifficulty>(value?.Trim(), ignoreCase: true, out var parsed)
        && Enum.IsDefined(parsed)
            ? parsed
            : null;

    // ── Discovery sorts, reused from B8 rather than redeclared ──────────────────────────────

    /// <summary>
    /// The knowledge sort vocabulary, which is B8's plus <c>relevance</c>.
    /// <para>
    /// Relevance is the one sort only a search engine can produce, and it is the default when a
    /// phrase was given. Every other key maps onto a <c>GuideSortOrder</c> B8 already knows how to
    /// order and page by, so the directory adds a vocabulary rather than a second ordering
    /// implementation.
    /// </para>
    /// </summary>
    public const string RelevanceSort = "relevance";

    /// <summary>Whether a sort key asks for relevance, which only an engine can answer.</summary>
    public static bool IsRelevance(string? value) =>
        string.Equals(value?.Trim(), RelevanceSort, StringComparison.OrdinalIgnoreCase);
}
