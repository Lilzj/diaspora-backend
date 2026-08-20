using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;

namespace Diaspora.Modules.Financial.Domain.Payments;

/// <summary>Retrieval and staging for member refund requests.</summary>
public interface IRefundRequestRepository
{
    void Add(RefundRequest request);

    void Update(RefundRequest request);

    Task<RefundRequest?> FindAsync(
        RefundRequestId requestId, CancellationToken cancellationToken = default);

    /// <summary>The open request for a payment, or null. At most one exists.</summary>
    Task<RefundRequest?> FindOpenAsync(
        PaymentIntentId paymentIntentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RefundRequest>> ListForUserAsync(
        Guid userId, int take, CancellationToken cancellationToken = default);

    /// <summary>Everything waiting for somebody to answer, oldest first. The staff queue.</summary>
    Task<IReadOnlyList<RefundRequest>> ListByStatusAsync(
        RefundRequestStatus status, int take, CancellationToken cancellationToken = default);
}
