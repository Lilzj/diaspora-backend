using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.ValueObjects;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Jobs.Domain.Postings;

/// <summary>
/// Reads and writes job postings.
/// <para>
/// <b>Three shapes of read, and the split is load-bearing.</b> <see cref="GetAsync"/>
/// materialises the whole aggregate — lines, skills, tags, references, questions, notes —
/// because a command about to mutate it needs all of it, and a partially loaded aggregate
/// cannot enforce its own invariants. <see cref="GetForReadAsync"/> materialises the same graph
/// untracked, for the detail screen. <see cref="QueryAsync"/> materialises the <em>card</em>
/// graph, because a results page of twenty postings that eagerly loaded twenty full adverts
/// would be several thousand rows to render twenty titles.
/// </para>
/// </summary>
public interface IJobPostingRepository
{
    /// <summary>The full aggregate, tracked. For commands.</summary>
    Task<JobPosting?> GetAsync(JobPostingId id, CancellationToken cancellationToken = default);

    /// <summary>The full graph, untracked. Everything the detail screen renders.</summary>
    Task<JobPosting?> GetForReadAsync(
        JobPostingId id, CancellationToken cancellationToken = default);

    /// <summary>The full graph, by the deep-link handle.</summary>
    Task<JobPosting?> GetBySlugAsync(JobSlug slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// The full graph, tracked, including a soft-deleted posting.
    /// <para>
    /// <b>The one read that must see past the global filter.</b> Every other read is right to
    /// hide deleted rows, but undelete has to load the very row deletion hid — without this the
    /// restore command could only ever answer "not found", which is certainly wrong for a
    /// posting the employer can see in their own bin. Deliberately the only member that ignores
    /// the filter, so the places deleted postings are reachable stay countable on one hand.
    /// </para>
    /// </summary>
    Task<JobPosting?> GetIncludingDeletedAsync(
        JobPostingId id, CancellationToken cancellationToken = default);

    /// <summary>Several postings at once, card graph, untracked. Hydrates search hits.</summary>
    Task<IReadOnlyList<JobPosting>> GetManyAsync(
        IReadOnlyCollection<JobPostingId> ids, CancellationToken cancellationToken = default);

    /// <summary>The one query search, browse, nearby and every dashboard run. Card graph.</summary>
    Task<CursorPagedResult<JobPosting>> QueryAsync(
        JobQueryFilter filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>How many postings match, for a facet count or a dashboard tile.</summary>
    Task<int> CountAsync(JobQueryFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a slug is free. Advisory, not authoritative — the unique index is what actually
    /// guarantees it. Exists so the allocator can pick a free suffix before writing, rather than
    /// catching a constraint violation and retrying.
    /// </summary>
    Task<bool> SlugExistsAsync(JobSlug slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// The posting holding Trust's verification identifier, tracked.
    /// <para>
    /// How an outcome finds the claim that raised it. Trust's events name a member, but a member
    /// may recruit for several employers with many postings each, so the correlation is the case
    /// identifier B9 stored when it submitted. It also works for a claim this deployment never
    /// saw submitted: after a replay, or across a deployment gap.
    /// </para>
    /// </summary>
    Task<JobPosting?> GetByVerificationIdAsync(
        Guid verificationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Postings whose closing date has passed but whose status has not caught up. The expiry
    /// sweep's work list, bounded so one pass cannot load the whole table.
    /// </summary>
    Task<IReadOnlyList<JobPosting>> GetExpiredAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Postings published since an instant, for the alert sweep. Discoverable postings only —
    /// an alert must never surface a draft or an unlisted role.
    /// </summary>
    Task<IReadOnlyList<JobPosting>> GetPublishedSinceAsync(
        DateTimeOffset since, int limit, CancellationToken cancellationToken = default);

    /// <summary>How many postings an employer currently has open. Enforces the posting ceiling.</summary>
    Task<int> CountOpenByEmployerAsync(
        EmployerProfileId employerId, CancellationToken cancellationToken = default);

    Task AddAsync(JobPosting posting, CancellationToken cancellationToken = default);

    void Remove(JobPosting posting);
}
