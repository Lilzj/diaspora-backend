namespace Diaspora.Contracts.Identity.V1.Requests;

/// <summary>
/// Lifts a suspension or hard lock, returning the member to the access rung they held
/// beforehand.
/// </summary>
public sealed record ReinstateAccountRequest
{
    public string? Notes { get; init; }
}
