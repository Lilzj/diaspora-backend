using Diaspora.Modules.Financial.Domain.Webhooks;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Persistence.Repositories;

/// <summary>EF Core repository for the record of received provider callbacks.</summary>
public sealed class ProcessedWebhookEventRepository : IProcessedWebhookEventRepository
{
    private readonly DiasporaDbContext _dbContext;

    public ProcessedWebhookEventRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<ProcessedWebhookEvent> Events => _dbContext.Set<ProcessedWebhookEvent>();

    public void Add(ProcessedWebhookEvent record)
    {
        ArgumentNullException.ThrowIfNull(record);

        Events.Add(record);
    }

    /// <summary>
    /// The stored outcome for a provider's event identifier.
    /// <para>
    /// Consults the change tracker first for the reason <c>WalletRepository</c> records: a callback
    /// that has been recorded but not yet saved is invisible to a database query, and the unique
    /// index would then refuse the second insert at save time with an error naming two columns and
    /// nothing about why.
    /// </para>
    /// </summary>
    public async Task<ProcessedWebhookEvent?> FindAsync(
        string provider, string providerEventId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(providerEventId))
        {
            return null;
        }

        var normalizedProvider = ProcessedWebhookEvent.NormalizeProvider(provider);
        var normalizedEventId = providerEventId.Trim();

        var staged = Events.Local.FirstOrDefault(
            record => string.Equals(record.Provider, normalizedProvider, StringComparison.Ordinal)
                && string.Equals(record.ProviderEventId, normalizedEventId, StringComparison.Ordinal));

        if (staged is not null)
        {
            return staged;
        }

        return await Events.FirstOrDefaultAsync(
            record => record.Provider == normalizedProvider
                && record.ProviderEventId == normalizedEventId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<ProcessedWebhookEvent>> ListRecentAsync(
        string? provider, int take, CancellationToken cancellationToken = default)
    {
        var records = Events.AsQueryable();

        if (!string.IsNullOrWhiteSpace(provider))
        {
            var normalized = ProcessedWebhookEvent.NormalizeProvider(provider);

            records = records.Where(record => record.Provider == normalized);
        }

        return await records
            .OrderByDescending(record => record.ReceivedAt)
            .Take(Math.Clamp(take, 1, Domain.Policies.FinancialLimits.MaxLedgerPageSize))
            .ToListAsync(cancellationToken);
    }
}
