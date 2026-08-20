using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Directories;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Extensibility.Caching;
using Diaspora.Modules.Guides.Extensibility.Knowledge;
using Diaspora.Modules.Guides.Extensibility.Search;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Knowledge.Discovery;

/// <summary>
/// Reports what is registered here.
/// <para>
/// Assembled from the live registries rather than from a list, so it cannot drift: a deployment that
/// adds a section provider gets it in this answer without anybody remembering to.
/// </para>
/// </summary>
public sealed class GetKnowledgeCapabilitiesQueryHandler
    : IQueryHandler<GetKnowledgeCapabilitiesQuery, KnowledgeCapabilitiesResponse>
{
    private readonly KnowledgeSectionProviderRegistry _sections;
    private readonly CountryContextContributorRegistry _context;
    private readonly KnowledgeAnalyticsRecorder _analytics;
    private readonly IKnowledgeSearchEngine _engine;
    private readonly IKnowledgeCache _cache;
    private readonly IKnowledgeCategoryRepository _categories;
    private readonly ICountryKnowledgeDirectoryRepository _directories;

    public GetKnowledgeCapabilitiesQueryHandler(
        KnowledgeSectionProviderRegistry sections,
        CountryContextContributorRegistry context,
        KnowledgeAnalyticsRecorder analytics,
        IKnowledgeSearchEngine engine,
        IKnowledgeCache cache,
        IKnowledgeCategoryRepository categories,
        ICountryKnowledgeDirectoryRepository directories)
    {
        _sections = sections;
        _context = context;
        _analytics = analytics;
        _engine = engine;
        _cache = cache;
        _categories = categories;
        _directories = directories;
    }

    public async Task<Result<KnowledgeCapabilitiesResponse>> Handle(
        GetKnowledgeCapabilitiesQuery query, CancellationToken cancellationToken)
    {
        var categories = await _categories.ListAsync(
            includeArchived: false, cancellationToken);

        var countries = await _directories.ListHeadersAsync(
            publishedOnly: true, cancellationToken);

        return Result.Success(new KnowledgeCapabilitiesResponse
        {
            Sections = _sections.Keys,
            Sorts =
            [
                KnowledgeWireNames.RelevanceSort,
                "newest",
                "most-helpful",
                "recently-updated",
                "popular",
                "trending"
            ],
            Difficulties =
            [
                .. Enum.GetValues<GuideDifficulty>()
                    .Select(KnowledgeWireNames.DifficultyToWire)
            ],
            SearchEngine = _engine.Name,
            SearchAvailable = await _engine.IsAvailableAsync(cancellationToken),
            CacheProvider = _cache.Name,
            CachingEnabled = _cache.IsEnabled,
            ContextModules = _context.Modules,
            AnalyticsSinks = _analytics.SinkNames,
            CategoryCount = categories.Count,
            PublishedCountryCount = countries.Count,
            MaxDirectoryEntries = KnowledgeLimits.MaxDirectoryEntries,
            MaxFeaturedGuides = KnowledgeLimits.MaxFeaturedGuides,
            MaxPathSteps = KnowledgeLimits.MaxPathSteps,
            MaxRecommendedReads = KnowledgeLimits.MaxRecommendedReads,
            MaxPageSize = KnowledgeLimits.MaxPageSize
        });
    }
}
