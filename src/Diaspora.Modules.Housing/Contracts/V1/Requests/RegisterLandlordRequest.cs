namespace Diaspora.Modules.Housing.Contracts.V1.Requests;

/// <summary>Sets up a landlord profile. One per member.</summary>
public sealed record RegisterLandlordRequest
{
    /// <summary>What every card and profile renders.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Individual, agency, hostel, employer. Drives which Trust claim is made.</summary>
    public required string Kind { get; init; }

    /// <summary>Where they mostly let. ISO 3166-1 alpha-2.</summary>
    public required string CountryCode { get; init; }

    /// <summary>The registered name, for a business. Submitted to Trust rather than the trading name.</summary>
    public string? LegalName { get; init; }

    /// <summary>A company or letting-agency registration number. Passed to Trust, never parsed here.</summary>
    public string? RegistrationNumber { get; init; }

    public string? PhoneNumber { get; init; }

    public string? Email { get; init; }

    public string? WebsiteUrl { get; init; }

    /// <summary>Whether contact details appear to browsers. Defaults to no.</summary>
    public bool ShowContactPublicly { get; init; }

    public string? Bio { get; init; }

    /// <summary>An existing B7 provider to link. Ownership is checked before it is accepted.</summary>
    public Guid? ProviderId { get; init; }

    /// <summary>A Media asset for the logo. A reference, never bytes.</summary>
    public Guid? LogoMediaId { get; init; }
}
