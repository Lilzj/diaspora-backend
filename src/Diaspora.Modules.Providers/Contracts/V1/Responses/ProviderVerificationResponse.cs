namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>
/// What has been proven about a provider.
/// <para>
/// Replaces the client's single <c>verificationStatus: 'Verified'</c> string, which cannot
/// express "identity verified, business registration pending" — the most common real state of a
/// new provider. <see cref="IsVerified"/> is kept as the rolled-up boolean so the existing card
/// and badge render unchanged.
/// </para>
/// </summary>
public sealed record ProviderVerificationResponse
{
    /// <summary>The rolled-up answer. What a badge renders from.</summary>
    public required bool IsVerified { get; init; }

    /// <summary>The <c>ProviderVerificationState</c> name of the most advanced claim.</summary>
    public required string Status { get; init; }

    /// <summary>The kinds currently granted.</summary>
    public required IReadOnlyList<string> VerifiedKinds { get; init; }

    public DateTimeOffset? VerifiedAt { get; init; }

    /// <summary>The soonest a current grant lapses. What a renewal reminder is scheduled from.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Every claim, for the owner's verification screen.</summary>
    public required IReadOnlyList<ProviderVerificationClaimResponse> Claims { get; init; }
}
