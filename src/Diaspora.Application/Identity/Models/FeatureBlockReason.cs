namespace Diaspora.Application.Identity.Models;

/// <summary>
/// A closed feature gate, with the access state that would open it. Telling a member
/// "verify your account to publish a guide" is far more useful than a bare 403.
/// </summary>
public sealed record FeatureBlockReason
{
    public required string Feature { get; init; }

    public required string Reason { get; init; }

    /// <summary>The access state at which the gate opens. Null when it never will.</summary>
    public string? UnlockedAt { get; init; }
}
