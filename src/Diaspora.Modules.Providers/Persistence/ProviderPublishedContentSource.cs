using Diaspora.Contracts.Content.V1;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Providers.Persistence;

/// <summary>
/// This module's answer to the two cross-module content seams: how many of its items exist and
/// were added (FRONTEND-PHASE15-GAP-001), and what an item is called (a creator's sponsored list).
/// <para>
/// <b>Live</b> means active — the same set this module's own counts use. <b>Added</b> is
/// <c>PublishedAt</c>, which a provider sets once — on its first publication — and never moves.
/// </para>
/// <para>
/// Soft-deleted rows are excluded by the context's global filter, from both the total and the
/// additions.
/// </para>
/// </summary>
public sealed class ProviderPublishedContentSource : IPlatformInventorySource, IContentTitleSource
{
    private static readonly ProviderStatus[] Live =
    [
        ProviderStatus.Active
    ];

    private readonly DiasporaDbContext _dbContext;

    public ProviderPublishedContentSource(DiasporaDbContext dbContext) => _dbContext = dbContext;

    public string Key => "providers";

    public string ContentType => "Provider";

    public async Task<PlatformInventoryCount> CountAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        var rows = _dbContext.Set<Provider>().AsNoTracking();

        var total = await rows
            .LongCountAsync(row => Live.Contains(row.Status), cancellationToken);

        var added = await rows
            .Where(row => row.PublishedAt != null && row.PublishedAt >= from && row.PublishedAt < to)
            .Select(row => row.PublishedAt!.Value)
            .ToListAsync(cancellationToken);

        return new PlatformInventoryCount(total, added);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> FindTitlesAsync(
        IReadOnlyCollection<Guid> contentIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentIds);

        if (contentIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        // Compared as the typed identifier: Contains over a value-converted key translates, and
        // reaching through to .Value inside the query would not.
        var keys = contentIds.Distinct().Select(ProviderId.From).ToList();

        var rows = await _dbContext.Set<Provider>()
            .AsNoTracking()
            .Where(row => keys.Contains(row.Id))
            .Select(row => new { row.Id, Title = row.Name.Value })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.Id.Value, row => row.Title);
    }
}
