namespace Diaspora.Modules.Jobs.Contracts.V1.Requests;

/// <summary>The whole-document save for an employer profile.</summary>
public sealed record UpdateEmployerRequest
{
    public required string DisplayName { get; init; }

    public string? LegalName { get; init; }

    public string? RegistrationNumber { get; init; }

    public required string OrganizationType { get; init; }

    public string? CompanySize { get; init; }

    public int? FoundedYear { get; init; }

    public required string CountryCode { get; init; }

    public string? Description { get; init; }

    // ── Contact. All optional: the application flow runs entirely on the platform. ───────────

    public string? ContactEmail { get; init; }

    public string? ContactPhone { get; init; }

    /// <summary>Absolute http(s) only — a relative or javascript: value is a redirect vector.</summary>
    public string? WebsiteUrl { get; init; }

    public string? CareersUrl { get; init; }

    // ── Branding. Media references, never URLs. ─────────────────────────────────────────────

    public Guid? LogoMediaId { get; init; }

    public Guid? CoverMediaId { get; init; }

    public string? Tagline { get; init; }

    public int? BaseRevision { get; init; }
}
