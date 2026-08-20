namespace Diaspora.Contracts.Identity.V1.Requests;

/// <summary>
/// Closes an account. Sessions are revoked and every issued token stops working.
/// </summary>
public sealed record DisableAccountRequest
{
    public required string Reason { get; init; }
}
