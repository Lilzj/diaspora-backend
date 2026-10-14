using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Extensibility.Ranking;
using Diaspora.Modules.Guides.Extensibility.Search;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Discovery;

/// <summary>
/// Serves one page of discovery.
/// <para>
/// The shape is: translate the request into the search seam's question, ask the engine, hydrate
/// the hits from the write model, re-apply visibility, rank, map. Every step delegates the thing
/// it should — relevance to the registered engine, ordering to a contributed strategy — and the
/// handler's own job is to sequence them.
/// </para>
/// <para>
/// <b>Visibility is re-applied after the engine answers, and that is the whole safety story of
/// the search seam.</b> An engine indexes asynchronously and may be stale or over-broad; because
/// the page is hydrated from the write model and filtered through
/// <see cref="GuideAuthorizationService.CanSee"/>, an over-returning engine costs a wasted row
/// rather than a leaked draft.
/// </para>
/// <para>
/// <b>Ranking happens after the query, over one page.</b> Only for
/// <see cref="GuideSortOrder.Trending"/>: the engine already ordered the other sorts correctly
/// and correctly <em>across</em> pages, and re-sorting those in memory would scramble the
/// cursor's contract — page two would not continue page one.
/// </para>
/// <para>
/// <b>The read records nothing.</b> A search impression is an analytics fact and belongs to a
/// command; a query that wrote one would count every crawler and every cache warm-up as
/// readership, and would make a browse screen fail when the analytics row is contended.
/// </para>
/// </summary>
public sealed class SearchGuidesQueryHandler
    : IQueryHandler<SearchGuidesQuery, CursorPagedResult<GuideCardResponse>>
{
    private readonly IGuideSearchEngine _search;
    private readonly IGuideRepository _guides;
    private readonly GuideAuthorizationService _authorization;
    private readonly GuideRankingStrategyRegistry _rankings;
    private readonly IClock _clock;
    private readonly GuideAuthorNames _authorNames;

    public SearchGuidesQueryHandler(
        IGuideSearchEngine search,
        IGuideRepository guides,
        GuideAuthorizationService authorization,
        GuideRankingStrategyRegistry rankings,
        IClock clock,
        GuideAuthorNames authorNames)
    {
        _search = search;
        _guides = guides;
        _authorization = authorization;
        _rankings = rankings;
        _clock = clock;
        _authorNames = authorNames;
    }

    public async Task<Result<CursorPagedResult<GuideCardResponse>>> Handle(
        SearchGuidesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var request = query.Request;
        var sort = GuideWireNames.ParseSort(request.Sort);
        var limit = Math.Clamp(request.Limit ?? GuideLimits.DefaultPageSize, 1, GuideLimits.MaxPageSize);

        // Both spellings of the term mean the same thing; first non-empty wins (the B4
        // precedent the request record documents).
        var text = string.IsNullOrWhiteSpace(request.Query) ? request.Q : request.Query;

        var outcome = await _search.SearchAsync(
            new GuideSearchRequest
            {
                Text = string.IsNullOrWhiteSpace(text) ? null : text.Trim(),
                CountryCode = Normalize(request.Country)?.ToUpperInvariant(),
                CategoryKey = Normalize(request.Category)?.ToLowerInvariant(),
                Tag = Normalize(request.Tag)?.ToLowerInvariant(),
                Language = Normalize(request.Language)?.ToLowerInvariant(),
                AuthorId = request.AuthorId,
                VerifiedOnly = request.Verified ?? false,
                FeaturedOnly = request.Featured ?? false,
                Sort = sort,
                Cursor = request.Cursor,
                Limit = limit
            },
            cancellationToken);

        var candidates = await HydrateAsync(outcome, cancellationToken);

        var ranked = Rank(candidates, sort, text);

        return Result.Success(CursorPagedResult<GuideCardResponse>.Create(
            await _authorNames.ToCardsAsync(ranked, cancellationToken),
            outcome.NextCursor,
            outcome.HasMore,
            outcome.Total is { } total ? (int)Math.Min(total, int.MaxValue) : null));
    }

    /// <summary>
    /// Turns the engine's identifiers back into guides, in the engine's order.
    /// <para>
    /// One read per hit, because the repository publishes no batched by-identifier read and this
    /// module does not reach around it into persistence — the same bounded compromise
    /// <c>GuideDirectoryService</c> documents, with exactly one call site to improve when
    /// persistence grows a batched method. The page is at most fifty rows by construction.
    /// </para>
    /// </summary>
    private async Task<List<Guide>> HydrateAsync(
        GuideSearchOutcome outcome, CancellationToken cancellationToken)
    {
        var guides = new List<Guide>(outcome.Hits.Count);

        foreach (var hit in outcome.Hits)
        {
            var guide = await _guides.GetForReadAsync(GuideId.From(hit.GuideId), cancellationToken);

            if (guide is not null && _authorization.CanSee(guide))
            {
                guides.Add(guide);
            }
        }

        return guides;
    }

    /// <summary>
    /// Reorders the page for trending only. Every other sort is already ordered by the engine,
    /// across pages, and re-sorting it here would break the cursor.
    /// </summary>
    private IReadOnlyList<Guide> Rank(
        IReadOnlyList<Guide> candidates, GuideSortOrder sort, string? text)
    {
        if (sort != GuideSortOrder.Trending)
        {
            return candidates;
        }

        return _rankings.Resolve(GuideRankingKeys.Trending).Rank(
            candidates,
            new GuideRankingContext
            {
                Filter = new GuideQueryFilter { Sort = sort },
                At = _clock.UtcNow,
                ViewerId = _authorization.CallerId,
                Query = text
            });
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
