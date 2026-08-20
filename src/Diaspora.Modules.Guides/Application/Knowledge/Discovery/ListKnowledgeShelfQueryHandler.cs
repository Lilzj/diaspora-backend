using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Application.Mapping;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Directories;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Knowledge.Discovery;

/// <summary>
/// Serves one paged shelf.
/// <para>
/// <b>Featured is the shelf that is not a query, and it is why this handler branches.</b> Every
/// other shelf is an observation the database can order; featured is a list a person arranged, and
/// re-querying it to "check" the guides would reorder them by whatever the query ordered by —
/// silently discarding the curation while returning exactly the right set.
/// </para>
/// <para>
/// A navigation row key is expanded into the filing categories it draws from before the query runs.
/// That expansion is the whole reason a client can ask for "housing in Mauritius" without knowing
/// that "housing" is a navigation concept and the guides are filed under something else.
/// </para>
/// </summary>
public sealed class ListKnowledgeShelfQueryHandler
    : IQueryHandler<ListKnowledgeShelfQuery, CursorPagedResult<GuideCardResponse>>
{
    private readonly KnowledgeGuideFinder _finder;
    private readonly KnowledgeCardResolver _cards;
    private readonly IKnowledgeCategoryRepository _categories;
    private readonly ICountryKnowledgeDirectoryRepository _directories;

    public ListKnowledgeShelfQueryHandler(
        KnowledgeGuideFinder finder,
        KnowledgeCardResolver cards,
        IKnowledgeCategoryRepository categories,
        ICountryKnowledgeDirectoryRepository directories)
    {
        _finder = finder;
        _cards = cards;
        _categories = categories;
        _directories = directories;
    }

    public async Task<Result<CursorPagedResult<GuideCardResponse>>> Handle(
        ListKnowledgeShelfQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var request = query.Request;

        var limit = Math.Clamp(
            request.Limit ?? KnowledgeLimits.DefaultPageSize, 1, KnowledgeLimits.MaxPageSize);

        if (query.Shelf == KnowledgeShelf.Featured)
        {
            return Result.Success(await FeaturedAsync(request, limit, cancellationToken));
        }

        var sourceKeys = await SourceKeysAsync(request.CategoryKey, cancellationToken);

        var filter = Narrow(
            _finder.Filter(
                request.CountryCode, sourceKeys, request.Language, SortOf(query.Shelf), limit),
            query.Shelf);

        var page = await _finder.PageAsync(filter, request.Cursor, cancellationToken);

        return Result.Success(CursorPagedResult<GuideCardResponse>.Create(
            [.. page.Items.Select(GuideCardMapper.ToCard)],
            page.NextCursor,
            page.HasMore));
    }

    /// <summary>
    /// The curated shelf, in curated order.
    /// <para>
    /// Not paged with a cursor, because it cannot be: the ordering is a list a person wrote and
    /// there is no column to resume from. It is bounded instead — a curated shelf that needed
    /// pagination would be a curation problem rather than a paging one — so the response reports no
    /// further pages, which is true.
    /// </para>
    /// </summary>
    private async Task<CursorPagedResult<GuideCardResponse>> FeaturedAsync(
        Contracts.V1.Requests.BrowseKnowledgeRequest request,
        int limit,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CountryCode))
        {
            // Featuring is country-scoped by definition. A global featured shelf is B8's flag, and
            // asking this endpoint for one is a question with no answer rather than an error.
            return CursorPagedResult<GuideCardResponse>.Empty();
        }

        var directory = await _directories.GetForReadAsync(
            request.CountryCode, cancellationToken);

        if (directory is null || !directory.IsPublished)
        {
            return CursorPagedResult<GuideCardResponse>.Empty();
        }

        var featured = directory.FeaturedFor(request.CategoryKey).Take(limit).ToList();

        var cards = await _cards.ResolveAsync(featured, cancellationToken);

        return CursorPagedResult<GuideCardResponse>.Create(
            KnowledgeCardResolver.Order(featured, cards), nextCursor: null, hasMore: false);
    }

    /// <summary>
    /// Expands a navigation row key into the filing categories it draws from.
    /// <para>
    /// An unknown key yields no sources rather than an error, which makes the shelf empty rather
    /// than broken. A client following a stale link deserves an empty page, not a failure.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyCollection<string>?> SourceKeysAsync(
        string? categoryKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(categoryKey))
        {
            return null;
        }

        var category = await _categories.GetByKeyAsync(categoryKey, cancellationToken);

        return category?.EffectiveSourceKeys
            ?? [KnowledgeNormalization.Key(categoryKey) ?? categoryKey];
    }

    private static GuideSortOrder SortOf(KnowledgeShelf shelf) => shelf switch
    {
        KnowledgeShelf.Popular => GuideSortOrder.Popular,
        KnowledgeShelf.RecentlyUpdated => GuideSortOrder.RecentlyUpdated,
        KnowledgeShelf.Community => GuideSortOrder.Newest,
        _ => GuideSortOrder.MostHelpful
    };

    private static GuideQueryFilter Narrow(GuideQueryFilter filter, KnowledgeShelf shelf) =>
        shelf switch
        {
            KnowledgeShelf.Beginner => filter with { Difficulty = GuideDifficulty.Beginner },
            KnowledgeShelf.Advanced => filter with { Difficulty = GuideDifficulty.Advanced },
            KnowledgeShelf.Official => filter with { OfficialOnly = true },
            KnowledgeShelf.Verified => filter with { VerifiedOnly = true },
            _ => filter
        };
}
