namespace Diaspora.Contracts.Identity.V1.Requests;

/// <summary>
/// Moderation action. A suspended member may still sign in, but only reaches the
/// suspension screen — they need a route to appeal.
/// </summary>
public sealed record SuspendAccountRequest
{
    public required string Reason { get; init; }

    /// <summary>When the suspension lifts. Null means indefinite, pending review.</summary>
    public DateTimeOffset? SuspendedUntil { get; init; }
}
