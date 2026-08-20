using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;

namespace Diaspora.Modules.Financial.Domain.Purchases;

/// <summary>Retrieval and staging for purchases.</summary>
public interface IMonetizationPurchaseRepository
{
    void Add(MonetizationPurchase purchase);

    void Update(MonetizationPurchase purchase);

    Task<MonetizationPurchase?> FindAsync(
        MonetizationPurchaseId purchaseId, CancellationToken cancellationToken = default);

    Task<MonetizationPurchase?> FindByPaymentIntentAsync(
        PaymentIntentId paymentIntentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// A buyer's purchases, cursor-paged.
    /// <para>
    /// Excluded by identity at the boundary instant rather than by a strict timestamp comparison —
    /// two purchases made in one request share a <c>CreatedAt</c>, and a strict cursor drops
    /// whichever did not fit on the page. ADR-017.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<MonetizationPurchase>> ListByBuyerAsync(
        Guid buyerUserId,
        PurchaseStatus? status,
        DateTimeOffset? before,
        IReadOnlyList<Guid> excludeIds,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>Whether this subject has ever bought this product. The one-time-only rule.</summary>
    Task<bool> HasCompletedPurchaseAsync(
        Guid buyerUserId,
        string productCode,
        SubjectReference subject,
        CancellationToken cancellationToken = default);
}
