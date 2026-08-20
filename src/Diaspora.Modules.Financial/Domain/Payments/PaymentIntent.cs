using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Events;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Payments;

/// <summary>
/// One attempt to move money in from outside the platform, through its whole lifecycle.
/// <para>
/// <b>The state machine is on the aggregate and nowhere else.</b> Every transition is a method that
/// refuses when it does not apply, so there is no path — through a handler, a webhook, an operator
/// tool or a future adapter — that can move an intent somewhere it may not go. A payment that
/// quietly went backwards is the defect nobody finds until a reconciliation two months later.
/// </para>
/// <para>
/// <b>Nothing is ever overwritten.</b> A failure appends an attempt; a refund appends a record; a
/// cancellation sets a terminal status and stops. The intent accumulates its own history, which is
/// what makes "why was this member charged twice" answerable from one row and its children.
/// </para>
/// <para>
/// <b>The intent knows no provider.</b> <see cref="Gateway"/> holds whatever the registered adapter
/// said its name was, as a string, and nothing in this module switches on it.
/// </para>
/// <para>
/// <b><see cref="IdempotencyKey"/> is uniquely indexed.</b> A double-tapped pay button, a retried
/// mobile request and a webhook redelivery all arrive as the same key, and the database refuses the
/// second — rather than a handler check that a future refactor can step around.
/// </para>
/// </summary>
public sealed class PaymentIntent : AggregateRoot<PaymentIntentId>, IAuditableEntity
{
    private readonly List<PaymentAttempt> _attempts = [];
    private readonly List<RefundRecord> _refunds = [];

    private PaymentIntent(
        PaymentIntentId id,
        Guid payerUserId,
        FinancialAccountId accountId,
        Money amount,
        PaymentMethodKind methodKind,
        string description,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt) : base(id)
    {
        PayerUserId = payerUserId;
        AccountId = accountId;
        AmountMinorUnits = amount.MinorUnits;
        Currency = amount.Currency;
        Exponent = amount.Exponent;
        MethodKind = methodKind;
        Description = description;
        Status = PaymentIntentStatus.Draft;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    private PaymentIntent() { }

    public Guid PayerUserId { get; private set; }

    public FinancialAccountId AccountId { get; private set; } = default!;

    public long AmountMinorUnits { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public int Exponent { get; private set; } = Money.DefaultExponent;

    public PaymentIntentStatus Status { get; private set; }

    public PaymentMethodKind MethodKind { get; private set; }

    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// Where captured money lands. Set once, at creation, and never afterwards — a purpose that
    /// could be changed after authorisation is a payment whose counterparty a second request could
    /// redirect.
    /// </summary>
    public PaymentPurpose Purpose { get; private set; } = PaymentPurpose.Purchase;

    /// <summary>What the payment is for. Never resolved here.</summary>
    public MonetizationSubjectType? SubjectType { get; private set; }

    public Guid? SubjectId { get; private set; }

    /// <summary>The subject as a value object, rebuilt on read. See <c>LedgerEntry.Subject</c>.</summary>
    public SubjectReference? Subject =>
        SubjectType is { } type && SubjectId is { } id
            ? SubjectReference.Create(type, id)
            : null;

    /// <summary>Whatever the registered adapter calls itself. Null until one has seen the intent.</summary>
    public string? GatewayProvider { get; private set; }

    /// <summary>The provider's own identifier. The reconciliation key.</summary>
    public string? GatewayExternalReference { get; private set; }

    /// <summary>The provider's own status string, kept unmapped for support.</summary>
    public string? GatewayExternalStatus { get; private set; }

    /// <summary>The gateway reference as a value object, rebuilt on read.</summary>
    public GatewayReference? Gateway =>
        GatewayProvider is { } provider && GatewayExternalReference is { } reference
            ? GatewayReference.Create(provider, reference, GatewayExternalStatus)
            : null;

    /// <summary>How much has actually been taken. Zero until capture.</summary>
    public long CapturedMinorUnits { get; private set; }

    /// <summary>How much has been returned. Zero until a refund.</summary>
    public long RefundedMinorUnits { get; private set; }

    /// <summary>Unique when present. See the class summary.</summary>
    public string? IdempotencyKey { get; private set; }

    public string? CountryCode { get; private set; }

    /// <summary>
    /// What the client must do next, when the issuer wants something — a redirect URL, a challenge
    /// token. Opaque here and passed straight through; interpreting it would be provider knowledge.
    /// </summary>
    public string? NextActionPayload { get; private set; }

    public string? FailureCode { get; private set; }

    public string? FailureMessage { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? AuthorizedAt { get; private set; }

    public DateTimeOffset? CapturedAt { get; private set; }

    public DateTimeOffset? SettledAt { get; private set; }

    public DateTimeOffset? TerminatedAt { get; private set; }

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public IReadOnlyList<PaymentAttempt> Attempts => _attempts.AsReadOnly();

    public IReadOnlyList<RefundRecord> Refunds => _refunds.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public Money Amount => Money.FromMinorUnits(AmountMinorUnits, Currency, Exponent);

    public Money Captured => Money.FromMinorUnits(CapturedMinorUnits, Currency, Exponent);

    public Money Refunded => Money.FromMinorUnits(RefundedMinorUnits, Currency, Exponent);

    /// <summary>What is still refundable.</summary>
    public Money RefundableRemaining =>
        Money.FromMinorUnits(
            Math.Max(0, CapturedMinorUnits - RefundedMinorUnits), Currency, Exponent);

    public bool IsTerminal => Status is PaymentIntentStatus.Failed
        or PaymentIntentStatus.Cancelled
        or PaymentIntentStatus.Expired
        or PaymentIntentStatus.Refunded;

    public static PaymentIntent Create(
        PaymentIntentId id,
        Guid payerUserId,
        FinancialAccountId accountId,
        Money amount,
        PaymentMethodKind methodKind,
        string description,
        DateTimeOffset createdAt,
        SubjectReference? subject = null,
        string? idempotencyKey = null,
        string? countryCode = null,
        int lifetimeHours = FinancialLimits.PaymentIntentLifetimeHours,
        PaymentPurpose purpose = PaymentPurpose.Purchase)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        if (amount.IsZero)
        {
            throw new DomainException(
                "A payment intent for nothing is not a payment. Grant credit instead.",
                FinancialErrorCodes.AmountInvalid);
        }

        var intent = new PaymentIntent(
            id, payerUserId, accountId, amount, methodKind, description.Trim(),
            createdAt, createdAt.AddHours(lifetimeHours))
        {
            Purpose = purpose,
            SubjectType = subject?.SubjectType,
            SubjectId = subject?.SubjectId,
            IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey)
                ? null
                : idempotencyKey.Trim(),
            CountryCode = string.IsNullOrWhiteSpace(countryCode)
                ? null
                : countryCode.Trim().ToUpperInvariant()
        };

        intent.RaiseDomainEvent(new PaymentIntentCreatedDomainEvent
        {
            PaymentIntentId = id,
            PayerUserId = payerUserId,
            AmountMinorUnits = amount.MinorUnits,
            Currency = amount.Currency,
            MethodKind = methodKind,
            SubjectType = subject?.SubjectType,
            SubjectId = subject?.SubjectId,
            CountryCode = intent.CountryCode,
            ExpiresAt = intent.ExpiresAt,
            CreatedAt = createdAt
        });

        return intent;
    }

    /// <summary>Hands the intent to a gateway. Draft to pending.</summary>
    public void Submit(Guid attemptId, DateTimeOffset now, string? provider = null)
    {
        Require(PaymentIntentStatus.Draft, PaymentIntentStatus.Pending);

        Status = PaymentIntentStatus.Pending;
        _attempts.Add(PaymentAttempt.Record(
            attemptId, _attempts.Count + 1, PaymentAttemptStatus.Initiated, now, provider));
        Touch(now);
    }

    /// <summary>
    /// The issuer wants the payer to do something. Stays pending; records the attempt and the
    /// payload the client must act on.
    /// </summary>
    public void RequireAction(
        Guid attemptId, string payload, DateTimeOffset now, string? provider = null)
    {
        Require(PaymentIntentStatus.Draft, PaymentIntentStatus.Pending);

        Status = PaymentIntentStatus.Pending;
        NextActionPayload = string.IsNullOrWhiteSpace(payload) ? null : payload.Trim();
        _attempts.Add(PaymentAttempt.Record(
            attemptId, _attempts.Count + 1, PaymentAttemptStatus.RequiresAction, now, provider));
        Touch(now);
    }

    public void Authorize(
        Guid attemptId, GatewayReference gateway, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        Require(PaymentIntentStatus.Draft, PaymentIntentStatus.Pending);

        Status = PaymentIntentStatus.Authorized;
        SetGateway(gateway);
        AuthorizedAt = now;
        NextActionPayload = null;
        _attempts.Add(PaymentAttempt.Record(
            attemptId, _attempts.Count + 1, PaymentAttemptStatus.Succeeded, now,
            gateway.Provider, gateway.ExternalReference));
        Touch(now);

        RaiseDomainEvent(new PaymentAuthorizedDomainEvent
        {
            PaymentIntentId = Id,
            PayerUserId = PayerUserId,
            AmountMinorUnits = AmountMinorUnits,
            Currency = Currency,
            Provider = gateway.Provider,
            ExternalReference = gateway.ExternalReference,
            AuthorizedAt = now
        });
    }

    /// <summary>
    /// Takes the money.
    /// <para>
    /// Capturing less than was authorised is legitimate and common — a partial fulfilment — so the
    /// amount is a parameter rather than assumed. Capturing more than was authorised is not, and the
    /// aggregate refuses.
    /// </para>
    /// <para>
    /// Permitted directly from <c>Pending</c> as well as from <c>Authorized</c>, because several
    /// real-world methods have no separate authorisation step at all: a bank transfer and a mobile
    /// money push either happened or did not.
    /// </para>
    /// </summary>
    public void Capture(Money amount, DateTimeOffset now, GatewayReference? gateway = null)
    {
        ArgumentNullException.ThrowIfNull(amount);
        Require(PaymentIntentStatus.Authorized, PaymentIntentStatus.Pending);

        if (!string.Equals(amount.Currency, Currency, StringComparison.Ordinal))
        {
            throw new DomainException(
                $"A {amount.Currency} capture cannot settle a {Currency} intent.",
                FinancialErrorCodes.CurrencyMismatch);
        }

        if (amount.MinorUnits > AmountMinorUnits)
        {
            throw new DomainException(
                "A capture cannot exceed the authorised amount.",
                FinancialErrorCodes.PaymentCaptureFailed);
        }

        Status = PaymentIntentStatus.Captured;
        CapturedMinorUnits = amount.MinorUnits;
        CapturedAt = now;
        NextActionPayload = null;

        if (gateway is not null)
        {
            SetGateway(gateway);
        }

        Touch(now);

        RaiseDomainEvent(new PaymentCapturedDomainEvent
        {
            PaymentIntentId = Id,
            PayerUserId = PayerUserId,
            AccountId = AccountId,
            Purpose = Purpose,
            MethodKind = MethodKind,
            AmountMinorUnits = amount.MinorUnits,
            Currency = Currency,
            SubjectType = Subject?.SubjectType,
            SubjectId = Subject?.SubjectId,
            Provider = Gateway?.Provider,
            ExternalReference = Gateway?.ExternalReference,
            CountryCode = CountryCode,
            CapturedAt = now
        });
    }

    /// <summary>
    /// Marks the payment reconciled against the provider's own record.
    /// <para>
    /// Deliberately a separate state from captured. Capture is what this platform believes;
    /// settlement is what the provider confirms, and the gap between them is exactly where a
    /// chargeback, a reversal or a fee adjustment lives.
    /// </para>
    /// </summary>
    public void Settle(DateTimeOffset now, string? externalStatus = null)
    {
        Require(PaymentIntentStatus.Captured);

        Status = PaymentIntentStatus.Settled;
        SettledAt = now;

        if (GatewayProvider is not null && !string.IsNullOrWhiteSpace(externalStatus))
        {
            GatewayExternalStatus = externalStatus.Trim();
        }

        Touch(now);
    }

    public void Fail(
        Guid attemptId,
        string? failureCode,
        string? failureMessage,
        bool isRetryable,
        DateTimeOffset now,
        string? provider = null)
    {
        if (IsTerminal)
        {
            return;
        }

        Status = PaymentIntentStatus.Failed;
        FailureCode = string.IsNullOrWhiteSpace(failureCode) ? null : failureCode.Trim();
        FailureMessage = string.IsNullOrWhiteSpace(failureMessage) ? null : failureMessage.Trim();
        TerminatedAt = now;
        NextActionPayload = null;
        _attempts.Add(PaymentAttempt.Record(
            attemptId, _attempts.Count + 1, PaymentAttemptStatus.Failed, now,
            provider, null, FailureCode, FailureMessage));
        Touch(now);

        RaiseDomainEvent(new PaymentFailedDomainEvent
        {
            PaymentIntentId = Id,
            PayerUserId = PayerUserId,
            AmountMinorUnits = AmountMinorUnits,
            Currency = Currency,
            FailureCode = FailureCode,
            FailureMessage = FailureMessage,
            IsRetryable = isRetryable,
            FailedAt = now
        });
    }

    /// <summary>
    /// Stops the intent before any money moved.
    /// <para>
    /// Refuses once captured. Cancelling a captured payment is a refund, and letting one method mean
    /// both would give the platform two names for an operation whose accounting is entirely
    /// different.
    /// </para>
    /// </summary>
    public void Cancel(DateTimeOffset now, bool expired = false)
    {
        if (IsTerminal)
        {
            return;
        }

        if (Status is PaymentIntentStatus.Captured or PaymentIntentStatus.Settled)
        {
            throw new DomainException(
                "A captured payment cannot be cancelled. Refund it.",
                FinancialErrorCodes.PaymentTransitionInvalid);
        }

        Status = expired ? PaymentIntentStatus.Expired : PaymentIntentStatus.Cancelled;
        TerminatedAt = now;
        NextActionPayload = null;
        Touch(now);

        RaiseDomainEvent(new PaymentCancelledDomainEvent
        {
            PaymentIntentId = Id,
            PayerUserId = PayerUserId,
            AmountMinorUnits = AmountMinorUnits,
            Currency = Currency,
            Expired = expired,
            CancelledAt = now
        });
    }

    /// <summary>
    /// Returns money, in whole or in part.
    /// <para>
    /// The intent moves to <c>Refunded</c> only when everything captured has gone back; a partial
    /// refund leaves it captured or settled, because it is still a payment that happened.
    /// </para>
    /// </summary>
    public RefundRecord Refund(
        Guid refundId,
        Money amount,
        RefundReason reason,
        DateTimeOffset now,
        Guid? issuedByUserId = null,
        string? note = null,
        string? externalReference = null)
    {
        ArgumentNullException.ThrowIfNull(amount);

        if (Status is not (PaymentIntentStatus.Captured or PaymentIntentStatus.Settled))
        {
            throw new DomainException(
                "Only a captured payment can be refunded.",
                FinancialErrorCodes.RefundNotAllowed);
        }

        if (!string.Equals(amount.Currency, Currency, StringComparison.Ordinal))
        {
            throw new DomainException(
                $"A {Currency} payment cannot be refunded in {amount.Currency}.",
                FinancialErrorCodes.CurrencyMismatch);
        }

        if (amount.MinorUnits > CapturedMinorUnits - RefundedMinorUnits)
        {
            throw new DomainException(
                "That refund is larger than what remains of the captured amount.",
                FinancialErrorCodes.RefundExceedsCaptured);
        }

        var record = RefundRecord.Create(
            refundId, amount.MinorUnits, reason, now,
            note: note, issuedByUserId: issuedByUserId, externalReference: externalReference);

        _refunds.Add(record);
        RefundedMinorUnits += amount.MinorUnits;

        var isFull = RefundedMinorUnits >= CapturedMinorUnits;

        if (isFull)
        {
            Status = PaymentIntentStatus.Refunded;
            TerminatedAt = now;
        }

        Touch(now);

        RaiseDomainEvent(new RefundIssuedDomainEvent
        {
            RefundId = RefundId.From(refundId),
            PaymentIntentId = Id,
            PayerUserId = PayerUserId,
            AmountMinorUnits = amount.MinorUnits,
            Currency = Currency,
            IsFullRefund = isFull,
            Reason = reason,
            SubjectType = Subject?.SubjectType,
            SubjectId = Subject?.SubjectId,
            IssuedAt = now
        });

        return record;
    }

    /// <summary>Whether the intent has lapsed without anybody finishing it.</summary>
    public bool HasExpired(DateTimeOffset now) =>
        !IsTerminal
        && Status is PaymentIntentStatus.Draft or PaymentIntentStatus.Pending
        && now >= ExpiresAt;

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    private void SetGateway(GatewayReference gateway)
    {
        GatewayProvider = gateway.Provider;
        GatewayExternalReference = gateway.ExternalReference;
        GatewayExternalStatus = gateway.ExternalStatus;
    }

    private void Require(params PaymentIntentStatus[] allowed)
    {
        if (Array.IndexOf(allowed, Status) < 0)
        {
            throw new DomainException(
                $"A payment intent in '{Status}' cannot make that transition. "
                + $"Expected one of: {string.Join(", ", allowed)}.",
                FinancialErrorCodes.PaymentTransitionInvalid);
        }
    }

    private void Touch(DateTimeOffset now)
    {
        Revision++;
        ModifiedAt = now;
    }
}
