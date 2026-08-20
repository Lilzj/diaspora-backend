using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Domain.Saved;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Jobs.Persistence.Repositories;

/// <summary>EF Core repository for saved jobs.</summary>
public sealed class SavedJobRepository : ISavedJobRepository
{
    private readonly DiasporaDbContext _dbContext;

    public SavedJobRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<SavedJob> SavedJobs => _dbContext.Set<SavedJob>();

    public async Task<SavedJob?> GetAsync(
        SavedJobId id, CancellationToken cancellationToken = default) =>
        await SavedJobs.FirstOrDefaultAsync(saved => saved.Id == id, cancellationToken);

    public async Task<SavedJob?> GetByCandidateAndPostingAsync(
        CandidateProfileId candidateId,
        JobPostingId postingId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidateId);
        ArgumentNullException.ThrowIfNull(postingId);

        return await SavedJobs.FirstOrDefaultAsync(
            saved => saved.CandidateId == candidateId && saved.PostingId == postingId,
            cancellationToken);
    }

    public async Task<CursorPagedResult<SavedJob>> GetForCandidateAsync(
        CandidateProfileId candidateId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidateId);

        var query = SavedJobs.AsNoTracking()
            .Where(saved => saved.CandidateId == candidateId);

        var parsed = JobCursor.Parse(cursor);

        if (parsed?.AsTimestamp() is { } after)
        {
            // Excludes what the previous page already returned at this timestamp rather than
            // everything sharing it. See CursorBoundary.
            var tie = CursorBoundary.Exclusion(parsed.LastKey, SavedJobId.From);
            var seen = tie.Ids;
            var exhausted = tie.IsExhausted;

            query = query.Where(saved =>
                saved.CreatedAt < after
                || (saved.CreatedAt == after && !exhausted && !seen.Contains(saved.Id)));
        }

        query = query.OrderByDescending(saved => saved.CreatedAt)
            .ThenByDescending(saved => saved.Id);

        var effectiveLimit = Math.Clamp(limit, 1, JobLimits.MaxPageSize);
        var rows = await query.Take(effectiveLimit + 1).ToListAsync(cancellationToken);

        var hasMore = rows.Count > effectiveLimit;

        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var next = hasMore && rows.Count > 0
            ? JobCursor.FromTimestamp(
                rows[^1].CreatedAt,
                CursorBoundary.Key(
                    rows,
                    row => row.CreatedAt,
                    row => row.Id.Value,
                    parsed?.LastKey,
                    parsed?.AsTimestamp())).Encode()
            : null;

        return CursorPagedResult<SavedJob>.Create(rows, next, hasMore);
    }

    public async Task<int> CountForCandidateAsync(
        CandidateProfileId candidateId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidateId);

        return await SavedJobs.CountAsync(
            saved => saved.CandidateId == candidateId, cancellationToken);
    }

    public async Task<IReadOnlySet<Guid>> FindSavedPostingIdsAsync(
        UserId userId,
        IReadOnlyCollection<JobPostingId> postingIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(postingIds);

        if (postingIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        // Whole identifiers inside the query; unwrap once the rows are back. `.Value` has no
        // column of its own, so EF cannot translate it and throws at query time.
        var keys = postingIds.ToList();

        // One query for a whole results page, so twenty cards render their bookmark state
        // without twenty round trips.
        var saved = await SavedJobs.AsNoTracking()
            .Where(entry => entry.UserId == userId && keys.Contains(entry.PostingId))
            .Select(entry => entry.PostingId)
            .ToListAsync(cancellationToken);

        return saved.Select(id => id.Value).ToHashSet();
    }

    public async Task<IReadOnlyList<SavedJob>> GetDueRemindersAsync(
        DateTimeOffset closingBefore, int limit, CancellationToken cancellationToken = default)
    {
        // Correlated against the posting's closing date: the reminder is about the posting, and
        // denormalising that date onto the save would make it wrong the moment an employer
        // extended the deadline.
        var closingSoon = _dbContext.Set<JobPosting>()
            .Where(posting =>
                posting.ClosingAt != null && posting.ClosingAt <= closingBefore)
            .Select(posting => posting.Id);

        return await SavedJobs
            .Where(saved =>
                saved.RemindBeforeClosing
                && saved.ReminderSentAt == null
                && closingSoon.Contains(saved.PostingId))
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(SavedJob savedJob, CancellationToken cancellationToken = default) =>
        await SavedJobs.AddAsync(savedJob, cancellationToken);

    public void Remove(SavedJob savedJob) => SavedJobs.Remove(savedJob);
}
