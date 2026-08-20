using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;

namespace Diaspora.Modules.Financial.Domain.Payments;

/// <summary>Retrieval and staging for payment intents.</summary>
public interface IPaymentIntentRepository
{
    void Add(PaymentIntent intent);

    void Update(PaymentIntent intent);

    Task<PaymentIntent?> FindAsync(
        PaymentIntentId intentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The idempotency check. A duplicate key is a double-tapped pay button.
    /// <para>
    /// <b><paramref name="payerUserId"/> is required, and it is not a filter — it is half the
    /// key.</b> This method used to match on the idempotency key alone, which made the key a
    /// global name: a member whose key collided with another member's was answered with that
    /// member's intent, at 200, and their own payment was never created. Neither side saw an
    /// error. A collision needs no malice — a client that derives its key from an operation
    /// and a subject id produces colliding keys by construction.
    /// </para>
    /// <para>
    /// The parameter has no default for the same reason. An optional owner is an owner a
    /// future call site can omit, and this defect is what omitting it looks like.
    /// </para>
    /// </summary>
    Task<PaymentIntent?> FindByIdempotencyKeyAsync(
        Guid payerUserId, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// The webhook's entry point: a provider knows its own reference and nothing else about us.
    /// </summary>
    Task<PaymentIntent?> FindByExternalReferenceAsync(
        string provider, string externalReference, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentIntent>> ListByPayerAsync(
        Guid payerUserId,
        PaymentIntentStatus? status,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A payer's intents of one purpose, cursor-paged.
    /// <para>
    /// The purpose filter is what makes deposit history a view of the payments table rather than a
    /// second table — and the cursor is ADR-017's: excluded by identity, because two intents created
    /// in one request share a timestamp and a strict inequality would drop one for ever.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<PaymentIntent>> ListByPayerAsync(
        Guid payerUserId,
        PaymentIntentStatus? status,
        PaymentPurpose? purpose,
        DateTimeOffset? before,
        IReadOnlyList<Guid> excludeIds,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Captured payments that no provider has confirmed as settled, older than a moment.
    /// <para>
    /// The reconciliation sweep's input from this side: what the platform believes it has and has
    /// not yet had confirmed.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<PaymentIntent>> ListUnsettledAsync(
        DateTimeOffset capturedBefore, int take, CancellationToken cancellationToken = default);

    /// <summary>Intents past their expiry that nothing has closed. The sweep's input.</summary>
    Task<IReadOnlyList<PaymentIntent>> ListExpiredAsync(
        DateTimeOffset asOf, int take, CancellationToken cancellationToken = default);
}
