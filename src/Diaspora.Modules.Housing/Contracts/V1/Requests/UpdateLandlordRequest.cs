namespace Diaspora.Modules.Housing.Contracts.V1.Requests;

/// <summary>
/// Revises a landlord profile.
/// <para>
/// Every field is optional and null means leave it alone, so a client editing one detail need not
/// round-trip the rest and cannot blank a field by omitting it.
/// </para>
/// </summary>
public sealed record UpdateLandlordRequest
{
    public string? DisplayName { get; init; }

    public string? LegalName { get; init; }

    public string? RegistrationNumber { get; init; }

    public string? CountryCode { get; init; }

    public string? PhoneNumber { get; init; }

    public string? Email { get; init; }

    public string? WebsiteUrl { get; init; }

    public bool? ShowContactPublicly { get; init; }

    public string? Bio { get; init; }

    public Guid? ProviderId { get; init; }

    public Guid? LogoMediaId { get; init; }

    /// <summary>The revision the client loaded. A stale one is refused with a conflict.</summary>
    public int? BaseRevision { get; init; }
}
