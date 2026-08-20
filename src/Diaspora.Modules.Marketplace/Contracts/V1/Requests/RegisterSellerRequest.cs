namespace Diaspora.Modules.Marketplace.Contracts.V1.Requests;

/// <summary>Opens a seller profile.</summary>
public sealed record RegisterSellerRequest
{
    /// <summary>The trading name buyers see.</summary>
    public required string DisplayName { get; init; }

    /// <summary><c>Individual</c> or <c>Business</c>. Individual when omitted.</summary>
    public string? Kind { get; init; }

    /// <summary>ISO 3166-1 alpha-2. Where the seller mostly trades.</summary>
    public required string CountryCode { get; init; }

    /// <summary>The registered name, for Trust. Never rendered to a buyer.</summary>
    public string? LegalName { get; init; }

    /// <summary>A company number, for Trust. Never parsed here, never rendered.</summary>
    public string? RegistrationNumber { get; init; }

    /// <summary>The seller's own words about themselves. Plain text.</summary>
    public string? Bio { get; init; }

    public string? Phone { get; init; }

    public string? Email { get; init; }

    public string? Website { get; init; }

    /// <summary><c>InApp</c>, <c>Phone</c>, <c>Email</c> or <c>Any</c>. In-app when omitted.</summary>
    public string? ContactPreference { get; init; }

    /// <summary>A preferred storefront handle. Derived from the name when omitted or taken.</summary>
    public string? Slug { get; init; }

    /// <summary>
    /// A shop in the Providers directory to sell from. The caller must own it; the handler asks
    /// B7, because only B7 knows.
    /// </summary>
    public Guid? ProviderId { get; init; }
}
