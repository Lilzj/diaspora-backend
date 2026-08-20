namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// Everything a country's front page shows, in one response.
/// <para>
/// <b>The reusable surface the brief asks every module to consume rather than rebuild.</b> Jobs
/// wanting to show "new to this country? start here", Housing wanting neighbourhood guidance beside
/// a listing, Community Life wanting culture guides beside a festival — every one of them was going
/// to write its own version of this, and every version would have had its own idea of what
/// "official" means and its own bug in the fallback when a country has no directory yet.
/// </para>
/// <para>
/// <b>The shelves are deliberately short.</b> A dashboard is a summary, not a browse: each carries
/// a handful of guides, and every one of them has a corresponding paged endpoint for somebody who
/// wants the rest. A dashboard that returned fifty guides per shelf would be a response nobody
/// renders and everybody pays for.
/// </para>
/// </summary>
public sealed record CountryDashboardResponse
{
    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public required string CountryCode { get; init; }

    /// <summary>Already localized.</summary>
    public required string DisplayName { get; init; }

    public string? Summary { get; init; }

    public string? PrimaryLanguage { get; init; }

    public IReadOnlyList<string> Languages { get; init; } = [];

    /// <summary>The headline numbers, knowledge and usage together.</summary>
    public required KnowledgeStatisticsResponse Statistics { get; init; }

    // ── The shelves ─────────────────────────────────────────────────────────────────────────

    /// <summary>What a curator put at the top for this country.</summary>
    public IReadOnlyList<GuideCardResponse> Featured { get; init; } = [];

    /// <summary>Last edited. The shelf that answers "is any of this still true".</summary>
    public IReadOnlyList<GuideCardResponse> RecentlyUpdated { get; init; } = [];

    /// <summary>Most read.</summary>
    public IReadOnlyList<GuideCardResponse> Popular { get; init; } = [];

    /// <summary>Carrying an authority badge from Trust. Narrower than verified.</summary>
    public IReadOnlyList<GuideCardResponse> Official { get; init; } = [];

    /// <summary>Written by members and not verified. Most of the knowledge.</summary>
    public IReadOnlyList<GuideCardResponse> Community { get; init; } = [];

    /// <summary>Written for somebody who has just arrived.</summary>
    public IReadOnlyList<GuideCardResponse> Beginner { get; init; } = [];

    /// <summary>Written for somebody who has been here a while and hit something specific.</summary>
    public IReadOnlyList<GuideCardResponse> Advanced { get; init; } = [];

    // ── Navigation and context ──────────────────────────────────────────────────────────────

    /// <summary>Published onboarding routes for this country, without their steps.</summary>
    public IReadOnlyList<LearningPathSummaryResponse> LearningPaths { get; init; } = [];

    /// <summary>What is written versus what is read, per navigation row.</summary>
    public IReadOnlyList<KnowledgeCategoryCountResponse> CategoryBreakdown { get; init; } = [];

    /// <summary>What people are searching for here.</summary>
    public IReadOnlyList<TrendingTopicResponse> TrendingTopics { get; init; } = [];

    /// <summary>
    /// What else the platform holds for this country. Counts and routes, never content.
    /// </summary>
    public IReadOnlyList<CountryContextBlockResponse> Context { get; init; } = [];

    /// <summary>
    /// What an authority has said about this country — the top of the briefing, the emergency
    /// numbers, and where to find the rest.
    /// <para>
    /// <b>Added by B8.2, additively, and it is the whole of how country intelligence reaches this
    /// dashboard.</b> The brief asks for two things that look like they conflict: every country
    /// dashboard now surfaces official information, and country dashboards must not be duplicated.
    /// One property resolves both — there is still exactly one country dashboard, and the
    /// authoritative layer appears on it as a band with a route to its own full surface.
    /// </para>
    /// <para>
    /// <b>Null when nothing official exists for the country yet</b>, which a client renders as an
    /// absent band rather than as an empty one. Every field on it is additive to a contract shipped
    /// clients already parse, so nothing about this is a breaking change.
    /// </para>
    /// </summary>
    public CountryIntelligenceSummaryResponse? Intelligence { get; init; }
}
