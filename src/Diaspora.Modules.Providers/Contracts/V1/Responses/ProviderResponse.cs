namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>
/// A provider in full, as the profile screen renders it.
/// <para>
/// <b>One contract for every kind of provider.</b> A restaurant, a church, an embassy, a
/// student association and a freelance immigration adviser all come back in this shape, with the
/// blocks that do not apply simply absent — <see cref="Organization"/> is null for an individual,
/// <see cref="Services"/> is empty for a mosque, <see cref="Availability"/> reports
/// <c>AppointmentOnly</c> for a consultant. That is what the brief means by "future modules must
/// never implement their own provider model", made concrete in one payload.
/// </para>
/// <para>
/// The client today needs two types to cover this — an untyped <c>mockListings</c> element and a
/// typed <c>CommunityOrganization</c> — with overlapping fields and an <c>OrgCategory</c> member
/// literally commented <c>// future-ready</c> where they collide. This is their union, resolved.
/// </para>
/// </summary>
public sealed record ProviderResponse
{
    public required Guid ProviderId { get; init; }

    /// <summary>The deep-link handle. Stable across renames.</summary>
    public required string Slug { get; init; }

    public required string Name { get; init; }

    /// <summary>The <c>ProviderType</c> name.</summary>
    public required string ProviderType { get; init; }

    /// <summary>The <c>ProviderStatus</c> name.</summary>
    public required string Status { get; init; }

    /// <summary>Why staff suspended it, or why the owner closed it. Shown to the owner.</summary>
    public string? StatusReason { get; init; }

    /// <summary>The <c>ProviderVisibilityLevel</c> name.</summary>
    public required string Visibility { get; init; }

    /// <summary>The <c>ServiceDeliveryMode</c> name.</summary>
    public required string DeliveryMode { get; init; }

    public required bool IsFeatured { get; init; }

    public required ProviderCategoryResponse Category { get; init; }

    public required ProviderProfileResponse Profile { get; init; }

    /// <summary>Null for an individual. See the type's own note.</summary>
    public ProviderOrganizationResponse? Organization { get; init; }

    public required ProviderLocationResponse Location { get; init; }

    public required ProviderOwnerResponse Owner { get; init; }

    /// <summary>A Media reference. Never a URL.</summary>
    public ProviderMediaResponse? CoverMedia { get; init; }

    /// <summary>A Media reference. Never a URL.</summary>
    public ProviderMediaResponse? LogoMedia { get; init; }

    /// <summary>The client's <c>gallery</c>, as references rather than URLs.</summary>
    public required IReadOnlyList<ProviderMediaResponse> Gallery { get; init; }

    public required IReadOnlyList<string> Tags { get; init; }

    public required IReadOnlyList<ProviderServiceResponse> Services { get; init; }

    public required IReadOnlyList<ProviderContactMethodResponse> ContactMethods { get; init; }

    public required IReadOnlyList<ProviderLanguageResponse> Languages { get; init; }

    public required IReadOnlyList<ProviderCertificationResponse> Certifications { get; init; }

    public required IReadOnlyList<ProviderServiceAreaResponse> ServiceAreas { get; init; }

    /// <summary>Publicly listed people only, unless the caller may manage the provider.</summary>
    public required IReadOnlyList<ProviderMemberResponse> Members { get; init; }

    public required ProviderAvailabilityResponse Availability { get; init; }

    public required ProviderVerificationResponse Verification { get; init; }

    public required ProviderReviewSummaryResponse ReviewSummary { get; init; }

    /// <summary>Null unless the caller may read the provider's statistics.</summary>
    public ProviderStatisticsResponse? Statistics { get; init; }

    /// <summary>What this caller may do here. Null for an anonymous caller.</summary>
    public ProviderViewerStateResponse? ViewerState { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
}
