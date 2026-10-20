using Diaspora.Contracts.Content.V1;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Jobs.Persistence;

/// <summary>
/// This module's answer to the two cross-module content seams: how many of its items exist and
/// were added (FRONTEND-PHASE15-GAP-001), and what an item is called (a creator's sponsored list).
/// <para>
/// <b>Live</b> means published and not yet filled, expired or archived — the same set this module's own counts use. <b>Added</b> is
/// <c>FirstPublishedAt</c>, which never moves. <c>PublishedAt</c> moves on every republish, and counting it would report an edited item as new.
/// </para>
/// <para>
/// Soft-deleted rows are excluded by the context's global filter, from both the total and the
/// additions.
/// </para>
/// </summary>
public sealed class JobPublishedContentSource : IPlatformInventorySource, IContentTitleSource
{
    private static readonly JobStatus[] Live =
    [
        JobStatus.Published,
        JobStatus.Open,
        JobStatus.ReceivingApplications,
        JobStatus.Interviewing,
        JobStatus.OfferMade
    ];

    private readonly DiasporaDbContext _dbContext;

    public JobPublishedContentSource(DiasporaDbContext dbContext) => _dbContext = dbContext;

    public string Key => "jobs";

    public string ContentType => "Job";

    public async Task<PlatformInventoryCount> CountAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        var rows = _dbContext.Set<JobPosting>().AsNoTracking();

        var total = await rows
            .LongCountAsync(row => Live.Contains(row.Status), cancellationToken);

        var added = await rows
            .Where(row => row.FirstPublishedAt != null && row.FirstPublishedAt >= from && row.FirstPublishedAt < to)
            .Select(row => row.FirstPublishedAt!.Value)
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
        var keys = contentIds.Distinct().Select(JobPostingId.From).ToList();

        var rows = await _dbContext.Set<JobPosting>()
            .AsNoTracking()
            .Where(row => keys.Contains(row.Id))
            .Select(row => new { row.Id, Title = row.Title.Value })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.Id.Value, row => row.Title);
    }
}
