using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Knowledge.Mapping;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Directories;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Extensibility.Analytics;
using Diaspora.Modules.Guides.Extensibility.Caching;
using Diaspora.Modules.Guides.Extensibility.Knowledge;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Knowledge.Discovery;

/// <summary>
/// Serves a category page.
/// <para>
/// <b>Every shelf's identifiers are collected before a single guide is resolved.</b> Six or seven
/// providers each produce a short list, they overlap heavily — a featured guide is usually also a
/// most-helpful one — and resolving per shelf would be seven queries returning largely the same
/// rows. One batched resolution fills all of them.
/// </para>
/// <para>
/// <b>The category has to be one this country actually lists.</b> A reader following a link to a row
/// Mauritius does not carry should be told the page is not there, rather than shown an empty version
/// of a page that was never arranged.
/// </para>
/// </summary>
public sealed class GetKnowledgeCategoryViewQueryHandler
    : IQueryHandler<GetKnowledgeCategoryViewQuery, KnowledgeCategoryViewResponse>
{
    private readonly ICountryKnowledgeDirectoryRepository _directories;
    private readonly IKnowledgeCategoryRepository _categories;
    private readonly KnowledgeSectionProviderRegistry _sections;
    private readonly KnowledgeCardResolver _cards;
    private readonly KnowledgeGuideFinder _finder;
    private readonly KnowledgeAuthorizationService _authorization;
    private readonly KnowledgeAnalyticsRecorder _analytics;
    private readonly IKnowledgeCache _cache;
    private readonly IClock _clock;

    public GetKnowledgeCategoryViewQueryHandler(
        ICountryKnowledgeDirectoryRepository directories,
        IKnowledgeCategoryRepository categories,
        KnowledgeSectionProviderRegistry sections,
        KnowledgeCardResolver cards,
        KnowledgeGuideFinder finder,
        KnowledgeAuthorizationService authorization,
        KnowledgeAnalyticsRecorder analytics,
        IKnowledgeCache cache,
        IClock clock)
    {
        _directories = directories;
        _categories = categories;
        _sections = sections;
        _cards = cards;
        _finder = finder;
        _authorization = authorization;
        _analytics = analytics;
        _cache = cache;
        _clock = clock;
    }

    public async Task<Result<KnowledgeCategoryViewResponse>> Handle(
        GetKnowledgeCategoryViewQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var directory = await _directories.GetForReadAsync(
            query.CountryCode, cancellationToken);

        if (directory is null
            || (!directory.IsPublished && !_authorization.IsCurator))
        {
            return Result.Failure<KnowledgeCategoryViewResponse>(
                KnowledgeErrors.DirectoryNotFound);
        }

        var category = await _categories.GetByKeyAsync(
            query.KnowledgeCategoryKey, cancellationToken);

        if (category is null || !directory.Lists(category.Key))
        {
            return Result.Failure<KnowledgeCategoryViewResponse>(
                KnowledgeErrors.CategoryNotFound);
        }

        var composed = await ComposeAsync(query, directory, category, cancellationToken);

        await RecordViewAsync(directory.CountryCode, category.Key, query.Locale, cancellationToken);

        return Result.Success(composed);
    }

    private async Task<KnowledgeCategoryViewResponse> ComposeAsync(
        GetKnowledgeCategoryViewQuery query,
        CountryKnowledgeDirectory directory,
        KnowledgeCategory category,
        CancellationToken cancellationToken)
    {
        var cacheable = directory.IsPublished
            && !_authorization.IsCurator
            && string.IsNullOrWhiteSpace(query.Language)
            && query.Limit is null;

        var key = KnowledgeCacheKeys.Category(
            directory.CountryCode, category.Key, query.Locale);

        if (cacheable)
        {
            var cached = await _cache.GetAsync<KnowledgeCategoryViewResponse>(
                key, cancellationToken);

            if (cached is not null)
            {
                return cached;
            }
        }

        var limit = Math.Clamp(
            query.Limit ?? KnowledgeLimits.DefaultPageSize, 1, KnowledgeLimits.MaxPageSize);

        var request = KnowledgeDirectoryComposer.SectionRequest(
            directory, category, query.Locale, query.Language, limit, _clock.UtcNow);

        var sections = await _sections.BuildAsync(request, cancellationToken);

        // Every identifier across every shelf, resolved once.
        var cards = await _cards.ResolveAsync(
            [.. sections.SelectMany(section => section.GuideIds)], cancellationToken);

        var (name, description) = category.Localized(query.Locale);
        var (countryName, _) = directory.Localized(query.Locale);

        var entry = directory.Entries.FirstOrDefault(
            candidate => string.Equals(
                candidate.KnowledgeCategoryKey, category.Key, StringComparison.Ordinal));

        var composed = new KnowledgeCategoryViewResponse
        {
            CountryCode = directory.CountryCode,
            CountryName = countryName,
            Category = KnowledgeCategoryMapper.ToResponse(category, query.Locale) with
            {
                Name = entry?.LabelOverride ?? name,
                Description = description
            },
            Label = entry?.LabelOverride,
            GuideCount = await _finder.CountAsync(
                directory.CountryCode,
                category.EffectiveSourceKeys,
                query.Language,
                cancellationToken),
            Sections =
            [
                .. sections.Select(section => new KnowledgeSectionResponse
                {
                    Key = section.Key,
                    Title = section.Title,
                    Order = section.Order,
                    Guides = KnowledgeCardResolver.Order(section.GuideIds, cards)
                })
                .Where(section => section.Guides.Count > 0)
            ],
            ProviderCategoryKeys =
                [.. category.ProviderCategories.Select(provider => provider.ServiceCategoryKey)]
        };

        if (cacheable)
        {
            await _cache.SetAsync(key, composed, cancellationToken: cancellationToken);
        }

        return composed;
    }

    private async Task RecordViewAsync(
        string countryCode,
        string categoryKey,
        string? locale,
        CancellationToken cancellationToken)
    {
        if (_authorization.IsCurator)
        {
            return;
        }

        await _analytics.RecordAsync(
            new KnowledgeAnalyticsSignal
            {
                Kind = KnowledgeAnalyticsSignalKinds.CategoryViewed,
                CountryCode = countryCode,
                KnowledgeCategoryKey = categoryKey,
                Locale = locale,
                At = _clock.UtcNow
            },
            cancellationToken);
    }
}
