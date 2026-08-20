namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>One verification claim, as the owner's verification screen renders it.</summary>
public sealed record ProviderVerificationClaimResponse
{
    public required Guid VerificationId { get; init; }

    /// <summary>The <c>ProviderVerificationKind</c> name.</summary>
    public required string Kind { get; init; }

    /// <summary>The <c>ProviderVerificationState</c> name.</summary>
    public required string State { get; init; }

    public DateTimeOffset? SubmittedAt { get; init; }

    public DateTimeOffset? DecidedAt { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>SCREAMING_SNAKE, matching Trust's own vocabulary. Null unless refused.</summary>
    public string? ReasonCode { get; init; }

    public string? Reason { get; init; }

    /// <summary>The certification under review, for a <c>Certification</c> claim.</summary>
    public Guid? CertificationId { get; init; }
}
