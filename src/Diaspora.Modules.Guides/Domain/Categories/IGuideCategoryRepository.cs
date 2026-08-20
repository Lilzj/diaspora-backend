using Diaspora.Modules.Guides.Domain.Identifiers;

namespace Diaspora.Modules.Guides.Domain.Categories;

/// <summary>Reads and writes the guide catalogue.</summary>
public interface IGuideCategoryRepository
{
    Task<GuideCategory?> GetAsync(
        GuideCategoryId id, CancellationToken cancellationToken = default);

    /// <summary>One category by its stable key — the lookup every guide write runs.</summary>
    Task<GuideCategory?> GetByKeyAsync(
        string key, CancellationToken cancellationToken = default);

    /// <summary>The whole catalogue, ordered. Small by design; never paged.</summary>
    Task<IReadOnlyList<GuideCategory>> ListAsync(
        bool includeInactive = false, CancellationToken cancellationToken = default);

    /// <summary>Whether a key is taken. Advisory — the unique index is authoritative.</summary>
    Task<bool> KeyExistsAsync(string key, CancellationToken cancellationToken = default);

    Task AddAsync(GuideCategory category, CancellationToken cancellationToken = default);
}
