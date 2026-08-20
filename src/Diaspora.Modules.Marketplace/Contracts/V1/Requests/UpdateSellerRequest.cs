namespace Diaspora.Modules.Marketplace.Contracts.V1.Requests;

/// <summary>
/// Updates a seller profile.
/// <para>
/// <b>Changing the declared kind clears an existing verification, and that is not a bug.</b> Trust
/// verified a person or verified a business; the two are different claims reviewed against
/// different evidence, and keeping the badge across the switch would let an individual verify as
/// themselves and then trade as a "Verified Business".
/// </para>
/// </summary>
public sealed record UpdateSellerRequest
{
    public required string DisplayName { get; init; }

    public string? Kind { get; init; }

    public required string CountryCode { get; init; }

    public string? LegalName { get; init; }

    public string? RegistrationNumber { get; init; }

    public string? Bio { get; init; }

    public string? Phone { get; init; }

    public string? Email { get; init; }

    public string? Website { get; init; }

    public string? ContactPreference { get; init; }

    /// <summary>The logo, by reference. Uploaded through Media beforehand.</summary>
    public Guid? LogoMediaId { get; init; }

    /// <summary>The storefront banner, by reference.</summary>
    public Guid? BannerMediaId { get; init; }

    public int? BaseRevision { get; init; }
}
