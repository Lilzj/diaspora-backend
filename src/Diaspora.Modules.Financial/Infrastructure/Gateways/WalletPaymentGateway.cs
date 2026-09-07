using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Extensibility.Gateways;

namespace Diaspora.Modules.Financial.Infrastructure.Gateways;

/// <summary>
/// Paying with money the platform already holds for you.
/// <para>
/// <b>The one adapter whose funds are inside the platform.</b> Every other implementation of
/// <see cref="IPaymentGateway"/> talks to somebody outside — a card network, a bank, a mobile-money
/// operator — and answers for money that has to travel. This one answers for a balance that is
/// already here, so there is no network call, no hosted page, and nothing to wait for.
/// </para>
/// <para>
/// <b>It moves no money.</b> That is the whole point of it being an adapter rather than a special
/// case: the ledger posting in <c>PaymentProcessor.PostCaptureAsync</c> is the single place a wallet
/// payment debits anybody, exactly as it is for a card capture. This says only whether the payment
/// can proceed, and the ledger's own overdraw refusal remains the guarantee — the check below is a
/// better error, sooner, not a second source of truth.
/// </para>
/// <para>
/// <b>One step, deliberately.</b> There is no provider to hold funds on the platform's behalf, so
/// authorising and taking are the same moment; <see cref="SupportsSeparateAuthorization"/> is false
/// and <c>AuthorizeAsync</c> on the processor routes straight through capture. A two-step version
/// would reserve into <c>LedgerBucket.Reserved</c> and release on cancel, which is worth building
/// the day something needs to hold a member's balance while a decision is pending. Nothing does.
/// </para>
/// <para>
/// <b>Named after nobody, like its neighbour.</b> The architecture test that forbids a payment
/// vendor's name in this assembly is satisfied because the platform is not a vendor.
/// </para>
/// </summary>
public sealed class WalletPaymentGateway : IPaymentGateway
{
    /// <summary>Stored on the intent, never interpreted.</summary>
    public const string ProviderName = "wallet";

    private readonly FinancialIdentityService _identities;

    public WalletPaymentGateway(FinancialIdentityService identities) => _identities = identities;

    public string Provider => ProviderName;

    public IReadOnlySet<PaymentMethodKind> SupportedMethods { get; } =
        new HashSet<PaymentMethodKind> { PaymentMethodKind.Wallet };

    /// <summary>
    /// Empty: a member holds a wallet per currency, so the currency question is answered by whether
    /// they have that wallet with enough in it, not by a list this adapter keeps.
    /// </summary>
    public IReadOnlySet<string> SupportedCurrencies { get; } = new HashSet<string>(StringComparer.Ordinal);

    public bool SupportsSeparateAuthorization => false;

    public Task<PaymentGatewayResult> AuthorizeAsync(
        PaymentGatewayRequest request, CancellationToken cancellationToken = default) =>
        CaptureAsync(request, cancellationToken);

    public async Task<PaymentGatewayResult> CaptureAsync(
        PaymentGatewayRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.AccountId is not { } accountId)
        {
            return PaymentGatewayResult.Declined(
                "wallet_account_unknown",
                "This payment is not attached to an account, so there is no balance to take it from.");
        }

        var wallet = await _identities.PrimaryWalletAsync(
            FinancialAccountId.From(accountId), request.Amount.Currency, cancellationToken);

        if (wallet is null)
        {
            return PaymentGatewayResult.Declined(
                "wallet_missing",
                $"You do not have a {request.Amount.Currency} balance to pay from.");
        }

        // **Available, not the total.** Money already reserved against something else is not
        // spendable here, and reading the headline balance would let one commitment be spent twice.
        if (!wallet.CanDebit(request.Amount))
        {
            return PaymentGatewayResult.Declined(
                "wallet_insufficient_balance",
                "Your balance does not cover this. Add money to your wallet, or pay another way.");
        }

        // The reference is the intent's own id. There is no provider issuing one, and inventing a
        // synthetic handle would put a value in `GatewayExternalReference` that nothing can resolve —
        // which is exactly the defect that made a Stripe capture quote this platform's guid back at
        // Stripe. Here the platform genuinely is both sides, so its own id is the honest answer.
        return PaymentGatewayResult.Success(
            request.PaymentIntentId.ToString(), externalStatus: "wallet");
    }

    /// <summary>Nothing external is holding anything, so there is nothing to call off.</summary>
    public Task<PaymentGatewayResult> CancelAsync(
        PaymentGatewayRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.FromResult(
            PaymentGatewayResult.Success(request.PaymentIntentId.ToString(), "cancelled"));
    }

    /// <summary>
    /// The refund is the ledger entry that puts it back, and the processor posts that.
    /// <para>
    /// A wallet refund cannot fail for a provider's reasons — there is no provider — so this
    /// succeeds and lets the posting be the thing that can refuse.
    /// </para>
    /// </summary>
    public Task<PaymentGatewayResult> RefundAsync(
        PaymentGatewayRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.FromResult(
            PaymentGatewayResult.Success(request.PaymentIntentId.ToString(), "refunded"));
    }
}
