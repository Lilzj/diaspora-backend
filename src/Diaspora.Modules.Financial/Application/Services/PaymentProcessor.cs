using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Ledgers;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Domain.Wallets;
using Diaspora.Modules.Financial.Extensibility.Gateways;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Services;

/// <summary>
/// Drives a payment intent through a gateway and writes what happened into the ledger.
/// <para>
/// <b>The gateway decides whether money moved; this decides what that means.</b> An adapter answers
/// with an outcome and a reference; the aggregate's state machine decides whether the transition is
/// legal, and the ledger records it. Keeping the two apart is why a provider cannot drive an intent
/// into a state the platform forbids, and why adding a provider changes nothing here.
/// </para>
/// <para>
/// <b>Capture posts the money and authorisation does not.</b> An authorisation reserves funds with
/// an issuer and can still fail; posting on authorisation would credit the platform for money it
/// might never receive. Everything downstream — a placement activating, a subscription starting, a
/// donation being receipted — hangs off capture for the same reason.
/// </para>
/// <para>
/// <b>An adapter that cannot authorise separately is handled rather than refused.</b> Several real
/// methods have no two-step flow at all: a bank transfer and a mobile-money push either happened or
/// did not. <c>SupportsSeparateAuthorization</c> is what lets those take the single-step route.
/// </para>
/// </summary>
public sealed class PaymentProcessor
{
    private readonly IPaymentGatewayRegistry _gateways;
    private readonly ILedgerPostingService _ledger;
    private readonly FinancialIdentityService _identities;
    private readonly IPaymentIntentRepository _intents;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public PaymentProcessor(
        IPaymentGatewayRegistry gateways,
        ILedgerPostingService ledger,
        FinancialIdentityService identities,
        IPaymentIntentRepository intents,
        IGuidGenerator guids,
        IClock clock)
    {
        _gateways = gateways;
        _ledger = ledger;
        _identities = identities;
        _intents = intents;
        _guids = guids;
        _clock = clock;
    }

    /// <summary>
    /// Hands an intent to a gateway and records what came back.
    /// <para>
    /// Returns success for a challenge as well as for an authorisation: a 3-D Secure prompt is not a
    /// failure, and the client needs the payload rather than an error.
    /// </para>
    /// </summary>
    public async Task<Result<PaymentIntent>> AuthorizeAsync(
        PaymentIntent intent,
        string? clientContext = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(intent);

        // **A deposit cannot be funded from the balance it credits.** Both legs would be the same
        // wallet: the member's balance debited and credited by the same amount, a transaction that
        // balances, posts cleanly, moves nothing and reads on a statement as though something
        // happened. Refused here rather than at one of the call sites, because every path that opens
        // a deposit goes through this method and only this one.
        if (intent.MethodKind == PaymentMethodKind.Wallet && intent.Purpose == PaymentPurpose.Deposit)
        {
            return Result.Failure<PaymentIntent>(Error.Validation(
                FinancialErrorCodes.PaymentMethodNotSupported,
                "Money cannot be added to a wallet from that same wallet. "
                + "Top up with a card, a bank transfer or mobile money."));
        }

        var gateway = _gateways.Resolve(intent.MethodKind, intent.Currency, intent.CountryCode);

        if (gateway is null)
        {
            return Result.Failure<PaymentIntent>(Error.ServiceUnavailable(
                FinancialErrorCodes.PaymentGatewayUnknown,
                $"Nothing here can take a {intent.MethodKind} payment in {intent.Currency}."));
        }

        var request = Request(intent, clientContext);
        var now = _clock.UtcNow;

        var result = gateway.SupportsSeparateAuthorization
            ? await gateway.AuthorizeAsync(request, cancellationToken)
            : await gateway.CaptureAsync(request, cancellationToken);

        switch (result.Outcome)
        {
            case GatewayOutcome.RequiresAction:
                intent.RequireAction(
                    _guids.NewSequentialGuid(),
                    result.NextActionPayload ?? string.Empty,
                    now,
                    gateway.Provider);
                break;

            case GatewayOutcome.Succeeded when gateway.SupportsSeparateAuthorization:
                intent.Authorize(
                    _guids.NewSequentialGuid(),
                    GatewayReference.Create(
                        gateway.Provider,
                        result.ExternalReference ?? intent.Id.Value.ToString(),
                        result.ExternalStatus),
                    now);
                break;

            case GatewayOutcome.Succeeded:
                // A one-step provider. Authorised and captured are the same moment, so the intent
                // goes straight to captured and the ledger is posted here rather than on a second
                // call somebody would have to remember to make.
                intent.Authorize(
                    _guids.NewSequentialGuid(),
                    GatewayReference.Create(
                        gateway.Provider,
                        result.ExternalReference ?? intent.Id.Value.ToString(),
                        result.ExternalStatus),
                    now);

                var captured = await CaptureAsync(intent, null, cancellationToken);

                return captured;

            default:
                intent.Fail(
                    _guids.NewSequentialGuid(),
                    result.FailureCode,
                    result.FailureMessage,
                    result.Outcome is GatewayOutcome.Retryable or GatewayOutcome.Unavailable,
                    now,
                    gateway.Provider);
                break;
        }

        _intents.Update(intent);

        return Result.Success(intent);
    }

    /// <summary>
    /// Takes the money and posts it.
    /// <para>
    /// <b>The ledger legs are the platform's clearing wallet debited and its revenue wallet
    /// credited.</b> Money arriving from outside has to have a counterparty inside or the ledger does
    /// not balance, and the clearing wallet is the only account on the platform allowed to run
    /// negative for exactly that reason.
    /// </para>
    /// </summary>
    public async Task<Result<PaymentIntent>> CaptureAsync(
        PaymentIntent intent, Money? amount = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(intent);

        var gateway = _gateways.Find(intent.GatewayProvider ?? string.Empty)
            ?? _gateways.Resolve(intent.MethodKind, intent.Currency, intent.CountryCode);

        if (gateway is null)
        {
            return Result.Failure<PaymentIntent>(Error.ServiceUnavailable(
                FinancialErrorCodes.PaymentGatewayUnknown,
                "The adapter that took this payment is no longer registered."));
        }

        var toCapture = amount ?? intent.Amount;
        var now = _clock.UtcNow;

        // **Asked before the money is taken, not after.**
        //
        // The capture used to call the gateway and then post, and a posting that could not be made
        // left the money captured with nothing crediting it. That is not hypothetical: every member
        // wallet on the deployment was provisioned and never activated, so a card deposit would have
        // charged the member and then failed on the credit leg. The gateway had the money and
        // nothing refunded it.
        //
        // Refusing here leaves the authorisation uncaptured, which expires on its own. A member who
        // cannot be credited is told so instead of being charged.
        var postable = await CanPostAsync(intent, toCapture, cancellationToken);

        if (postable.IsFailure)
        {
            return Result.Failure<PaymentIntent>(postable.Error);
        }

        // A one-step provider has already moved the money during authorisation; calling capture
        // again would take it twice.
        if (gateway.SupportsSeparateAuthorization)
        {
            var result = await gateway.CaptureAsync(Request(intent, null), cancellationToken);

            if (!result.Succeeded)
            {
                intent.Fail(
                    _guids.NewSequentialGuid(),
                    result.FailureCode,
                    result.FailureMessage,
                    result.Outcome is GatewayOutcome.Retryable or GatewayOutcome.Unavailable,
                    now,
                    gateway.Provider);

                _intents.Update(intent);

                return Result.Failure<PaymentIntent>(Error.Conflict(
                    FinancialErrorCodes.PaymentCaptureFailed,
                    result.FailureMessage ?? "The payment could not be taken."));
            }
        }

        intent.Capture(toCapture, now);

        var posted = await PostCaptureAsync(intent, toCapture, cancellationToken);

        if (posted.IsFailure)
        {
            return Result.Failure<PaymentIntent>(posted.Error);
        }

        _intents.Update(intent);

        return Result.Success(intent);
    }

    /// <summary>
    /// Applies a capture the provider has already confirmed, without asking it again.
    /// <para>
    /// <b>The webhook path, and the reason it is a separate method rather than a flag.</b> A callback
    /// says the money moved; calling <c>CaptureAsync</c> would call the gateway a second time to ask
    /// whether the money it has just told us about moved, which is at best a wasted round trip and at
    /// worst a second capture against a provider whose capture is not idempotent.
    /// </para>
    /// <para>
    /// The aggregate still decides. A callback for an intent that is already captured, already
    /// failed or never authorised is refused by the state machine exactly as a client call would be —
    /// which is what stops a provider driving an intent somewhere the platform forbids.
    /// </para>
    /// </summary>
    public async Task<Result<PaymentIntent>> ConfirmCaptureAsync(
        PaymentIntent intent,
        Money amount,
        GatewayReference? gateway = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(amount);

        intent.Capture(amount, _clock.UtcNow, gateway);

        var posted = await PostCaptureAsync(intent, amount, cancellationToken);

        if (posted.IsFailure)
        {
            return Result.Failure<PaymentIntent>(posted.Error);
        }

        _intents.Update(intent);

        return Result.Success(intent);
    }

    /// <summary>
    /// Applies a refund the provider has already made — a chargeback, a reversal, a refund raised in
    /// the provider's own dashboard — without asking it to refund again.
    /// </summary>
    public async Task<Result<PaymentIntent>> ConfirmRefundAsync(
        PaymentIntent intent,
        Money amount,
        RefundReason reason,
        string? externalReference = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(amount);

        intent.Refund(
            _guids.NewSequentialGuid(),
            amount,
            reason,
            _clock.UtcNow,
            issuedByUserId: null,
            note: "Confirmed by the provider.",
            externalReference: externalReference);

        var posted = await PostRefundAsync(intent, amount, reason, cancellationToken);

        if (posted.IsFailure)
        {
            return Result.Failure<PaymentIntent>(posted.Error);
        }

        _intents.Update(intent);

        return Result.Success(intent);
    }

    /// <summary>Gives money back and posts the reverse legs.</summary>
    public async Task<Result<PaymentIntent>> RefundAsync(
        PaymentIntent intent,
        Money amount,
        RefundReason reason,
        Guid? issuedByUserId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(amount);

        var gateway = _gateways.Find(intent.GatewayProvider ?? string.Empty);

        if (gateway is not null)
        {
            var result = await gateway.RefundAsync(Request(intent, null), cancellationToken);

            if (!result.Succeeded)
            {
                return Result.Failure<PaymentIntent>(Error.Conflict(
                    FinancialErrorCodes.RefundNotAllowed,
                    result.FailureMessage ?? "The provider refused the refund."));
            }
        }

        var record = intent.Refund(
            _guids.NewSequentialGuid(), amount, reason, _clock.UtcNow, issuedByUserId, note);

        var posted = await PostRefundAsync(intent, amount, reason, cancellationToken);

        if (posted.IsFailure)
        {
            return Result.Failure<PaymentIntent>(posted.Error);
        }

        _intents.Update(intent);

        _ = record;

        return Result.Success(intent);
    }

    /// <summary>
    /// Whether the capture could be posted, checked before the gateway is asked to take anything.
    /// <para>
    /// <b>Deliberately the domain ’s own questions rather than a copy of the ledger’s checks.</b>
    /// `CanCredit` and `CanDebit` are what the wallets answer; re-implementing the posting service’s
    /// private rules here would give two places that decide whether money may move, and they would
    /// drift. This is a pre-flight, not a second gate: the ledger still refuses on its own terms.
    /// </para>
    /// </summary>
    private async Task<Result> CanPostAsync(
        PaymentIntent intent, Money amount, CancellationToken cancellationToken)
    {
        var counterparty = await CounterpartyAsync(intent, amount.Currency, cancellationToken);

        if (counterparty.IsFailure)
        {
            return Result.Failure(counterparty.Error);
        }

        var funding = await FundingWalletAsync(intent, amount.Currency, cancellationToken);

        if (funding.IsFailure)
        {
            return Result.Failure(funding.Error);
        }

        if (!counterparty.Value.CanCredit(amount))
        {
            return Result.Failure(Error.Conflict(
                FinancialErrorCodes.WalletClosed,
                "That balance cannot receive money right now, so the payment was not taken."));
        }

        if (!funding.Value.CanDebit(amount))
        {
            return Result.Failure(Error.Conflict(
                FinancialErrorCodes.InsufficientFunds,
                "The funding balance cannot cover this, so the payment was not taken."));
        }

        return Result.Success();
    }

    private async Task<Result> PostCaptureAsync(
        PaymentIntent intent, Money amount, CancellationToken cancellationToken)
    {
        var counterparty = await CounterpartyAsync(intent, amount.Currency, cancellationToken);

        if (counterparty.IsFailure)
        {
            return Result.Failure(counterparty.Error);
        }

        var funding = await FundingWalletAsync(intent, amount.Currency, cancellationToken);

        if (funding.IsFailure)
        {
            return Result.Failure(funding.Error);
        }

        var legs = BuildLegs(
            amount, funding.Value, counterparty.Value, EntryTypeFor(intent.Purpose), inbound: true);

        if (legs.IsFailure)
        {
            return Result.Failure(legs.Error);
        }

        var posting = await _ledger.PostAsync(
            new LedgerPostingRequest
            {
                TransactionType = TransactionTypeFor(intent.Purpose),
                Amount = amount,
                Description = intent.Description,
                Audit = AuditStamp.Create(
                    FinancialActor.Member(intent.PayerUserId),
                    FinancialSource.Api,
                    $"Payment captured: {intent.Description}"),
                Legs = legs.Value,
                Subject = intent.Subject,
                InitiatedByUserId = intent.PayerUserId,
                Reference = $"capture:{intent.Id.Value:N}",
                CountryCode = intent.CountryCode
            },
            cancellationToken);

        return posting.IsSuccess ? Result.Success() : Result.Failure(posting.Error);
    }

    private async Task<Result> PostRefundAsync(
        PaymentIntent intent, Money amount, RefundReason reason, CancellationToken cancellationToken)
    {
        // The refund reverses whatever the capture did, so it debits the same counterparty. A refund
        // of a deposit takes the money back out of the member's own wallet — and is refused when they
        // have already spent it, which is the correct answer rather than a negative balance.
        var counterparty = await CounterpartyAsync(intent, amount.Currency, cancellationToken);

        if (counterparty.IsFailure)
        {
            return Result.Failure(counterparty.Error);
        }

        var funding = await FundingWalletAsync(intent, amount.Currency, cancellationToken);

        if (funding.IsFailure)
        {
            return Result.Failure(funding.Error);
        }

        // Outbound: the counterparty is debited and the funding side credited. For a wallet payment
        // that puts the money back where it came from, which is the member's own balance.
        var legs = BuildLegs(
            amount, funding.Value, counterparty.Value, LedgerEntryType.Refund, inbound: false);

        if (legs.IsFailure)
        {
            return Result.Failure(legs.Error);
        }

        var posting = await _ledger.PostAsync(
            new LedgerPostingRequest
            {
                TransactionType = TransactionType.Refund,
                Amount = amount,
                Description = $"Refund: {intent.Description}",
                Audit = AuditStamp.Create(
                    FinancialActor.System(nameof(PaymentProcessor)),
                    FinancialSource.Api,
                    $"Refund ({reason}) against {intent.Description}"),
                Legs = legs.Value,
                Subject = intent.Subject,
                InitiatedByUserId = intent.PayerUserId,
                Reference = $"refund:{_guids.NewSequentialGuid():N}",
                CountryCode = intent.CountryCode
            },
            cancellationToken);

        return posting.IsSuccess ? Result.Success() : Result.Failure(posting.Error);
    }

    /// <summary>
    /// Which wallet the money lands in — and it is never one a request body named.
    /// <para>
    /// A purchase credits the platform's revenue wallet. A deposit credits the primary wallet on the
    /// <em>intent's own account</em>, which was resolved from the payer when the intent was created.
    /// There is no path from a client field to a wallet identifier, which is why deposit is an enum
    /// member rather than a destination.
    /// </para>
    /// </summary>
    private async Task<Result<Wallet>> CounterpartyAsync(
        PaymentIntent intent, string currency, CancellationToken cancellationToken)
    {
        if (intent.Purpose == PaymentPurpose.Deposit)
        {
            var payerWallet = await _identities.PrimaryWalletAsync(
                intent.AccountId, currency, cancellationToken);

            return payerWallet is null
                ? Result.Failure<Wallet>(Error.Unexpected(
                    FinancialErrorCodes.WalletNotFound,
                    "The payer has no primary wallet in this currency to deposit into."))
                : Result.Success(payerWallet);
        }

        var revenue = await _identities.PlatformWalletAsync(
            WalletType.Revenue, currency, cancellationToken);

        return revenue is null
            ? Result.Failure<Wallet>(Error.Unexpected(
                FinancialErrorCodes.WalletNotFound,
                "The platform has no revenue wallet in this currency. "
                + "The reference-data seeder opens them; check that it ran."))
            : Result.Success(revenue);
    }

    private static TransactionType TransactionTypeFor(PaymentPurpose purpose) =>
        purpose == PaymentPurpose.Deposit ? TransactionType.Deposit : TransactionType.Payment;

    private static LedgerEntryType EntryTypeFor(PaymentPurpose purpose) =>
        purpose == PaymentPurpose.Deposit ? LedgerEntryType.Deposit : LedgerEntryType.Payment;

    /// <summary>
    /// The two legs every external movement has.
    /// <para>
    /// Inbound: clearing debited, the counterparty credited. Outbound: the reverse. Both balance,
    /// which is the invariant the posting service refuses to violate — and the clearing wallet is the
    /// only account on the platform allowed to run negative, which is what lets money arriving from
    /// outside have an inside counterparty at all.
    /// </para>
    /// </summary>
    /// <summary>
    /// Where the money comes from, which is not always outside the platform.
    /// <para>
    /// <b>Clearing is the account money crosses when it enters or leaves.</b> It is the only wallet
    /// allowed to run negative, precisely because it stands in for the outside world while a
    /// provider settles.
    /// </para>
    /// <para>
    /// <b>A wallet payment never crosses it.</b> The money is already inside: it moves from the
    /// member's own balance to whoever is being paid, and routing it through clearing would post a
    /// debit against the outside world for money that never came from there — leaving clearing
    /// permanently short by every wallet payment ever made, and the member's balance untouched.
    /// </para>
    /// </summary>
    private async Task<Result<Wallet>> FundingWalletAsync(
        PaymentIntent intent, string currency, CancellationToken cancellationToken)
    {
        if (intent.MethodKind != PaymentMethodKind.Wallet)
        {
            var clearing = await _identities.PlatformWalletAsync(
                WalletType.Clearing, currency, cancellationToken);

            return clearing is null
                ? Result.Failure<Wallet>(Error.Unexpected(
                    FinancialErrorCodes.WalletNotFound,
                    "The platform has no clearing wallet in this currency. "
                    + "The reference-data seeder opens them; check that it ran."))
                : Result.Success(clearing);
        }

        var payer = await _identities.PrimaryWalletAsync(intent.AccountId, currency, cancellationToken);

        return payer is null
            ? Result.Failure<Wallet>(Error.Conflict(
                FinancialErrorCodes.WalletNotFound,
                $"You do not have a {currency} balance to pay from."))
            : Result.Success(payer);
    }

    private static Result<IReadOnlyList<LedgerPostingLeg>> BuildLegs(
        Money amount,
        Wallet funding,
        Wallet counterparty,
        LedgerEntryType entryType,
        bool inbound)
    {
        var clearing = funding;

        IReadOnlyList<LedgerPostingLeg> legs = inbound
            ?
            [
                new LedgerPostingLeg
                {
                    WalletId = clearing.Id,
                    Direction = LedgerDirection.Debit,
                    Amount = amount,
                    EntryType = entryType
                },
                new LedgerPostingLeg
                {
                    WalletId = counterparty.Id,
                    Direction = LedgerDirection.Credit,
                    Amount = amount,
                    EntryType = entryType
                }
            ]
            :
            [
                new LedgerPostingLeg
                {
                    WalletId = counterparty.Id,
                    Direction = LedgerDirection.Debit,
                    Amount = amount,
                    EntryType = entryType
                },
                new LedgerPostingLeg
                {
                    WalletId = clearing.Id,
                    Direction = LedgerDirection.Credit,
                    Amount = amount,
                    EntryType = entryType
                }
            ];

        return Result.Success(legs);
    }

    private static PaymentGatewayRequest Request(PaymentIntent intent, string? clientContext) =>
        new()
        {
            PaymentIntentId = intent.Id.Value,
            Amount = intent.Amount,
            MethodKind = intent.MethodKind,
            Description = intent.Description,
            ClientContext = clientContext,
            ExternalReference = intent.GatewayExternalReference,
            CountryCode = intent.CountryCode,
            PayerUserId = intent.PayerUserId,
            AccountId = intent.AccountId.Value,
            IdempotencyKey = intent.IdempotencyKey
        };
}
