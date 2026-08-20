using Diaspora.Modules.Providers.Domain.Enums;

namespace Diaspora.Modules.Providers.Extensibility.Verification;

/// <summary>
/// Everything a verification provider is told about a claim.
/// <para>
/// Flat primitives rather than the <c>Provider</c> aggregate, and that is the whole point of the
/// seam: a verification provider written against a national companies register needs a company
/// number, a jurisdiction and a name, and handing it the aggregate would couple it to every
/// future change in B7's domain and give it the ability to mutate what it was asked to check.
/// </para>
/// </summary>
public sealed record ProviderVerificationRequest
{
    public required Guid ProviderId { get; init; }

    public required ProviderVerificationKind Kind { get; init; }

    /// <summary>The member submitting. Trust reviews people, not rows.</summary>
    public required Guid SubmittedBy { get; init; }

    public required ProviderType ProviderType { get; init; }

    /// <summary>The trading name.</summary>
    public required string ProviderName { get; init; }

    /// <summary>The registered name, when it differs.</summary>
    public string? LegalName { get; init; }

    /// <summary>Company, charity or association number.</summary>
    public string? RegistrationNumber { get; init; }

    public string? RegistrationJurisdiction { get; init; }

    public string? TaxIdentifier { get; init; }

    /// <summary>Where the provider is, so a register can be chosen by country.</summary>
    public string? CountryCode { get; init; }

    /// <summary>The certification under review, for a <c>Certification</c> claim.</summary>
    public Guid? CertificationId { get; init; }

    public string? CertificationName { get; init; }

    public string? CertificationIssuer { get; init; }

    public string? CertificationReference { get; init; }

    /// <summary>Media identifiers of the evidence. References, never URLs.</summary>
    public IReadOnlyList<Guid> EvidenceMediaIds { get; init; } = [];
}
