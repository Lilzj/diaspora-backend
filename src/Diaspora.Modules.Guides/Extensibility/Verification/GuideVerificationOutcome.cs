using Diaspora.Modules.Guides.Domain.Enums;

namespace Diaspora.Modules.Guides.Extensibility.Verification;

/// <summary>
/// What a verification provider decided, or did not.
/// <para>
/// <see cref="State"/> is <see cref="GuideVerificationState.Pending"/> when the claim was
/// handed to something that will answer later — which is what Trust's queue does, and what the
/// overwhelming majority of real claims will do. An immediate grant is the interesting case
/// and the one this seam exists to make possible.
/// </para>
/// </summary>
public sealed record GuideVerificationOutcome
{
    /// <summary>The decision, or <see cref="GuideVerificationState.Pending"/> if deferred.</summary>
    public required GuideVerificationState State { get; init; }

    /// <summary>
    /// The deciding system's own identifier for the case — Trust's verification id, or an
    /// external workflow's reference. Stored so the two systems can be reconciled, and so the
    /// outcome events can find the guide that raised the claim.
    /// </summary>
    public Guid? VerificationId { get; init; }

    /// <summary>SCREAMING_SNAKE, matching the deciding system's vocabulary where one applies.</summary>
    public string? ReasonCode { get; init; }

    public string? Reason { get; init; }

    /// <summary>Whether this provider had an opinion at all.</summary>
    public bool IsDecided { get; init; } = true;

    /// <summary>
    /// The provider worked and has nothing to say. Distinct from a failure: the registry moves
    /// on to the next provider either way, but only a failure is worth alerting on.
    /// </summary>
    public static GuideVerificationOutcome Undecided { get; } = new()
    {
        State = GuideVerificationState.None,
        IsDecided = false
    };

    /// <summary>Handed to a queue. Somebody will decide later.</summary>
    public static GuideVerificationOutcome Deferred(Guid verificationId) => new()
    {
        State = GuideVerificationState.Pending,
        VerificationId = verificationId
    };
}
