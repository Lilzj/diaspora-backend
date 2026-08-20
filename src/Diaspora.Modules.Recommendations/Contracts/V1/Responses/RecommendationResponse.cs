namespace Diaspora.Modules.Recommendations.Contracts.V1.Responses;

/// <summary>
/// One recommendation, as the client receives it.
/// <para>
/// <b>A card, deliberately, and one shaped to match what the mobile app already renders.</b> The
/// existing guide, listing and update cards all carry a title, a summary, a cover image, a location
/// object and a verification flag, and this response uses those names so a recommendation renders
/// through the client's existing components rather than through a new one.
/// </para>
/// <para>
/// <b><see cref="TargetType"/> and <see cref="TargetId"/> are the contract; everything else is a
/// rendering cache.</b> The client is expected to navigate to the owning module's own endpoint for
/// anything that matters — a price, an availability, an application form. The copy here is projected
/// from published contracts and can be stale; saying so plainly is better than pretending otherwise,
/// which is why <see cref="IsProjection"/> exists.
/// </para>
/// <para>
/// <b><see cref="Reason"/> is never null.</b> A recommendation without an explanation cannot be
/// constructed on the server, so a client can render the reason line unconditionally.
/// </para>
/// <para>
/// <b><see cref="MediaReference"/> is a Media identifier and not a URL</b> — B5's rule, which this
/// module honours rather than resolving on somebody else's behalf.
/// </para>
/// </summary>
public sealed record RecommendationResponse
{
    /// <summary><c>Job</c>, <c>Guide</c>, <c>HousingListing</c>, and so on. What the client routes on.</summary>
    public required string TargetType { get; init; }

    /// <summary>The owning module's identifier. The only field here that is authoritative.</summary>
    public required Guid TargetId { get; init; }

    public required string Title { get; init; }

    public string? Summary { get; init; }

    /// <summary>The owning module's deep-link handle.</summary>
    public string? Slug { get; init; }

    /// <summary>An opaque Media identifier. Never a URL — see the class summary.</summary>
    public string? MediaReference { get; init; }

    public string? CategoryKey { get; init; }

    public required RecommendationLocationResponse Location { get; init; }

    /// <summary>Whether the owning module says this carries a Trust mark.</summary>
    public required bool IsVerified { get; init; }

    public required bool IsFeatured { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    /// <summary>Zero-based rank on the page as generated.</summary>
    public required int Position { get; init; }

    /// <summary>The final weighted score, between 0 and 1.</summary>
    public required decimal Score { get; init; }

    /// <summary>Whichever strategy contributed most.</summary>
    public required string Strategy { get; init; }

    /// <summary>Why. Never null — see the class summary.</summary>
    public required RecommendationReasonResponse Reason { get; init; }

    /// <summary>The per-strategy arithmetic, largest contribution first.</summary>
    public required IReadOnlyList<StrategyContributionResponse> Contributions { get; init; }

    /// <summary>
    /// Always true, and it is here to be honest rather than to be read.
    /// <para>
    /// The copy on this card is projected from a published contract and may lag the owning module by
    /// the length of an outbox delivery. A client that renders a price or an availability from a
    /// recommendation card is making a mistake, and a field that says so is cheaper than the incident
    /// that teaches it.
    /// </para>
    /// </summary>
    public bool IsProjection { get; init; } = true;
}
