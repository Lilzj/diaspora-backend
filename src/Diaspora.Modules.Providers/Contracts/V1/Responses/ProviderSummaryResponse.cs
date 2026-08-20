namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>
/// A provider as a discovery card renders it.
/// <para>
/// Every field maps to something the client's <c>ProviderCard</c> or its discovery tab already
/// draws, and nothing else is here. That restraint is the point: the card query loads a lighter
/// graph than the profile query, and a summary that carried services and galleries would make a
/// page of twenty cards several thousand rows to render twenty names.
/// </para>
/// <para>
/// Renames against the client are deliberate and recorded in the frontend contract report:
/// <c>listingId</c> → <c>providerId</c> (D-1), <c>title</c> → <c>name</c>,
/// <c>rating</c> → <c>reviewSummary</c>, <c>coverImageUrl</c> → <c>coverMedia</c> (D-6),
/// <c>isOpenNow</c> → <c>availability.isOpenNow</c>, nullable (D-4).
/// </para>
/// </summary>
public sealed record ProviderSummaryResponse
{
    public required Guid ProviderId { get; init; }

    /// <summary>The deep-link handle. Stable across renames.</summary>
    public required string Slug { get; init; }

    /// <summary>The client's <c>title</c>.</summary>
    public required string Name { get; init; }

    /// <summary>Card-sized. Derived from the description when the provider supplied no summary.</summary>
    public string? Summary { get; init; }

    /// <summary>The <c>ProviderType</c> name.</summary>
    public required string ProviderType { get; init; }

    /// <summary>The <c>ProviderStatus</c> name.</summary>
    public required string Status { get; init; }

    public required ProviderCategoryResponse Category { get; init; }

    public required ProviderLocationResponse Location { get; init; }

    /// <summary>A Media reference. Never a URL.</summary>
    public ProviderMediaResponse? CoverMedia { get; init; }

    /// <summary>A Media reference. Never a URL.</summary>
    public ProviderMediaResponse? LogoMedia { get; init; }

    public required ProviderReviewSummaryResponse ReviewSummary { get; init; }

    /// <summary>The rolled-up badge answer. The client's <c>verificationStatus === 'Verified'</c>.</summary>
    public required bool IsVerified { get; init; }

    public required bool IsFeatured { get; init; }

    /// <summary>Null when "open" is not a meaningful question for this provider.</summary>
    public bool? IsOpenNow { get; init; }

    /// <summary>"By appointment", "Closed for Eid". Rendered instead of Open/Closed when set.</summary>
    public string? AvailabilityText { get; init; }

    /// <summary>The <c>ServiceDeliveryMode</c> name.</summary>
    public required string DeliveryMode { get; init; }

    /// <summary>The client's tag chips.</summary>
    public required IReadOnlyList<string> Tags { get; init; }

    /// <summary>BCP-47 codes, for the "speaks your language" affordance.</summary>
    public required IReadOnlyList<string> Languages { get; init; }

    public required ProviderOwnerResponse Owner { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }
}
