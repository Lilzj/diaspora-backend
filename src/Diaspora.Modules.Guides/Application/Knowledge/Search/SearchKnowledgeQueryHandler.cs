using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Extensibility.Analytics;
using Diaspora.Modules.Guides.Extensibility.Search;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Knowledge.Search;

/// <summary>
/// Runs a knowledge search through whichever engine is registered.
/// <para>
/// <b>An unavailable engine is a failure, not an empty result.</b> Returning nothing would read as
/// "there is nothing about visas for this country", which is a far worse thing to tell somebody than
/// "search is down" — and it is the answer they would act on.
/// </para>
/// <para>
/// <b>The phrase is counted whatever the engine answered</b>, including when it answered nothing.
/// That is the whole point: a phrase with a high search count and no results is a guide that should
/// be written this month, named in the words the person used to ask for it.
/// </para>
/// <para>
/// The navigation row is expanded into its filing categories before the request reaches the engine,
/// so an OpenSearch adapter receives a plain list of category keys and never has to know that
/// navigation and filing are different things.
/// </para>
/// </summary>
public sealed class SearchKnowledgeQueryHandler
    : IQueryHandler<SearchKnowledgeQuery, KnowledgeSearchResponse>
{
    private readonly IKnowledgeSearchEngine _engine;
    private readonly IKnowledgeCategoryRepository _categories;
    private readonly KnowledgeCardResolver _cards;
    private readonly KnowledgeAnalyticsRecorder _analytics;
    private readonly IClock _clock;

    public SearchKnowledgeQueryHandler(
        IKnowledgeSearchEngine engine,
        IKnowledgeCategoryRepository categories,
        KnowledgeCardResolver cards,
        KnowledgeAnalyticsRecorder analytics,
        IClock clock)
    {
        _engine = engine;
        _categories = categories;
        _cards = cards;
        _analytics = analytics;
        _clock = clock;
    }

    public async Task<Result<KnowledgeSearchResponse>> Handle(
        SearchKnowledgeQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!await _engine.IsAvailableAsync(cancellationToken))
        {
            return Result.Failure<KnowledgeSearchResponse>(KnowledgeErrors.SearchUnavailable);
        }

        var request = query.Request;
        var country = KnowledgeNormalization.Country(request.CountryCode);

        var outcome = await _engine.SearchAsync(
            new KnowledgeSearchRequest
            {
                Text = request.Q,
                CountryCode = country,
                KnowledgeCategoryKey = request.CategoryKey,
                GuideCategoryKeys = await SourceKeysAsync(request.CategoryKey, cancellationToken),
                Language = string.IsNullOrWhiteSpace(request.Language)
                    ? null
                    : request.Language.Trim().ToLowerInvariant(),
                Tag = request.Tag,
                Difficulty = request.Difficulty,
                MaxReadingTimeMinutes = request.MaxReadingTime,
                VerifiedOnly = request.VerifiedOnly ?? false,
                OfficialOnly = request.OfficialOnly ?? false,
                FeaturedOnly = request.FeaturedOnly ?? false,
                Sort = request.Sort,
                Cursor = request.Cursor,
                Limit = Math.Clamp(
                    request.Limit ?? KnowledgeLimits.DefaultPageSize,
                    1,
                    KnowledgeLimits.MaxPageSize)
            },
            cancellationToken);

        var ordered = outcome.Hits.Select(hit => hit.GuideId).ToList();

        var cards = await _cards.ResolveAsync(ordered, cancellationToken);

        var results = KnowledgeCardResolver.Order(ordered, cards);

        await RecordAsync(request.Q, country, results.Count, cancellationToken);

        return Result.Success(new KnowledgeSearchResponse
        {
            Results = results,
            NextCursor = outcome.NextCursor,
            HasMore = outcome.HasMore,
            TotalCount = outcome.TotalCount,
            Engine = outcome.Engine,
            CountryCode = country
        });
    }

    /// <summary>
    /// Expands a navigation row key into the filing categories it draws from.
    /// <para>
    /// An unknown key falls back to itself, so a search for a row this deployment has not seeded
    /// still finds guides filed under that key rather than searching everything.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<string>> SourceKeysAsync(
        string? categoryKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(categoryKey))
        {
            return [];
        }

        var category = await _categories.GetByKeyAsync(categoryKey, cancellationToken);

        if (category is not null)
        {
            return category.EffectiveSourceKeys;
        }

        var normalized = KnowledgeNormalization.Key(categoryKey);

        return normalized is null ? [] : [normalized];
    }

    /// <summary>
    /// Counts the phrase. Fire-and-record rather than fire-and-forget: the recorder already isolates
    /// a failing sink, so awaiting costs nothing a caller can notice and guarantees the count
    /// happened before the response is written.
    /// </summary>
    private Task RecordAsync(
        string? term, string? countryCode, int resultCount, CancellationToken cancellationToken) =>
        _analytics.RecordAsync(
            new KnowledgeAnalyticsSignal
            {
                Kind = KnowledgeAnalyticsSignalKinds.Searched,
                CountryCode = countryCode,
                Term = term,
                ResultCount = resultCount,
                At = _clock.UtcNow
            },
            cancellationToken);
}
