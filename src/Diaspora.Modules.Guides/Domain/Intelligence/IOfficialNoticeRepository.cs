using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Guides.Domain.Intelligence;

/// <summary>
/// How the application layer reaches official notices.
/// <para>
/// <b>Every listing goes through <see cref="ListAsync"/> and one <see cref="NoticeQueryFilter"/>.</b>
/// The alternative — a method per screen — is how a platform ends up with six definitions of what a
/// reader can see, five of which are right. The filter is also what a contributed search engine hands
/// back, so an external index and the shipped one are subject to identical visibility rules.
/// </para>
/// <para>
/// <b>Counting shares that filter rather than materialising a page.</b> A badge that says "12" and a
/// list that shows eleven is the defect B8.1 records; one filter used by both makes it impossible.
/// </para>
/// </summary>
public interface IOfficialNoticeRepository
{
    Task<OfficialNotice?> GetAsync(
        OfficialNoticeId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The one read that sees past the global query filter.
    /// <para>
    /// Archiving is a soft delete, so the filter hides the row — and a restore has to load exactly
    /// the row the archiving hid. Without this, restore could only ever answer "not found", which is
    /// the one answer certainly wrong for a notice the editor can see in their own archive list.
    /// </para>
    /// </summary>
    Task<OfficialNotice?> GetIncludingArchivedAsync(
        OfficialNoticeId id, CancellationToken cancellationToken = default);

    /// <summary>One page, ordered and filtered by the filter and nothing else.</summary>
    Task<CursorPagedResult<OfficialNotice>> ListAsync(
        NoticeQueryFilter filter, string? cursor, CancellationToken cancellationToken = default);

    /// <summary>
    /// The first <paramref name="limit"/> rows matching the filter, without a cursor.
    /// <para>
    /// For dashboard bands, which want five rows and never a second page. Going through the paged
    /// member would make every band mint a cursor nobody follows.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<OfficialNotice>> TakeAsync(
        NoticeQueryFilter filter, int limit, CancellationToken cancellationToken = default);

    /// <summary>An exact count under the same filter the listing pages with.</summary>
    Task<int> CountAsync(NoticeQueryFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Several notices by identifier, in one round trip. The N+1 guard.</summary>
    Task<IReadOnlyList<OfficialNotice>> GetByIdsAsync(
        IReadOnlyCollection<OfficialNoticeId> ids, CancellationToken cancellationToken = default);

    /// <summary>Whether any notice at all is filed under a category. Guards its archival.</summary>
    Task<bool> AnyForCategoryAsync(string categoryKey, CancellationToken cancellationToken = default);

    /// <summary>Whether any published notice names an authority. Guards its archival.</summary>
    Task<bool> AnyPublishedForAuthorityAsync(
        IssuingAuthorityId authorityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Published notices whose expiry has passed and that the sweep has not yet announced.
    /// <para>
    /// Bounded by <paramref name="limit"/> so one pass over a neglected deployment cannot load the
    /// whole archive into memory, and so the sweep's cost is predictable however far behind it is.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<OfficialNotice>> ListExpiredUnannouncedAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default);

    /// <summary>How many notices sit under each category key, for the whole platform or one country.</summary>
    Task<IReadOnlyDictionary<string, int>> CountByCategoryAsync(
        string? countryCode, DateTimeOffset asOf, CancellationToken cancellationToken = default);

    Task AddAsync(OfficialNotice notice, CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives, through the platform's soft-delete path.
    /// <para>
    /// The handler never stamps the archival itself. It marks the aggregate for removal and saves;
    /// the shared context converts that into a soft delete, sets the timestamp and the actor, and
    /// leaves the row where every audit and every restore can find it. Stamping it by hand is the
    /// defect B1.0.1 records, and it is silent.
    /// </para>
    /// </summary>
    void Remove(OfficialNotice notice);
}
