namespace Diaspora.Contracts.Identity.V1.Requests;

/// <summary>
/// Proposes reopening a closed account, which a second member of staff confirms (7 Nov 2026). How a
/// closed member's appeal is honoured: the member returns at limited access.
/// </summary>
public sealed record ReactivateAccountRequest
{
    /// <summary>
    /// One of the platform's moderation reasons (<c>ModerationReasonCode</c>, by name): the reason the
    /// account was closed that the reopening answers.
    /// </summary>
    public required string ReasonCode { get; init; }

    /// <summary>Why it is reopened: at least 10 characters. The member reads it.</summary>
    public required string Note { get; init; }
}
