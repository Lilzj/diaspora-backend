using Diaspora.Modules.Guides.Domain.Identifiers;

namespace Diaspora.Modules.Guides.Domain.Intelligence;

/// <summary>
/// How the application layer reaches the notice category catalogue.
/// <para>
/// Every read loads the whole graph, and that is affordable because the catalogue is small by
/// design — twenty-odd rows, each with a handful of translations. The alternative, a lean read for
/// listings and a full one for editing, would save a join on a query that runs once per country page
/// and is cached, and would introduce the class of bug where a caller operates on a category whose
/// translations silently did not load.
/// </para>
/// </summary>
public interface INoticeCategoryRepository
{
    Task<NoticeCategory?> GetAsync(
        NoticeCategoryId id, CancellationToken cancellationToken = default);

    Task<NoticeCategory?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="IOfficialNoticeRepository.GetIncludingArchivedAsync" />
    Task<NoticeCategory?> GetIncludingArchivedAsync(
        string key, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NoticeCategory>> ListAsync(
        bool includeArchived = false, CancellationToken cancellationToken = default);

    /// <summary>Several categories by key, in one round trip. The N+1 guard every composed read uses.</summary>
    Task<IReadOnlyDictionary<string, NoticeCategory>> GetByKeysAsync(
        IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a key is taken, archived rows included.
    /// <para>
    /// Archived rows count: the unique index would refuse the insert anyway, and reusing the key
    /// would make restoring the archived row impossible.
    /// </para>
    /// </summary>
    Task<bool> KeyExistsAsync(string key, CancellationToken cancellationToken = default);

    Task AddAsync(NoticeCategory category, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="IOfficialNoticeRepository.Remove" />
    void Remove(NoticeCategory category);
}
