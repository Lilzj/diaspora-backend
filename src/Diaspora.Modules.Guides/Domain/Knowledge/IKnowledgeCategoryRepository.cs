using Diaspora.Modules.Guides.Domain.Identifiers;

namespace Diaspora.Modules.Guides.Domain.Knowledge;

/// <summary>
/// Reads and writes the Knowledge Directory's navigation catalogue.
/// <para>
/// Small by design and never paged: the catalogue is what a country page renders as a menu, and a
/// menu that needs pagination has stopped being one. Every read loads the whole graph —
/// localizations, sources, provider categories — because every caller renders all of it.
/// </para>
/// </summary>
public interface IKnowledgeCategoryRepository
{
    Task<KnowledgeCategory?> GetAsync(
        KnowledgeCategoryId id, CancellationToken cancellationToken = default);

    /// <summary>One category by its stable key — the lookup every directory arrangement runs.</summary>
    Task<KnowledgeCategory?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// One category by key, archived ones included.
    /// <para>
    /// <b>The one read that sees past the global query filter, and the reason it has to exist.</b>
    /// Archiving is a soft delete, so the filter hides the row — and a restore command needs to
    /// load exactly the row the archiving hid. Without this member, restore could only ever answer
    /// "not found", which is the one answer that is certainly wrong for a category the curator can
    /// see in their own archive list.
    /// </para>
    /// </summary>
    Task<KnowledgeCategory?> GetIncludingArchivedAsync(
        string key, CancellationToken cancellationToken = default);

    /// <summary>The whole catalogue, ordered. Never paged.</summary>
    Task<IReadOnlyList<KnowledgeCategory>> ListAsync(
        bool includeArchived = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Several categories by key, in one round trip.
    /// <para>
    /// The N+1 guard for rendering a country directory: an entry list of twenty keys must not be
    /// twenty queries, and every dashboard, category view and learning path resolves a set of keys
    /// exactly once through here.
    /// </para>
    /// </summary>
    Task<IReadOnlyDictionary<string, KnowledgeCategory>> GetByKeysAsync(
        IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default);

    /// <summary>Whether a key is taken, archived rows included. Advisory — the unique index rules.</summary>
    Task<bool> KeyExistsAsync(string key, CancellationToken cancellationToken = default);

    Task AddAsync(KnowledgeCategory category, CancellationToken cancellationToken = default);

    /// <summary>Archives, through the platform's soft-delete path. Never a hard delete.</summary>
    void Remove(KnowledgeCategory category);
}
