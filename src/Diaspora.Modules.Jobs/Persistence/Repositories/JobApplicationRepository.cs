using Diaspora.Modules.Jobs.Domain.Applications;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Jobs.Persistence.Repositories;

/// <summary>
/// EF Core repository for applications.
/// <para>
/// <b>The listing read deliberately does not load the history.</b> An applicant list of twenty
/// rows showing a name, a status and a date must not drag several hundred history entries with
/// it — and the history is unbounded in a way the other owned collections are not. The detail
/// read does load it, because the timeline is the reason anyone opens one.
/// </para>
/// <para>
/// <b><see cref="QueryAsync"/> refuses an unscoped query.</b> A listing scoped to neither an
/// employer nor an applicant returns other people's job applications, and that is not a
/// mistake worth leaving to callers to avoid.
/// </para>
/// </summary>
public sealed class JobApplicationRepository : IJobApplicationRepository
{
    private readonly DiasporaDbContext _dbContext;

    public JobApplicationRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<JobApplication> Applications => _dbContext.Set<JobApplication>();

    public async Task<JobApplication?> GetAsync(
        JobApplicationId id, CancellationToken cancellationToken = default) =>
        await FullGraph(Applications)
            .FirstOrDefaultAsync(application => application.Id == id, cancellationToken);

    public async Task<JobApplication?> GetForReadAsync(
        JobApplicationId id, CancellationToken cancellationToken = default) =>
        await FullGraph(Applications.AsNoTracking())
            .FirstOrDefaultAsync(application => application.Id == id, cancellationToken);

    /// <summary>
    /// One column. No graph, no history, no tracking — see the interface for why that matters
    /// here and nowhere else.
    /// </summary>
    public async Task<UserId?> FindApplicantUserIdAsync(
        JobApplicationId id, CancellationToken cancellationToken = default) =>
        await Applications
            .AsNoTracking()
            .Where(application => application.Id == id)
            .Select(application => application.ApplicantUserId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<JobApplication?> GetByCandidateAndPostingAsync(
        CandidateProfileId candidateId,
        JobPostingId postingId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidateId);
        ArgumentNullException.ThrowIfNull(postingId);

        return await Applications
            .FirstOrDefaultAsync(
                application => application.CandidateId == candidateId
                    && application.PostingId == postingId,
                cancellationToken);
    }

    public async Task<CursorPagedResult<JobApplication>> QueryAsync(
        ApplicationQueryFilter filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        // An unscoped application listing returns other people's job applications.
        if (!filter.IsStaff
            && filter.EmployerId is null
            && filter.ApplicantUserId is null
            && filter.CandidateId is null)
        {
            return CursorPagedResult<JobApplication>.Empty();
        }

        var query = ListGraph(Apply(Applications.AsNoTracking(), filter));

        var parsed = JobCursor.Parse(cursor);

        if (parsed?.AsTimestamp() is { } after)
        {
            // The boundary is a timestamp *and* the identifiers already returned at it.
            //
            // A strict comparison on the timestamp alone drops every row sharing the boundary
            // value, and applications tie readily — a bulk import, a backfill, or simply two
            // submissions inside one clock tick, which on Windows is about fifteen
            // milliseconds. On a thousand applicants paged fifty at a time that silently loses
            // roughly one applicant in fifty from the recruiter's list, and the person who is
            // never seen has no way of knowing.
            //
            // The obvious keyset — `SubmittedAt = @after AND Id > @lastId` — cannot be written
            // here: the identifier is a value-converted strongly-typed id, and no provider
            // translates an ordering comparison on one. Excluding the identifiers already seen
            // does translate, and gives the same result. See CursorBoundary.
            var tie = CursorBoundary.Exclusion(parsed.LastKey, JobApplicationId.From);
            var seen = tie.Ids;
            var exhausted = tie.IsExhausted;

            query = filter.OldestFirst
                ? query.Where(application =>
                    application.SubmittedAt > after
                    || (application.SubmittedAt == after
                        && !exhausted && !seen.Contains(application.Id)))
                : query.Where(application =>
                    application.SubmittedAt < after
                    || (application.SubmittedAt == after
                        && !exhausted && !seen.Contains(application.Id)));
        }

        // The identifier as a secondary sort so a tie group comes back in a stable order rather
        // than whatever the query plan happened to produce. Ordering on a value-converted id is
        // an ORDER BY on its column and translates; comparing one does not, which is why the
        // boundary above excludes by identity instead.
        query = filter.OldestFirst
            ? query.OrderBy(application => application.SubmittedAt)
                .ThenBy(application => application.Id)
            : query.OrderByDescending(application => application.SubmittedAt)
                .ThenByDescending(application => application.Id);

        var effectiveLimit = Math.Clamp(limit, 1, JobLimits.MaxPageSize);
        var rows = await query.Take(effectiveLimit + 1).ToListAsync(cancellationToken);

        var hasMore = rows.Count > effectiveLimit;

        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var next = hasMore && rows.Count > 0
            ? JobCursor.FromTimestamp(
                rows[^1].SubmittedAt,
                CursorBoundary.Key(
                    rows,
                    row => row.SubmittedAt,
                    row => row.Id.Value,
                    parsed?.LastKey,
                    parsed?.AsTimestamp())).Encode()
            : null;

        return CursorPagedResult<JobApplication>.Create(rows, next, hasMore);
    }

    public async Task<int> CountAsync(
        ApplicationQueryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        return await Apply(Applications.AsNoTracking(), filter).CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<ApplicationStatus, int>> CountByStatusAsync(
        JobPostingId postingId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(postingId);

        // One round trip for the whole pipeline view, rather than one per column.
        var counts = await Applications.AsNoTracking()
            .Where(application => application.PostingId == postingId)
            .GroupBy(application => application.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(entry => entry.Status, entry => entry.Count);
    }

    public async Task<IReadOnlyDictionary<ApplicationStatus, int>> CountByStatusForEmployerAsync(
        EmployerProfileId employerId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(employerId);

        var counts = await Applications.AsNoTracking()
            .Where(application => application.EmployerId == employerId)
            .GroupBy(application => application.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(entry => entry.Status, entry => entry.Count);
    }

    public async Task<IReadOnlyDictionary<string, int>> CountByStageAsync(
        JobPostingId postingId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(postingId);

        var counts = await Applications.AsNoTracking()
            .Where(application =>
                application.PostingId == postingId && application.StageKey != null)
            .GroupBy(application => application.StageKey!)
            .Select(group => new { Stage = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(entry => entry.Stage, entry => entry.Count, StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<JobApplication>> GetStaleUnansweredAsync(
        DateTimeOffset submittedBefore, int limit, CancellationToken cancellationToken = default) =>
        await Applications
            .Where(application =>
                application.FirstRespondedAt == null
                && application.SubmittedAt < submittedBefore
                && application.Status == ApplicationStatus.Submitted)
            .OrderBy(application => application.SubmittedAt)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<JobApplication>> GetActiveForPostingAsync(
        JobPostingId postingId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(postingId);

        var terminal = new[]
        {
            ApplicationStatus.Accepted,
            ApplicationStatus.Rejected,
            ApplicationStatus.Withdrawn
        };

        return await Applications
            .Where(application =>
                application.PostingId == postingId
                && !terminal.Contains(application.Status))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountByApplicantSinceAsync(
        UserId applicantUserId, DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applicantUserId);

        return await Applications.AsNoTracking()
            .CountAsync(
                application => application.ApplicantUserId == applicantUserId
                    && application.SubmittedAt >= since,
                cancellationToken);
    }

    public async Task<IReadOnlySet<Guid>> FindAppliedPostingIdsAsync(
        UserId applicantUserId,
        IReadOnlyCollection<JobPostingId> postingIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applicantUserId);
        ArgumentNullException.ThrowIfNull(postingIds);

        if (postingIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        // Whole identifiers on both sides. Reaching through to `.Value` inside the query asks EF
        // for a member with no column of its own — the converter maps the identifier as a unit —
        // and it fails at query time, not compile time. Unwrap after the results come back.
        var keys = postingIds.ToList();

        // One query for a whole results page, so a card can render "you applied" without a
        // round trip each.
        var applied = await Applications.AsNoTracking()
            .Where(application =>
                application.ApplicantUserId == applicantUserId
                && keys.Contains(application.PostingId))
            .Select(application => application.PostingId)
            .ToListAsync(cancellationToken);

        return applied.Select(id => id.Value).ToHashSet();
    }

    public async Task AddAsync(
        JobApplication application, CancellationToken cancellationToken = default) =>
        await Applications.AddAsync(application, cancellationToken);

    public void Remove(JobApplication application) => Applications.Remove(application);

    /// <summary>Everything, history included. For a command and for the detail screen.</summary>
    private static IQueryable<JobApplication> FullGraph(IQueryable<JobApplication> query) =>
        query
            .Include(application => application.Answers)
            .Include(application => application.Attachments)
            .Include(application => application.History);

    /// <summary>
    /// What a list row renders. No history — it is the one owned collection here that grows
    /// without a small bound, and no listing shows it.
    /// </summary>
    private static IQueryable<JobApplication> ListGraph(IQueryable<JobApplication> query) =>
        query.Include(application => application.Attachments);

    private static IQueryable<JobApplication> Apply(
        IQueryable<JobApplication> query, ApplicationQueryFilter filter)
    {
        if (filter.EmployerId is { } employerId)
        {
            query = query.Where(application => application.EmployerId == employerId);
        }

        if (filter.ApplicantUserId is { } applicantUserId)
        {
            query = query.Where(application => application.ApplicantUserId == applicantUserId);
        }

        if (filter.CandidateId is { } candidateId)
        {
            query = query.Where(application => application.CandidateId == candidateId);
        }

        if (filter.PostingId is { } postingId)
        {
            query = query.Where(application => application.PostingId == postingId);
        }

        if (filter.Statuses is { Count: > 0 })
        {
            var statuses = filter.Statuses.ToList();
            query = query.Where(application => statuses.Contains(application.Status));
        }

        if (filter.ActiveOnly)
        {
            var terminal = new[]
            {
                ApplicationStatus.Accepted,
                ApplicationStatus.Rejected,
                ApplicationStatus.Withdrawn
            };

            query = query.Where(application => !terminal.Contains(application.Status));
        }

        if (!string.IsNullOrWhiteSpace(filter.StageKey))
        {
            var stage = filter.StageKey.Trim().ToLowerInvariant();
            query = query.Where(application => application.StageKey == stage);
        }

        if (filter.UnviewedOnly)
        {
            query = query.Where(application => application.FirstViewedAt == null);
        }

        if (filter.UnansweredBefore is { } unansweredBefore)
        {
            query = query.Where(application =>
                application.FirstRespondedAt == null
                && application.SubmittedAt < unansweredBefore);
        }

        if (filter.SubmittedAfter is { } submittedAfter)
        {
            query = query.Where(application => application.SubmittedAt > submittedAfter);
        }

        return query;
    }
}
