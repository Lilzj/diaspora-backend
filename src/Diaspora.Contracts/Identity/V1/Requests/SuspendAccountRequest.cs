namespace Diaspora.Contracts.Identity.V1.Requests;

/// <summary>
/// Proposes a suspension, which a second member of staff confirms (7 Nov 2026). A suspended member
/// may still sign in, but only reaches the suspension screen: they need a route to appeal.
/// </summary>
public sealed record SuspendAccountRequest
{
    /// <summary>
    /// One of the platform's moderation reasons (<c>ModerationReasonCode</c>, by name), for example
    /// <c>FraudOrScam</c>. The member is told its label.
    /// </summary>
    public required string ReasonCode { get; init; }

    /// <summary>Why, in the proposer's words: at least 10 characters. The member reads it.</summary>
    public required string Reason { get; init; }

    /// <summary>When the suspension lifts. Null means indefinite, pending review.</summary>
    public DateTimeOffset? SuspendedUntil { get; init; }
}
