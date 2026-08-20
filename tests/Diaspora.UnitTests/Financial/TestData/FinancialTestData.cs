using Diaspora.Modules.Financial.Domain.Accounts;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Domain.Wallets;

namespace Diaspora.UnitTests.Financial.TestData;

/// <summary>
/// The fixtures the financial suite builds from.
/// <para>
/// Every helper returns a fresh instance. A shared aggregate between two tests is a test that passes
/// alone and fails in a run, and a shared owned value object is the trap the platform's own
/// <c>ValueObjectSharingArchitectureTests</c> exists to catch.
/// </para>
/// </summary>
internal static class FinancialTestData
{
    public const string Gbp = "GBP";

    public static readonly DateTimeOffset Now = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    public static Money Gbp_(decimal amount) => Money.FromAmount(amount, Gbp);

    public static AuditStamp Stamp(string reason = "A test.") =>
        AuditStamp.Create(FinancialActor.System("tests"), FinancialSource.System, reason);

    public static FinancialAccount Account(
        FinancialAccountType accountType = FinancialAccountType.Personal, bool active = true)
    {
        var account = FinancialAccount.Open(
            FinancialAccountId.New(), Guid.NewGuid(), accountType, Gbp, "Test", Now);

        if (active)
        {
            account.Activate(Now);
        }

        return account;
    }

    public static Wallet Wallet_(
        WalletType walletType = WalletType.Primary,
        bool active = true,
        FinancialAccountType ownerType = FinancialAccountType.Personal)
    {
        var wallet = Wallet.Open(
            WalletId.New(),
            FinancialAccountId.New(),
            Guid.NewGuid(),
            ownerType,
            walletType,
            Gbp,
            Now);

        if (active)
        {
            wallet.Activate(Now);
        }

        return wallet;
    }

    /// <summary>The platform's clearing wallet — the one account allowed to run negative.</summary>
    public static Wallet ClearingWallet() =>
        Wallet_(WalletType.Clearing, active: true, ownerType: FinancialAccountType.Platform);
}
