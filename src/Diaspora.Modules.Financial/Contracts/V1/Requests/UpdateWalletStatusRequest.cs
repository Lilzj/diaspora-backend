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

    public string? Reason { get; init; }
}
