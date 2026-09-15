using System.Diagnostics;
using System.Globalization;
using System.Text;
using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Integration;
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
using Diaspora.SharedKernel.Pagination;
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

        var nearest = sort.Value == ProviderSortOrder.Distance && aroundPoint
            ? await NearestPageAsync(scoped, scope.Value, cancellationToken)
            : null;

        // "Distance" that cannot be honoured across pages — no circle, or too much in it — answers
        // in the recommended order, and says so. Ranking each page by proximity instead is the
        // order that left nearer providers on page two, and it would not be the order reported.
        if (sort.Value == ProviderSortOrder.Distance && nearest is null)
        {
            scoped = scoped with { Sort = ProviderSortOrder.Recommended };
        }

        var page = nearest ?? await _providers.QueryAsync(scoped, cancellationToken);

        var distances = _geography.MeasureDistances(scope.Value, page.Items);

        var inside = GeographicScopeAdapter.WithinRadius(
            scope.Value, page.Items, distances, aroundPoint);

        // Already in distance order across every page; a page-level proximity ranking on top would
        // be the old behaviour, where page two could hold providers nearer than page one.
        var ranked = nearest is not null ? inside : Rank(inside, scoped, distances, request);

        var appliedSort = nearest is not null ? ProviderSortOrder.Distance : scoped.Sort;

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
            TotalCount = page.TotalCount,
            AppliedSort = appliedSort.ToString()
        });
    }

    /// <summary>
    /// How many providers a nearest-first search measures. Past this, the search answers in the
    /// recommended order and the response says so.
    /// </summary>
    private const int NearestCandidateLimit = 1_000;

    /// <summary>
    /// "Nearest first", across every page.
    /// <para>
    /// <b>Measured here, never in SQL</b> — every distance is Location's, which is the rule this
    /// module's architecture tests hold it to. Before this, a distance search ordered one page by
    /// proximity, so a nearer provider on page two stayed below page one. Every candidate inside
    /// the circle is read (identifiers and positions only), measured, ordered by distance then
    /// slug — providers with no position last, since they matched through a service area — and
    /// the page is cut after the cursor. Null when the scope is not a radius or holds more than
    /// <see cref="NearestCandidateLimit"/>.
    /// </para>
    /// </summary>
    private async Task<CursorPagedResult<Provider>?> NearestPageAsync(
        ProviderQueryFilter filter, GeographicScope scope, CancellationToken cancellationToken)
    {
        if (scope is not { Center: not null, IsViewport: false }
            || scope.Radius.Scope != Location.Domain.Enums.RadiusScope.Radius)
        {
            return null;
        }

        var candidates = await _providers.QueryNearestCandidatesAsync(
            filter, NearestCandidateLimit + 1, cancellationToken);

        if (candidates.Count > NearestCandidateLimit)
        {
            return null;
        }

        var edge = scope.Radius.Distance.Kilometers;

        // An unmeasurable provider sorts after every measured one, under the largest key a cursor
        // can carry, so it pages like any other row.
        var ordered = candidates
            .Select(candidate => (
                Candidate: candidate,
                Kilometres: _geography.KilometresFromCentre(scope, candidate.Latitude, candidate.Longitude)))
            .Where(measured => measured.Kilometres is not { } km || km <= edge)
            .Select(measured => (measured.Candidate, Key: measured.Kilometres ?? double.MaxValue))
            .OrderBy(measured => measured.Key)
            .ThenBy(measured => measured.Candidate.Slug, StringComparer.Ordinal)
            .ToList();

        var remaining = NearestCursor.TryParse(filter.Cursor) is { } after
            ? ordered.SkipWhile(measured =>
                measured.Key < after.Key
                || (measured.Key == after.Key
                    && string.CompareOrdinal(measured.Candidate.Slug, after.Slug) <= 0))
            : ordered;

        var limit = filter.EffectiveLimit;
        var window = remaining.Take(limit + 1).ToList();
        var hasMore = window.Count > limit;
        var pageRows = window.Take(limit).ToList();

        var providers = await _providers.GetManyAsync(
            [.. pageRows.Select(measured => measured.Candidate.Id)], cancellationToken);

        var byId = providers.ToDictionary(provider => provider.Id);

        var items = pageRows
            .Select(measured => byId.GetValueOrDefault(measured.Candidate.Id))
            .OfType<Provider>()
            .ToList();

        var nextCursor = hasMore && pageRows.Count > 0
            ? NearestCursor.Encode(pageRows[^1].Key, pageRows[^1].Candidate.Slug)
            : null;

        return CursorPagedResult<Provider>.Create(items, nextCursor, hasMore, ordered.Count);
    }

    /// <summary>
    /// The nearest-first continuation token: the last row's distance and slug.
    /// <para>
    /// Written in the repository cursor's own shape — base64 of <c>key|slug|0</c> — rather than by
    /// reaching into Persistence from here, so a token that outlives a fallback to the ordinary
    /// query still decodes there as a number and a slug instead of failing.
    /// </para>
    /// </summary>
    private static class NearestCursor
    {
        public static string Encode(double key, string slug) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(
                string.Create(CultureInfo.InvariantCulture, $"{key:R}|{slug}|0")));

        public static (double Key, string Slug)? TryParse(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            try
            {
                var parts = Encoding.UTF8.GetString(Convert.FromBase64String(token)).Split('|');

                return parts.Length >= 2
                    && parts[1].Length > 0
                    && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var key)
                        ? (key, parts[1])
                        : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }
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
