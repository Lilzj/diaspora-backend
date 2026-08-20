namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// What the platform knows about one country, counted.
/// <para>
/// <b>Two kinds of number, and they are deliberately in one response.</b> The first block counts
/// the knowledge — how many guides, how many verified, how many official. The second counts the
/// use — how many people opened the directory, how many of them went on to read something. An
/// editorial team needs both together, because the interesting cases are the mismatches: a country
/// with forty guides and no readers has a discovery problem, and one with high traffic and eight
/// guides has a commissioning problem.
/// </para>
/// <para>
/// Usage numbers are aggregate totals with no reader identity behind them anywhere. Rates are
/// computed rather than stored, and are zero rather than undefined when nothing has happened yet —
/// a client should never have to guard against a division that already happened on the server.
/// </para>
/// </summary>
public sealed record KnowledgeStatisticsResponse
{
    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public required string CountryCode { get; init; }

    // ── What exists ─────────────────────────────────────────────────────────────────────────

    public required int TotalGuides { get; init; }

    /// <summary>Guides Trust has verified.</summary>
    public required int VerifiedGuides { get; init; }

    /// <summary>Guides carrying an authority badge — narrower than verified.</summary>
    public required int OfficialGuides { get; init; }

    /// <summary>Everything else: written by members, unverified, and most of the knowledge.</summary>
    public required int CommunityGuides { get; init; }

    /// <summary>Guides a curator placed on this country's shelf.</summary>
    public required int FeaturedGuides { get; init; }

    /// <summary>Navigation rows the directory lists, hidden ones excluded.</summary>
    public required int CategoryCount { get; init; }

    /// <summary>Published learning paths for this country.</summary>
    public required int LearningPathCount { get; init; }

    /// <summary>The languages guides for this country are written in.</summary>
    public IReadOnlyList<string> Languages { get; init; } = [];

    /// <summary>When something was last published here. Null for a country with no guides yet.</summary>
    public DateTimeOffset? LastPublishedAt { get; init; }

    // ── What people did with it ─────────────────────────────────────────────────────────────

    public required long DirectoryViews { get; init; }

    public required long CategoryViews { get; init; }

    /// <summary>Guides opened from the directory. Not the same as a guide's own view count.</summary>
    public required long GuideDiscoveries { get; init; }

    public required long Searches { get; init; }

    /// <summary>Guides opened per directory view. Zero when nobody has arrived yet.</summary>
    public required double DiscoveryRate { get; init; }

    /// <summary>Guides opened per directory impression, country page and category pages together.</summary>
    public required double ClickThroughRate { get; init; }

    /// <summary>Of the paths somebody started here, how many they finished.</summary>
    public required double PathCompletionRate { get; init; }
}
