namespace Diaspora.Contracts.Identity.V1.Requests;

/// <summary>
/// Proposes closing an account, which a second member of staff confirms (7 Nov 2026). Once
/// confirmed, sessions are revoked and every issued token stops working.
/// </summary>
public sealed record DisableAccountRequest
{
    /// <summary>One of the platform's moderation reasons (<c>ModerationReasonCode</c>, by name).</summary>
    public required string ReasonCode { get; init; }

    /// <summary>Why, in the proposer's words: at least 10 characters. The member reads it.</summary>
    public required string Reason { get; init; }
}
