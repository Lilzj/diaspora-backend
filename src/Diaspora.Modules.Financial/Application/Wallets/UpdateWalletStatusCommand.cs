using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Wallets;

/// <summary>
/// Freezes, unfreezes or closes a wallet.
/// <para>
/// <b>Audited, because freezing somebody's money is the most consequential thing an operator can do
/// on this platform and it produces no other record.</b> The wallet's own status reason is what the
/// member sees; the audit entry is what says who decided.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.WalletManage)]
[Auditable(Action = "financial.wallet.status")]
public sealed record UpdateWalletStatusCommand : ICommand<WalletResponse>
{
    public required Guid WalletId { get; init; }

    /// <summary><c>Active</c>, <c>Frozen</c> or <c>Closed</c>.</summary>
    public required string Status { get; init; }

    /// <summary>
    /// Why. Required, at least 10 characters, to freeze or close (7 Nov 2026); optional to unfreeze.
    /// Kept on the wallet and shown to the member in the notice and in the app.
    /// </summary>
    public string? Reason { get; init; }
}
