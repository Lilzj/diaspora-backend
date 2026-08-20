namespace Diaspora.Modules.Providers.Contracts.V1.Requests;

/// <summary>
/// Adds a credential a provider claims.
/// <para>
/// It lands as <c>Claimed</c>, not <c>Verified</c>. Only Trust can move it, through the
/// verification endpoint — a model that let a provider mark its own gas-safety registration as
/// verified is a category of harm worth designing against explicitly.
/// </para>
/// </summary>
public sealed record ProviderCertificationRequest
{
    public required string Name { get; init; }

    /// <summary>Required. An unattributed credential is not a credential.</summary>
    public required string Issuer { get; init; }

    public string? Reference { get; init; }

    public DateOnly? IssuedOn { get; init; }

    public DateOnly? ExpiresOn { get; init; }

    /// <summary>A Media identifier for the scan. Never a URL.</summary>
    public Guid? EvidenceMediaId { get; init; }
}
