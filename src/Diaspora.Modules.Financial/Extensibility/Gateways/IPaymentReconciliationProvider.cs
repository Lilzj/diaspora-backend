namespace Diaspora.Modules.Financial.Extensibility.Gateways;

/// <summary>
/// Fetches a provider's own record of what it did, so the platform can check itself.
/// <para>
/// <b>The seam most likely to be needed first in production and least likely to be built in
/// advance.</b> A payment platform that cannot answer "does our ledger match the provider's
/// settlement file" is a platform that finds out about a discrepancy from an accountant. Declared
/// here so a reconciliation job is an adapter rather than a rewrite; no implementation ships.
/// </para>
/// </summary>
public interface IPaymentReconciliationProvider
{
    string Provider { get; }

    /// <summary>Everything the provider settled in a window.</summary>
    Task<IReadOnlyList<ReconciliationRecord>> FetchAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}
