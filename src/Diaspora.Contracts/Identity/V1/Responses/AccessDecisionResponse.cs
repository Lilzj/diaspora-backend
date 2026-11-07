namespace Diaspora.Contracts.Identity.V1.Responses;

/// <summary>
/// Where the Access Gateway says this member should be sent, and what they may do once
/// they arrive. Computed in one place so no controller ever re-derives it.
/// </summary>
public sealed record AccessDecisionResponse
{
    public required string AccessState { get; init; }

    public required string AccountStatus { get; init; }

    /// <summary>The destination surface, for example ExploreLimited or VerificationDashboard.</summary>
    public required string Destination { get; init; }

    /// <summary>Short explanation suitable for display to the member.</summary>
    public required string Message { get; init; }

    /// <summary>What the member must do to progress, in the order they should do it.</summary>
    public required IReadOnlyList<string> RequiredActions { get; init; }

    public required IReadOnlyList<string> GrantedFeatures { get; init; }

    public required IReadOnlyList<string> BlockedFeatures { get; init; }

    public required IReadOnlyList<string> Permissions { get; init; }

    /// <summary>Set when the member is suspended for a fixed period.</summary>
    public DateTimeOffset? RestrictedUntil { get; init; }

    /// <summary>
    /// Why staff suspended or closed the account: a moderation reason code's name (7 Nov 2026).
    /// Null for any other state, and for a restriction placed before reasons were coded.
    /// </summary>
    public string? ReasonCode { get; init; }

    /// <summary>The reason as the member reads it, for example "a fraud or scam".</summary>
    public string? ReasonLabel { get; init; }

    /// <summary>What the member of staff wrote, for a suspended or closed account.</summary>
    public string? Note { get; init; }
}
