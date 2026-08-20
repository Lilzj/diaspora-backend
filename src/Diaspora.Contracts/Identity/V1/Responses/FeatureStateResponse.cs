namespace Diaspora.Contracts.Identity.V1.Responses;

/// <summary>
/// One feature gate and whether it is open for this member.
/// </summary>
public sealed record FeatureStateResponse
{
    public required string Name { get; init; }

    public required bool IsEnabled { get; init; }

    /// <summary>Why the gate is closed, and what would open it. Null when enabled.</summary>
    public string? BlockedReason { get; init; }

    /// <summary>The access state at which this feature becomes available.</summary>
    public string? UnlockedAt { get; init; }
}
