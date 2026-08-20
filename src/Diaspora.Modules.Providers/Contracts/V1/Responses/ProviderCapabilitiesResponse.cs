namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>
/// What this deployment of B7 can do, for a client to bootstrap from.
/// <para>
/// <b>Exists so the registration wizard is not hard-coded in the app.</b> The client's provider
/// types, verification kinds, pricing models and sort options are all decisions a deployment
/// makes — a country pilot may not offer government services, a regulated market may demand a
/// registration number — and an app that ships the list cannot express any of that without a
/// release. B6 published the same shape for the same reason.
/// </para>
/// </summary>
public sealed record ProviderCapabilitiesResponse
{
    /// <summary>The provider types this deployment accepts, with their requirements.</summary>
    public required IReadOnlyList<ProviderTypeOptionResponse> ProviderTypes { get; init; }

    /// <summary>The <c>ServiceDeliveryMode</c> names.</summary>
    public required IReadOnlyList<string> DeliveryModes { get; init; }

    /// <summary>The <c>PricingModel</c> names.</summary>
    public required IReadOnlyList<string> PricingModels { get; init; }

    /// <summary>The <c>ContactMethodKind</c> names.</summary>
    public required IReadOnlyList<string> ContactMethodKinds { get; init; }

    /// <summary>The <c>AvailabilityKind</c> names.</summary>
    public required IReadOnlyList<string> AvailabilityKinds { get; init; }

    /// <summary>The <c>ProviderVisibilityLevel</c> names.</summary>
    public required IReadOnlyList<string> VisibilityLevels { get; init; }

    /// <summary>The <c>ProviderSortOrder</c> names the discovery endpoint accepts.</summary>
    public required IReadOnlyList<string> SortOrders { get; init; }

    /// <summary>Ranking keys registered on this deployment. Grows when B15 or B17 ship.</summary>
    public required IReadOnlyList<string> RankingKeys { get; init; }

    /// <summary>Media roles B7 uses. Open — a role not on this list is still accepted.</summary>
    public required IReadOnlyList<string> MediaRoles { get; init; }

    // ── Ceilings, so a client can validate before submitting ────────────────────────────────

    public required int MaxServices { get; init; }

    public required int MaxGalleryItems { get; init; }

    public required int MaxTags { get; init; }

    public required int MaxServiceAreas { get; init; }

    public required int MaxLanguages { get; init; }

    public required int MaxContactMethods { get; init; }

    public required int MaxCertifications { get; init; }

    /// <summary>The client's own review body cap, confirmed by the server.</summary>
    public required int MaxReviewBodyLength { get; init; }

    /// <summary>Days a member may still edit a review they wrote.</summary>
    public required int ReviewEditWindowDays { get; init; }

    public required int MaxPageSize { get; init; }
}
