using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Payments;

namespace Diaspora.Modules.Financial.Domain.Invoices;

/// <summary>Retrieval and staging for invoices.</summary>
public interface IInvoiceRepository
{
    void Add(Invoice invoice);

    /// <summary>
    /// Stops tracking an invoice the database refused, so the next save does not retry its insert.
    /// </summary>
    void Forget(Invoice invoice);

    Task<Invoice?> FindAsync(InvoiceId invoiceId, CancellationToken cancellationToken = default);

    Task<Invoice?> FindByPaymentIntentAsync(
        PaymentIntentId paymentIntentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// An owner's invoices, newest number first, keyset-paged on (year, sequence) strictly below the
    /// cursor when one is given.
    /// </summary>
    Task<IReadOnlyList<Invoice>> ListByOwnerAsync(
        Guid ownerUserId,
        int? beforeYear,
        long? beforeSequence,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>The next unused sequence in <paramref name="year"/>. One more than the highest issued.</summary>
    Task<long> NextSequenceAsync(int year, CancellationToken cancellationToken = default);

    /// <summary>
    /// The payer's captured payments the platform kept and has not yet invoiced, oldest capture first.
    /// <para>
    /// Purpose <c>Purchase</c> with something captured, and not a donation or a contribution — those
    /// carry their own receipt. Read without tracking: issuing an invoice never writes to a payment.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<PaymentIntent>> ListUninvoicedPaymentsAsync(
        Guid payerUserId, int take, CancellationToken cancellationToken = default);

    /// <summary>The payments behind a page of invoices, read untracked, for their refund state.</summary>
    Task<IReadOnlyDictionary<PaymentIntentId, PaymentIntent>> ListPaymentsAsync(
        IReadOnlyCollection<PaymentIntentId> paymentIntentIds,
        CancellationToken cancellationToken = default);
}
