using Diaspora.Platform.Transactions;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Persistence.Transactions;

/// <summary>
/// The receipt table's housekeeping, on EF Core.
/// <para>
/// Bounded and index-covered, like every other sweep in the platform. This table turns over faster
/// than any other — one row per transaction on a retrying provider — so a sweep that scanned it
/// would be the most expensive query the platform ran, against the table with the least to say.
/// </para>
/// </summary>
public sealed class EfTransactionReceiptStore : ITransactionReceiptStore
{
    private readonly DiasporaDbContext _context;

    public EfTransactionReceiptStore(DiasporaDbContext context)
    {
        _context = context;
    }

    public async Task<int> PurgeAsync(
        long createdBeforeTicks, int batchSize, CancellationToken cancellationToken = default)
    {
        // Keys first, then a delete naming them. `DELETE … LIMIT` is not portable — PostgreSQL
        // does not support it at all — and this keeps the delete's lock footprint to exactly the
        // rows chosen.
        var expired = await _context.Set<TransactionReceipt>()
            .Where(receipt => receipt.CreatedAtTicks < createdBeforeTicks)
            .OrderBy(receipt => receipt.CreatedAtTicks)
            .Select(receipt => receipt.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0)
        {
            return 0;
        }

        return await _context.Set<TransactionReceipt>()
            .Where(receipt => expired.Contains(receipt.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        _context.Set<TransactionReceipt>().CountAsync(cancellationToken);
}
