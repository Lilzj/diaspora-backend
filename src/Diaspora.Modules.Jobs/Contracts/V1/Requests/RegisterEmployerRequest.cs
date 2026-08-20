namespace Diaspora.Modules.Jobs.Contracts.V1.Requests;

/// <summary>
/// Registers an employer profile, with the caller as its first owner.
/// <para>
/// <see cref="LegalName"/> is separate from the display name deliberately: candidates recognise
/// the trading name and registries hold the legal one, and Trust needs the registered one when it
/// checks. Forcing a choice would mean employers pick the recognisable one and verification gets
/// harder.
/// </para>
/// </summary>
public sealed record RegisterEmployerRequest
{
    /// <summary>The trading name a card renders.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The registered entity name, when it differs. What Trust checks against.</summary>
    public string? LegalName { get; init; }

    /// <summary>Unvalidated free text — every jurisdiction formats company numbers differently.</summary>
    public string? RegistrationNumber { get; init; }

    /// <summary>
    /// <c>Company</c>, <c>RecruitmentAgency</c>, <c>NonProfit</c> and so on. Candidates are
    /// entitled to know whether they are dealing with the employer or an intermediary.
    /// </summary>
    public required string OrganizationType { get; init; }

    /// <summary><c>Micro</c> … <c>Enterprise</c>. Omitted means unspecified.</summary>
    public string? CompanySize { get; init; }

    public int? FoundedYear { get; init; }

    /// <summary>Where the organisation is based. Upper-case ISO 3166-1 alpha-2.</summary>
    public required string CountryCode { get; init; }

    /// <summary>A requested deep-link handle. One is derived from the name when omitted.</summary>
    public string? Slug { get; init; }

    public string? Description { get; init; }
}
