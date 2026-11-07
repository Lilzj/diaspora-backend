namespace Diaspora.Contracts.Identity.V1.Requests;

/// <summary>
/// Lifts a suspension or hard lock, returning the member to the access rung they held
/// beforehand.
/// </summary>
public sealed record ReinstateAccountRequest
{
    /// <summary>Why it is lifted. Required since 7 Nov 2026; the member is told it.</summary>
    public string? Notes { get; init; }
}
