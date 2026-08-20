using Diaspora.Modules.Financial.Domain.Enums;

namespace Diaspora.Modules.Financial.Extensibility.Provisioning;

/// <summary>
/// Decides which wallets a new financial identity is opened with.
/// <para>
/// <b>A seam because the answer differs by deployment and by owner.</b> A personal account gets a
/// primary wallet and a promotional one; a provider account will one day want an escrow wallet the
/// day escrow exists. Hard-coding the set here would mean editing this module to change a
/// provisioning decision that has nothing to do with how money works.
/// </para>
/// </summary>
public interface IWalletProvisioningPolicy
{
    /// <summary>The wallets to open, in order. Never empty.</summary>
    IReadOnlyList<WalletType> WalletsFor(FinancialAccountType accountType, string currency);
}
