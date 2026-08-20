using Diaspora.Modules.Providers.Domain.Enums;

namespace Diaspora.Modules.Providers.Extensibility.Verification;

/// <summary>
/// What a verification provider decided, or did not.
/// <para>
/// <see cref="State"/> is <see cref="ProviderVerificationState.Pending"/> when the claim was
/// handed to something that will answer later — which is what Trust's queue does, and what the
/// overwhelming majority of real claims will do. An immediate grant is the interesting case and
/// the one this seam exists to make possible.
/// </para>
/// </summary>
public sealed record ProviderVerificationOutcome
{
    /// <summary>The decision, or <see cref="ProviderVerificationState.Pending"/> if deferred.</summary>
    public required ProviderVerificationState State { get; init; }

    /// <summary>
    /// The deciding system's own identifier for the case — Trust's verification id, or a
    /// reference from an external register. Stored so the two systems can be reconciled.
    /// </summary>
    public Guid? ExternalVerificationId { get; init; }

    /// <summary>When a grant lapses. Null when it does not.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>SCREAMING_SNAKE, matching Trust's own vocabulary where one applies.</summary>
    public string? ReasonCode { get; init; }

    public string? Reason { get; init; }

    /// <summary>Whether this provider had an opinion at all.</summary>
    public bool IsDecided { get; init; } = true;

    /// <summary>
    /// The provider worked and has nothing to say. Distinct from a failure: the registry moves
    /// on to the next provider either way, but only a failure is worth alerting on.
    /// </summary>
    public static ProviderVerificationOutcome Undecided { get; } = new()
    {
        State = ProviderVerificationState.Unverified,
        IsDecided = false
    };

    /// <summary>Handed to a queue. Somebody will decide later.</summary>
    public static ProviderVerificationOutcome Deferred(Guid externalVerificationId) => new()
    {
        State = ProviderVerificationState.Pending,
        ExternalVerificationId = externalVerificationId
    };
}
