using Diaspora.Modules.Recommendations.Domain.Enums;

namespace Diaspora.Modules.Recommendations.Extensibility.Feed;

/// <summary>
/// One row on the home feed, as a module declares it.
/// <para>
/// <b>The home feed is a list of sections, not a list of cards, and the client already believes
/// that.</b> The mobile app renders "Trending Guides" as a horizontal carousel, "Community Updates"
/// as a vertical list and the seasonal block as a panel — three different shapes for three
/// different sections. A backend that returned a flat ranked list would leave the client to invent
/// the grouping, which is how a feed ends up looking different on iOS and Android.
/// </para>
/// <para>
/// <b><see cref="RequiresPersonalization"/> is what keeps the feed honest for a member who opted
/// out.</b> A "Because you viewed housing" section for somebody who turned personalization off is
/// not a degraded section — it is a section that must not appear at all.
/// </para>
/// </summary>
public sealed record FeedSectionDefinition
{
    /// <summary>The stable key the client switches its layout on. Never localised.</summary>
    public required string Key { get; init; }

    /// <summary>The English fallback title. The client translates from <see cref="TitleResourceKey"/>.</summary>
    public required string Title { get; init; }

    /// <summary>The translation key. Same contract as a recommendation reason.</summary>
    public required string TitleResourceKey { get; init; }

    /// <summary>What this section is made of. Null mixes types — the "for you" case.</summary>
    public RecommendationTargetType? TargetType { get; init; }

    /// <summary>
    /// The surface whose strategy weights rank this section.
    /// <para>
    /// A section on the home feed may be ranked by the jobs surface's weights, which is what lets
    /// "Jobs for you" on the home screen agree with the jobs tab rather than quietly disagreeing.
    /// </para>
    /// </summary>
    public required RecommendationSurface Surface { get; init; }

    /// <summary>Lower appears higher on the screen.</summary>
    public int Order { get; init; } = 100;

    /// <summary>How many cards. Small — a home-feed row is a glance, not a page.</summary>
    public int Limit { get; init; } = 6;

    /// <summary>Omitted entirely for a member who opted out. See the class summary.</summary>
    public bool RequiresPersonalization { get; init; }

    /// <summary>
    /// Whether the section is shown to a member with no history.
    /// <para>
    /// False for "Continue learning", which has nothing to continue, and true for "Popular in your
    /// country", which is the whole cold-start answer.
    /// </para>
    /// </summary>
    public bool ShowOnColdStart { get; init; } = true;

    /// <summary>
    /// A section with fewer than this many items is dropped rather than shown half-empty.
    /// <para>
    /// A carousel with one card looks broken; an absent section looks deliberate.
    /// </para>
    /// </summary>
    public int MinimumItems { get; init; } = 1;

    /// <summary>The client route the section's "see all" affordance leads to.</summary>
    public string? SeeAllRoute { get; init; }
}
