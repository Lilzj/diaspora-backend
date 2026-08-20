namespace Diaspora.Platform.Transactions;

/// <summary>
/// Collects spent verification receipts.
/// <para>
/// The only operation the receipt table needs beyond the insert the unit of work performs and the
/// lookup its verification performs — both of which live inside the unit of work, because they are
/// part of a transaction rather than services anything else may call.
/// </para>
/// <para>
/// A port rather than a direct dependency for the ordinary reason: the sweep runs from a hosted
/// service in the infrastructure layer, which does not reference the persistence layer and should
/// not start now.
/// </para>
/// </summary>
public interface ITransactionReceiptStore
{
    /// <summary>
    /// Deletes receipts written before the cutoff, up to <paramref name="batchSize"/> rows.
    /// Returns how many were removed so the caller can loop until a batch comes back short.
    /// </summary>
    Task<int> PurgeAsync(long createdBeforeTicks, int batchSize, CancellationToken cancellationToken = default);

    /// <summary>How many receipts are currently retained. Read by the health check.</summary>
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
