using Diaspora.Modules.Guides.Domain.Categories;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Services;

/// <summary>
/// Resolves category keys against the catalogue, with a per-request cache.
/// <para>
/// The aggregate stores the key and validates its <em>shape</em>; whether the key names a real,
/// active category is a catalogue question, and this is the one place it is asked. Scoped, so a
/// request that touches the same category five times — validate, map, list — costs one query.
/// </para>
/// </summary>
public sealed class GuideCategoryResolver
{
    private readonly IGuideCategoryRepository _categories;
    private readonly Dictionary<string, GuideCategory?> _cache = new(StringComparer.Ordinal);

    public GuideCategoryResolver(IGuideCategoryRepository categories) => _categories = categories;

    /// <summary>One category by key, or null. Cached for the request.</summary>
    public async Task<GuideCategory?> FindAsync(
        string? key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var normalized = key.Trim().ToLowerInvariant();

        if (_cache.TryGetValue(normalized, out var cached))
        {
            return cached;
        }

        var category = await _categories.GetByKeyAsync(normalized, cancellationToken);
        _cache[normalized] = category;

        return category;
    }

    /// <summary>
    /// The category a guide may be filed under: it exists and it is active.
    /// <para>
    /// Inactive is a distinct failure from unknown, because the fix differs — an unknown key is
    /// a client bug, an inactive one is a catalogue decision the author can be told about.
    /// </para>
    /// </summary>
    public async Task<Result<GuideCategory>> ResolveActiveAsync(
        string? key, CancellationToken cancellationToken = default)
    {
        var category = await FindAsync(key, cancellationToken);

        if (category is null)
        {
            return Result.Failure<GuideCategory>(GuideErrors.CategoryNotFound);
        }

        return category.IsActive
            ? Result.Success(category)
            : Result.Failure<GuideCategory>(GuideErrors.CategoryInactive);
    }

    /// <summary>The whole catalogue, ordered. Small by design.</summary>
    public Task<IReadOnlyList<GuideCategory>> ListAsync(
        bool includeInactive = false, CancellationToken cancellationToken = default) =>
        _categories.ListAsync(includeInactive, cancellationToken);
}
