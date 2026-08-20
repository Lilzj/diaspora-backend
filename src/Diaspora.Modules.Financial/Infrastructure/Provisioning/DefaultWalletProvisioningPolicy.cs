using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Extensibility.Provisioning;

namespace Diaspora.Modules.Financial.Infrastructure.Provisioning;

/// <summary>
/// What wallets a new financial identity is opened with.
/// <para>
/// <b>A primary wallet and a promotional one for everybody who is not the platform.</b> The
/// promotional wallet exists from the start rather than being created on the first credit grant,
/// because a grant is usually issued by a sweep or a consumer and a path that has to create a wallet
/// first is a path with two failure modes.
/// </para>
/// <para>
/// <b>The platform account gets four.</b> Clearing is the counterparty to everything arriving from
/// outside; revenue is where commission and fees land; escrow is the seam a future escrow engine
/// needs; promotional funds credit grants so the platform's own giving is visible as a balance
/// rather than as an unexplained hole in clearing.
/// </para>
/// </summary>
public sealed class DefaultWalletProvisioningPolicy : IWalletProvisioningPolicy
{
    public IReadOnlyList<WalletType> WalletsFor(FinancialAccountType accountType, string currency) =>
        accountType == FinancialAccountType.Platform
            ? [WalletType.Clearing, WalletType.Revenue, WalletType.Escrow, WalletType.Promotional]
            : [WalletType.Primary, WalletType.Promotional];
}
