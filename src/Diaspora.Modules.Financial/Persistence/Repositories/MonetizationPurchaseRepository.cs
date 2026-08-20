using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Purchases;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Persistence.Repositories;

/// <summary>EF Core repository for purchases.</summary>
public sealed class MonetizationPurchaseRepository : IMonetizationPurchaseRepository
{
    private readonly DiasporaDbContext _dbContext;

    public MonetizationPurchaseRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<MonetizationPurchase> Purchases => _dbContext.Set<MonetizationPurchase>();

    public void Add(MonetizationPurchase purchase)
    {
        ArgumentNullException.ThrowIfNull(purchase);

        Purchases.Add(purchase);
    }

    public void Update(MonetizationPurchase purchase)
    {
        ArgumentNullException.ThrowIfNull(purchase);

        FinancialAggregateUpdater.MarkDirty(_dbContext, purchase);
    }

    public async Task<MonetizationPurchase?> FindAsync(
        MonetizationPurchaseId purchaseId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(purchaseId);

        return await Purchases.FirstOrDefaultAsync(
            purchase => purchase.Id == purchaseId, cancellationToken);
    }

    public async Task<MonetizationPurchase?> FindByPaymentIntentAsync(
        PaymentIntentId paymentIntentId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paymentIntentId);

        return await Purchases.FirstOrDefaultAsync(
            purchase => purchase.PaymentIntentId == paymentIntentId, cancellationToken);
    }

    public async Task<IReadOnlyList<MonetizationPurchase>> ListByBuyerAsync(
        Guid buyerUserId,
        PurchaseStatus? status,
        DateTimeOffset? before,
        IReadOnlyList<Guid> excludeIds,
        int take,
        CancellationToken cancellationToken = default)
    {
        var purchases = Purchases.Where(purchase => purchase.BuyerUserId == buyerUserId);

        if (status is { } wanted)
        {
            purchases = purchases.Where(purchase => purchase.Status == wanted);
        }

        if (before is { } boundary)
        {
            purchases = purchases.Where(purchase => purchase.CreatedAt <= boundary);
        }

        if (excludeIds is { Count: > 0 })
        {
            // Compared at the value-object level: reaching through a value-converted identifier to
            // its `.Value` compiles and throws at runtime.
            var excluded = excludeIds.Select(MonetizationPurchaseId.From).ToList();

            purchases = purchases.Where(purchase => !excluded.Contains(purchase.Id));
        }

        return await purchases
            .OrderByDescending(purchase => purchase.CreatedAt)
            .Take(Math.Clamp(take, 1, Domain.Policies.FinancialLimits.MaxLedgerPageSize))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> HasCompletedPurchaseAsync(
        Guid buyerUserId,
        string productCode,
        SubjectReference subject,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        if (string.IsNullOrWhiteSpace(productCode))
        {
            return Task.FromResult(false);
        }

        var normalized = productCode.Trim().ToLowerInvariant();
        var subjectType = subject.SubjectType;
        var subjectId = subject.SubjectId;

        return Purchases.AnyAsync(
            purchase => purchase.BuyerUserId == buyerUserId
                && purchase.ProductCode == normalized
                && purchase.Status == PurchaseStatus.Completed
                && purchase.Subject.SubjectType == subjectType
                && purchase.Subject.SubjectId == subjectId,
            cancellationToken);
    }
}
