using System.Diagnostics;
using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Application.Mapping;
using Diaspora.Modules.Providers.Application.Services;
using Diaspora.Modules.Providers.Contracts.V1.Requests;
using Diaspora.Modules.Providers.Contracts.V1.Responses;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Policies;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Modules.Providers.Extensibility.Ranking;
using Diaspora.Modules.Providers.Observability;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Application.Discovery;

/// <summary>
/// Serves one page of discovery.
/// <para>
/// The shape is: resolve geography through B4, translate the request into a filter, query, ask
/// Location for distances, rank in memory, build the read context, map. Every step delegates the
/// thing it should — geography to B4, ordering to a contributed strategy, category resolution to
/// the catalogue — and the handler's own job is to sequence them.
/// </para>
/// <para>
/// <b>Ranking happens after the query, in memory, over one page.</b> That is a deliberate
/// limitation and worth naming: the balanced strategy reorders the fifty rows the database
/// already chose, so it improves the order of a page rather than choosing which page. Real
/// relevance ordering needs a search index and is B15's; until then, a stable, explicable
/// re-ordering of a sensible page is a far better answer than a scoring function pushed into SQL
/// that nobody could tune or test.
/// </para>
/// <para>
/// <see cref="ProviderTelemetry.EmptyResults"/> is incremented on a zero-result page. That is a
/// successful response with an empty body, so no error rate will ever surface it — and a rising
/// count is the earliest signal that a category is missing, a default radius is too tight, or a
/// country has no providers in it yet.
/// </para>
/// </summary>
public sealed class SearchProvidersQueryHandler
    : IQueryHandler<SearchProvidersQuery, ProviderPageResponse>
{
    private readonly IProviderRepository _providers;
    private readonly ServiceCategoryResolver _categories;
    private readonly GeographicScopeAdapter _geography;
    private readonly RankingStrategyRegistry _rankings;
    private readonly ProviderAuthorizationService _authorization;
    private readonly ProviderReadContextBuilder _contexts;
    private readonly IClock _clock;

    public SearchProvidersQueryHandler(
        IProviderRepository providers,
        ServiceCategoryResolver categories,
        GeographicScopeAdapter geography,
        RankingStrategyRegistry rankings,
        ProviderAuthorizationService authorization,
        ProviderReadContextBuilder contexts,
        IClock clock)
    {
        _providers = providers;
        _categories = categories;
        _geography = geography;
        _rankings = rankings;
        _authorization = authorization;
        _contexts = contexts;
        _clock = clock;
    }

    public async Task<Result<ProviderPageResponse>> Handle(
        SearchProvidersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var request = query.Request;
        var started = Stopwatch.GetTimestamp();

        var sort = ProviderWireNames.ParseSort(request.Sort);

        if (sort.IsFailure)
        {
            return Result.Failure<ProviderPageResponse>(sort.Error);
        }

        var filter = await BuildFilterAsync(request, sort.Value, cancellationToken);

        if (filter.IsFailure)
        {
            return Result.Failure<ProviderPageResponse>(filter.Error);
        }

        var scope = await _geography.ResolveAsync(
            request, _authorization.CallerId, cancellationToken);

        if (scope.IsFailure)
        {
            return Result.Failure<ProviderPageResponse>(scope.Error);
        }

        // A point the member sent, as opposed to the centre the resolver gives a town search.
        var aroundPoint = request.Latitude is not null
            && request.Longitude is not null
            && string.IsNullOrWhiteSpace(request.Bounds);

        var scoped = await _geography.ApplyAsync(
            filter.Value, scope.Value, aroundPoint, cancellationToken);

        var page = await _providers.QueryAsync(scoped, cancellationToken);

        var distances = _geography.MeasureDistances(scope.Value, page.Items);

        var inside = GeographicScopeAdapter.WithinRadius(
            scope.Value, page.Items, distances, aroundPoint);

        var ranked = Rank(inside, scoped, distances, request);

        var context = await _contexts.BuildAsync(
            ranked,
            scope.Value,
            distances,
            query.LanguageCode,
            includeViewerReviews: false,
            cancellationToken);

        RecordTelemetry(request, sort.Value, scope.Value.IsSpatial, ranked.Count, started);

        return Result.Success(new ProviderPageResponse
        {
            Items = ranked.Select(provider => ProviderMapper.ToSummary(provider, context)).ToList(),
            NextCursor = page.NextCursor,
            HasMore = page.HasMore,
            TotalCount = page.TotalCount
        });
    }

    private async Task<Result<ProviderQueryFilter>> BuildFilterAsync(
        ProviderSearchRequest request, ProviderSortOrder sort, CancellationToken cancellationToken)
    {
        var filter = new ProviderQueryFilter
        {
            Statuses = [ProviderStatus.Active],
            Query = request.Query,
            VerifiedOnly = request.VerifiedOnly ?? false,
            FeaturedOnly = request.FeaturedOnly ?? false,
            CertifiedOnly = request.CertifiedOnly ?? false,
            MinimumRating = request.MinRating,
            MinimumReviewCount = request.MinReviews,
            TagSlugs = Split(request.Tags)?.Select(Slugify).ToList(),
            LanguageCodes = Split(request.Languages)?.Select(code => code.ToLowerInvariant()).ToList(),
            ProviderTypes = ParseMany(request.ProviderTypes, ProviderWireNames.ParseProviderType),
            DeliveryModes = ParseMany(request.DeliveryModes, ProviderWireNames.ParseDeliveryMode),
            AllowedVisibility = _authorization.AllowedVisibility(),
            ViewerId = _authorization.CallerId,
            Sort = sort,
            Cursor = request.Cursor,
            Limit = request.Limit ?? ProviderLimits.DefaultPageSize,
            IncludeTotalCount = request.IncludeTotalCount ?? false
        };

        if (string.IsNullOrWhiteSpace(request.Category))
        {
            return Result.Success(filter);
        }

        var category = await _categories.ResolveAsync(request.Category, cancellationToken);

        if (category.IsFailure)
        {
            return Result.Failure<ProviderQueryFilter>(category.Error);
        }

        // A subtree filter by default: choosing "Food & Restaurants" should return the
        // restaurants filed under it, not only the providers filed against the parent node
        // itself. A caller that genuinely wants one node says so.
        return Result.Success(request.IncludeSubcategories ?? true
            ? filter with { CategoryPathPrefix = category.Value.Path.Value }
            : filter with { CategoryId = category.Value.Id });
    }

    /// <summary>
    /// Reorders the page.
    /// <para>
    /// Only for <see cref="ProviderSortOrder.Recommended"/> and
    /// <see cref="ProviderSortOrder.Distance"/>: the database already ordered a rating, review-count
    /// or name sort correctly and correctly across pages, and re-sorting those in memory would
    /// scramble the cursor's contract — page two would not continue page one.
    /// </para>
    /// </summary>
    private IReadOnlyList<Provider> Rank(
        IReadOnlyList<Provider> candidates,
        ProviderQueryFilter filter,
        IReadOnlyDictionary<Guid, double> distances,
        ProviderSearchRequest request)
    {
        if (filter.Sort is not (ProviderSortOrder.Recommended or ProviderSortOrder.Distance))
        {
            return candidates;
        }

        var key = request.Ranking ?? (filter.Sort == ProviderSortOrder.Distance
            ? RankingKeys.Proximity
            : RankingKeys.Balanced);

        var strategy = _rankings.Resolve(key);

        return strategy.Rank(
            candidates,
            new ProviderRankingContext
            {
                Filter = filter,
                At = _clock.UtcNow,
                ViewerId = _authorization.CallerId,
                DistancesKm = distances,
                ViewerCountryCode = request.OriginCountryCode,
                Query = request.Query
            });
    }

    private static void RecordTelemetry(
        ProviderSearchRequest request,
        ProviderSortOrder sort,
        bool isSpatial,
        int resultCount,
        long started)
    {
        var sortTag = new KeyValuePair<string, object?>("sort", sort.ToString());
        var spatialTag = new KeyValuePair<string, object?>("spatial", isSpatial);

        ProviderTelemetry.DiscoveryQueries.Add(1, sortTag, spatialTag);

        ProviderTelemetry.DiscoveryLatency.Record(
            Stopwatch.GetElapsedTime(started).TotalMilliseconds, sortTag);

        if (resultCount == 0)
        {
            ProviderTelemetry.EmptyResults.Add(
                1,
                new KeyValuePair<string, object?>("hasCategory", request.Category is not null),
                new KeyValuePair<string, object?>("hasRadius", request.RadiusKm is not null));
        }
    }

    private static string[]? Split(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Turns a caller's tag into the slug the column stores, so <c>"Natural Hair"</c> matches
    /// <c>natural-hair</c>. Mirrors <c>ProviderTag</c>'s own normalisation.
    /// </summary>
    private static string Slugify(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length);
        var previousWasDash = false;

        foreach (var character in value.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasDash = false;
            }
            else if (!previousWasDash && builder.Length > 0)
            {
                builder.Append('-');
                previousWasDash = true;
            }
        }

        return builder.ToString().Trim('-');
    }

    /// <summary>
    /// Parses a comma-separated enum list, dropping anything unrecognised.
    /// <para>
    /// Dropping rather than failing: a client that sends a filter value a later deployment
    /// removed should get a slightly wider result set, not an error on a browse screen.
    /// </para>
    /// </summary>
    private static List<TEnum>? ParseMany<TEnum>(
        string? value, Func<string, Result<TEnum>> parse) where TEnum : struct, Enum
    {
        var parts = Split(value);

        if (parts is null)
        {
            return null;
        }

        var parsed = parts
            .Select(parse)
            .Where(result => result.IsSuccess)
            .Select(result => result.Value)
            .ToList();

        return parsed.Count > 0 ? parsed : null;
    }
}
