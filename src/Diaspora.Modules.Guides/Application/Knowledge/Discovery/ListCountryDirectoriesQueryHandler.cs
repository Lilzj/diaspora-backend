using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Knowledge.Mapping;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Directories;
using Diaspora.Modules.Guides.Domain.KnowledgeAnalytics;
using Diaspora.Modules.Guides.Extensibility.Caching;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Knowledge.Discovery;

/// <summary>
/// Serves the country grid.
/// <para>
/// <b>One guide count per country, and one analytics read for all of them.</b> The counts are index
/// seeks bounded by how many countries exist rather than by how much content they hold, which is
/// what makes them affordable at all; the interest figures come from a single leaderboard read
/// rather than one query per tile.
/// </para>
/// <para>
/// Cached, because it changes when a country launches and at no other time — the single most
/// cacheable read in the phase.
/// </para>
/// </summary>
public sealed class ListCountryDirectoriesQueryHandler
    : IQueryHandler<ListCountryDirectoriesQuery, IReadOnlyList<CountryDirectorySummaryResponse>>
{
    private const int InterestLeaderboardSize = 250;

    private readonly ICountryKnowledgeDirectoryRepository _directories;
    private readonly ICountryKnowledgeAnalyticsRepository _analytics;
    private readonly KnowledgeGuideFinder _finder;
    private readonly KnowledgeAuthorizationService _authorization;
    private readonly IKnowledgeCache _cache;

    public ListCountryDirectoriesQueryHandler(
        ICountryKnowledgeDirectoryRepository directories,
        ICountryKnowledgeAnalyticsRepository analytics,
        KnowledgeGuideFinder finder,
        KnowledgeAuthorizationService authorization,
        IKnowledgeCache cache)
    {
        _directories = directories;
        _analytics = analytics;
        _finder = finder;
        _authorization = authorization;
        _cache = cache;
    }

    public async Task<Result<IReadOnlyList<CountryDirectorySummaryResponse>>> Handle(
        ListCountryDirectoriesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var includeUnpublished = query.IncludeUnpublished && _authorization.IsCurator;

        if (includeUnpublished)
        {
            return Result.Success(
                await LoadAsync(publishedOnly: false, query.Locale, cancellationToken));
        }

        var key = KnowledgeCacheKeys.Countries(publishedOnly: true) + ":" + (query.Locale ?? "default");

        var cached = await _cache.GetAsync<CachedCountries>(key, cancellationToken);

        if (cached is not null)
        {
            return Result.Success(cached.Countries);
        }

        var countries = await LoadAsync(publishedOnly: true, query.Locale, cancellationToken);

        await _cache.SetAsync(key, new CachedCountries(countries), cancellationToken: cancellationToken);

        return Result.Success(countries);
    }

    private async Task<IReadOnlyList<CountryDirectorySummaryResponse>> LoadAsync(
        bool publishedOnly, string? locale, CancellationToken cancellationToken)
    {
        var directories = await _directories.ListHeadersAsync(publishedOnly, cancellationToken);

        // One leaderboard read for every tile's interest figure, rather than one per tile.
        var interest = (await _analytics.TopByInterestAsync(
                InterestLeaderboardSize, cancellationToken))
            .ToDictionary(
                analytics => analytics.CountryCode,
                analytics => analytics.DirectoryViews,
                StringComparer.Ordinal);

        var summaries = new List<CountryDirectorySummaryResponse>(directories.Count);

        foreach (var directory in directories)
        {
            summaries.Add(CountryDirectoryMapper.ToSummary(
                directory,
                locale,
                await _finder.CountCountryAsync(directory.CountryCode, cancellationToken),
                interest.GetValueOrDefault(directory.CountryCode)));
        }

        return summaries;
    }

    /// <summary>A wrapper, because the cache stores reference types. See the catalogue handler.</summary>
    private sealed record CachedCountries(IReadOnlyList<CountryDirectorySummaryResponse> Countries);
}
