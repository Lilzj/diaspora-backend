using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Domain.Guides;

/// <summary>
/// Reads and writes guides.
/// <para>
/// <b>Three shapes of read, and the split is load-bearing.</b> <see cref="GetAsync"/>
/// materialises the whole aggregate — sections, blocks, tags, references, notes — because a
/// command about to mutate it needs all of it, and a partially loaded aggregate cannot enforce
/// its own invariants. <see cref="GetForReadAsync"/> materialises the same graph without
/// tracking, for the detail screen. <see cref="QueryAsync"/> materialises the <em>card</em>
/// graph, because a discovery page of twenty cards that eagerly loaded twenty full documents
/// would be several thousand rows to render twenty titles.
/// </para>
/// </summary>
public interface IGuideRepository
{
    /// <summary>The full aggregate, tracked. For commands.</summary>
    Task<Guide?> GetAsync(GuideId id, CancellationToken cancellationToken = default);

    /// <summary>The full graph, untracked. Everything the detail screen renders.</summary>
    Task<Guide?> GetForReadAsync(GuideId id, CancellationToken cancellationToken = default);

    /// <summary>The full graph, by the deep-link handle.</summary>
    Task<Guide?> GetBySlugAsync(GuideSlug slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// The full graph, tracked, including a soft-deleted guide.
    /// <para>
    /// <b>The one read that must see past the global filter.</b> Every other read is right to
    /// hide deleted rows, but undelete has to load the very row deletion hid — without this the
    /// restore command could only ever answer "not found", which is the one answer that is
    /// certainly wrong for a guide the caller can see in their own bin.
    /// </para>
    /// <para>
    /// Deliberately the only member that ignores the filter for whole aggregates, so the places
    /// where deleted guides are reachable stay countable on one hand.
    /// </para>
    /// </summary>
    Task<Guide?> GetIncludingDeletedAsync(GuideId id, CancellationToken cancellationToken = default);

    /// <summary>The one query discovery, browse and "mine" all run. Card graph.</summary>
    Task<CursorPagedResult<Guide>> QueryAsync(
        GuideQueryFilter filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// How many guides match a filter, exactly.
    /// <para>
    /// <b>Added by B8.1, and it is the difference between a statistic and a guess.</b> The Knowledge
    /// Directory renders "Housing · 14 guides" on every row of every country page and "38 verified,
    /// 6 official" on every dashboard, and computing those by paging would report "at least twenty"
    /// for every busy category — a number that is technically true and useless to the editorial team
    /// it exists for. Counting through the same <see cref="GuideQueryFilter"/> the listing uses is
    /// what guarantees the count and the list can never disagree about what they are counting.
    /// </para>
    /// <para>
    /// Deliberately shares the filter rather than taking its own parameters. A second set of
    /// count-specific arguments would be a second implementation of visibility, and visibility
    /// computed twice is visibility computed differently.
    /// </para>
    /// </summary>
    Task<int> CountAsync(
        GuideQueryFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a slug is free. Advisory, not authoritative — the unique index is what actually
    /// guarantees it. Exists so the allocator can pick a free suffix before writing, rather
    /// than catching a constraint violation and retrying.
    /// </summary>
    Task<bool> SlugExistsAsync(GuideSlug slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// The guide holding Trust's verification identifier, tracked.
    /// <para>
    /// <b>How an outcome finds the claim that raised it.</b> Trust reviews people and its events
    /// name a member, but a member may have many guides and only one of them raised this case —
    /// so the correlation is the identifier B8 stored when it submitted, not the author. It also
    /// works for a claim this deployment never saw submitted: after a replay, or a decision that
    /// arrives across a deployment gap.
    /// </para>
    /// </summary>
    Task<Guide?> GetByVerificationIdAsync(
        Guid verificationId, CancellationToken cancellationToken = default);

    /// <summary>How many guides a member has authored. Enforces the per-author ceiling.</summary>
    Task<int> CountByAuthorAsync(UserId authorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// How many published guides apply to a country. Location's country page renders it.
    /// <para>
    /// A single aggregate rather than a page the caller counts: a headline number must not cost
    /// a hundred rows, which is the reason B4 separated counting from listing in the first place.
    /// </para>
    /// </summary>
    Task<int> CountInCountryAsync(string countryCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// How many published guides are scoped to an area, by materialised-path prefix.
    /// <para>
    /// Prefix-matched against the plain <c>AreaPath</c> column, which exists precisely so this
    /// query translates — a value-converted path reaching <c>StartsWith</c> is B4's recorded
    /// production failure.
    /// </para>
    /// </summary>
    Task<int> CountInAreaAsync(string areaPathPrefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// Drafts whose scheduled publication instant has passed. The scheduled publisher's query —
    /// batch-bounded so a backlog after downtime is worked through rather than swallowed whole.
    /// </summary>
    Task<IReadOnlyList<Guide>> GetScheduledDueAsync(
        DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default);

    Task AddAsync(Guide guide, CancellationToken cancellationToken = default);

    void Remove(Guide guide);
}
