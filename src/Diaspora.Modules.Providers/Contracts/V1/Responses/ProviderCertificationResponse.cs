namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>
/// A credential a provider claims.
/// <para>
/// <see cref="IsVerified"/> and <see cref="Status"/> are both present and both matter: a
/// self-declared "Gas Safe registered" is useful information and is not proof, and a client that
/// rendered every certification with a tick would be publishing a safety claim nobody checked.
/// </para>
/// </summary>
public sealed record ProviderCertificationResponse
{
    public required Guid CertificationId { get; init; }

    public required string Name { get; init; }

    public required string Issuer { get; init; }

    public string? Reference { get; init; }

    public DateOnly? IssuedOn { get; init; }

    public DateOnly? ExpiresOn { get; init; }

    /// <summary>The <c>CertificationStatus</c> name — <c>Claimed</c>, <c>Verified</c>.</summary>
    public required string Status { get; init; }

    /// <summary>True only when Trust checked it and it has not lapsed.</summary>
    public required bool IsVerified { get; init; }

    /// <summary>A Media identifier for the scan. Never a URL.</summary>
    public Guid? EvidenceMediaId { get; init; }
}
