using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Knowledge.Mapping;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Extensibility.Caching;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Knowledge.Categories;

/// <summary>
/// Serves the navigation catalogue.
/// <para>
/// <b>Cached, and the curator's view deliberately is not.</b> The reader's catalogue is the same
/// answer for everybody in one locale and changes when somebody edits it — the ideal thing to hold.
/// A curator's view includes archived rows and is read immediately after a write, where a cache
/// would show them their own edit not having happened.
/// </para>
/// </summary>
public sealed class ListKnowledgeCategoriesQueryHandler
    : IQueryHandler<ListKnowledgeCategoriesQuery, IReadOnlyList<KnowledgeCategoryResponse>>
{
    private readonly IKnowledgeCategoryRepository _categories;
    private readonly KnowledgeAuthorizationService _authorization;
    private readonly IKnowledgeCache _cache;

    public ListKnowledgeCategoriesQueryHandler(
        IKnowledgeCategoryRepository categories,
        KnowledgeAuthorizationService authorization,
        IKnowledgeCache cache)
    {
        _categories = categories;
        _authorization = authorization;
        _cache = cache;
    }

    public async Task<Result<IReadOnlyList<KnowledgeCategoryResponse>>> Handle(
        ListKnowledgeCategoriesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // A reader asking for archived rows gets the active ones. The parameter is a curator's
        // tool; refusing the whole catalogue read over it would break a page for a client bug.
        var includeArchived = query.IncludeArchived && _authorization.IsCurator;

        if (!includeArchived)
        {
            var key = KnowledgeCacheKeys.Catalogue(includeArchived: false, query.Locale);

            var cached = await _cache.GetAsync<CachedCatalogue>(key, cancellationToken);

            if (cached is not null)
            {
                return Result.Success(cached.Categories);
            }

            var fresh = await LoadAsync(includeArchived: false, query.Locale, cancellationToken);

            await _cache.SetAsync(key, new CachedCatalogue(fresh), cancellationToken: cancellationToken);

            return Result.Success(fresh);
        }

        return Result.Success(
            await LoadAsync(includeArchived: true, query.Locale, cancellationToken));
    }

    private async Task<IReadOnlyList<KnowledgeCategoryResponse>> LoadAsync(
        bool includeArchived, string? locale, CancellationToken cancellationToken)
    {
        var categories = await _categories.ListAsync(includeArchived, cancellationToken);

        return includeArchived
            ? [.. categories.Select(KnowledgeCategoryMapper.ToCuratorResponse)]
            : [.. categories.Select(category => KnowledgeCategoryMapper.ToResponse(category, locale))];
    }

    /// <summary>
    /// A wrapper, because the cache stores reference types and a bare list is awkward to constrain.
    /// Private because nothing outside this handler has any business holding one.
    /// </summary>
    private sealed record CachedCatalogue(IReadOnlyList<KnowledgeCategoryResponse> Categories);
}
