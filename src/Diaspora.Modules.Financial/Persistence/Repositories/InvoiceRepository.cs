using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Invoices;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Persistence.Repositories;

/// <summary>EF Core repository for invoices.</summary>
public sealed class InvoiceRepository : IInvoiceRepository
{
    private readonly DiasporaDbContext _dbContext;

    public InvoiceRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<Invoice> Invoices => _dbContext.Set<Invoice>();

    public void Add(Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        Invoices.Add(invoice);
    }

    public void Forget(Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        _dbContext.Entry(invoice).State = EntityState.Detached;
    }

    public async Task<Invoice?> FindAsync(
        InvoiceId invoiceId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoiceId);

        return await Invoices.AsNoTracking()
            .FirstOrDefaultAsync(invoice => invoice.Id == invoiceId, cancellationToken);
    }

    public async Task<Invoice?> FindByPaymentIntentAsync(
        PaymentIntentId paymentIntentId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paymentIntentId);

        return await Invoices.AsNoTracking()
            .FirstOrDefaultAsync(
                invoice => invoice.PaymentIntentId == paymentIntentId, cancellationToken);
    }

    public async Task<IReadOnlyList<Invoice>> ListByOwnerAsync(
        Guid ownerUserId,
        int? beforeYear,
        long? beforeSequence,
        int take,
        CancellationToken cancellationToken = default)
    {
        var invoices = Invoices.AsNoTracking().Where(invoice => invoice.OwnerUserId == ownerUserId);

        if (beforeYear is { } year && beforeSequence is { } sequence)
        {
            invoices = invoices.Where(invoice =>
                invoice.Year < year || (invoice.Year == year && invoice.Sequence < sequence));
        }

        return await invoices
            .OrderByDescending(invoice => invoice.Year)
            .ThenByDescending(invoice => invoice.Sequence)
            .Take(Math.Clamp(take, 1, Domain.Policies.FinancialLimits.MaxLedgerPageSize))
            .ToListAsync(cancellationToken);
    }

    public async Task<long> NextSequenceAsync(int year, CancellationToken cancellationToken = default)
    {
        // Nullable so an empty year reads as null rather than throwing on an empty aggregate.
        var highest = await Invoices
            .Where(invoice => invoice.Year == year)
            .MaxAsync(invoice => (long?)invoice.Sequence, cancellationToken);

        return (highest ?? 0) + 1;
    }

    public async Task<IReadOnlyList<PaymentIntent>> ListUninvoicedPaymentsAsync(
        Guid payerUserId, int take, CancellationToken cancellationToken = default)
    {
        var invoices = Invoices;
        var donations = _dbContext.Set<Donation>();
        var contributions = _dbContext.Set<CommunityContribution>();

        return await _dbContext.Set<PaymentIntent>()
            .AsNoTracking()
            .Where(intent => intent.PayerUserId == payerUserId
                && intent.Purpose == PaymentPurpose.Purchase
                && intent.CapturedMinorUnits > 0
                && intent.CapturedAt != null
                && !invoices.Any(invoice => invoice.PaymentIntentId == intent.Id)
                && !donations.Any(donation => donation.PaymentIntentId == intent.Id)
                && !contributions.Any(contribution => contribution.PaymentIntentId == intent.Id))
            .OrderBy(intent => intent.CapturedAt)
            .Take(Math.Clamp(take, 1, Domain.Policies.FinancialLimits.MaxLedgerPageSize))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<PaymentIntentId, PaymentIntent>> ListPaymentsAsync(
        IReadOnlyCollection<PaymentIntentId> paymentIntentIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paymentIntentIds);

        if (paymentIntentIds.Count == 0)
        {
            return new Dictionary<PaymentIntentId, PaymentIntent>();
        }

        var wanted = paymentIntentIds.Distinct().ToList();

        var intents = await _dbContext.Set<PaymentIntent>()
            .AsNoTracking()
            .Where(intent => wanted.Contains(intent.Id))
            .ToListAsync(cancellationToken);

        return intents.ToDictionary(intent => intent.Id);
    }
}
