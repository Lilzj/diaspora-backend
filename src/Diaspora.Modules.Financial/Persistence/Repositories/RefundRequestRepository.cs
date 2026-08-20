using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Persistence.Repositories;

/// <summary>EF Core repository for member refund requests.</summary>
public sealed class RefundRequestRepository : IRefundRequestRepository
{
    private readonly DiasporaDbContext _dbContext;

    public RefundRequestRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<RefundRequest> Requests => _dbContext.Set<RefundRequest>();

    public void Add(RefundRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        Requests.Add(request);
    }

    public void Update(RefundRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        FinancialAggregateUpdater.MarkDirty(_dbContext, request);
    }

    public async Task<RefundRequest?> FindAsync(
        RefundRequestId requestId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestId);

        return await Requests.FirstOrDefaultAsync(
            request => request.Id == requestId, cancellationToken);
    }

    public async Task<RefundRequest?> FindOpenAsync(
        PaymentIntentId paymentIntentId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paymentIntentId);

        var open = paymentIntentId.Value;

        var staged = Requests.Local.FirstOrDefault(
            request => request.OpenForPaymentIntentId == open);

        return staged ?? await Requests.FirstOrDefaultAsync(
            request => request.OpenForPaymentIntentId == open, cancellationToken);
    }

    public async Task<IReadOnlyList<RefundRequest>> ListForUserAsync(
        Guid userId, int take, CancellationToken cancellationToken = default) =>
        await Requests
            .Where(request => request.RequestedByUserId == userId)
            .OrderByDescending(request => request.RequestedAt)
            .Take(Math.Clamp(take, 1, Domain.Policies.FinancialLimits.MaxLedgerPageSize))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RefundRequest>> ListByStatusAsync(
        RefundRequestStatus status, int take, CancellationToken cancellationToken = default) =>
        await Requests
            .Where(request => request.Status == status)
            .OrderBy(request => request.RequestedAt)
            .Take(Math.Clamp(take, 1, Domain.Policies.FinancialLimits.MaxLedgerPageSize))
            .ToListAsync(cancellationToken);
}
