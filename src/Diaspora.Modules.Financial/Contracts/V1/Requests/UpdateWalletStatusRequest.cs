namespace Diaspora.Modules.Financial.Contracts.V1.Requests;

/// <summary>
/// Freeze, unfreeze or close a wallet. Staff only.
/// <para>
/// A frozen wallet still accepts money — refunds must reach somebody whose account is under review —
/// and refuses every debit. That asymmetry is the whole point of freezing rather than closing.
/// </para>
/// </summary>
public sealed record UpdateWalletStatusRequest
{
    /// <summary><c>Active</c>, <c>Frozen</c> or <c>Closed</c>.</summary>
    public required string Status { get; init; }

    /// <summary>
    /// Why. Required, at least 10 characters, to freeze or close (7 Nov 2026); optional to unfreeze.
    /// Kept on the wallet and shown to the member in the notice and in the app.
    /// </summary>
    public string? Reason { get; init; }
}
