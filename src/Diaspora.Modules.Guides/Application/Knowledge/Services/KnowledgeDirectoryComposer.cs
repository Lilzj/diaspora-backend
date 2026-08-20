using Diaspora.Modules.Guides.Application.Knowledge.Mapping;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Directories;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Domain.LearningPaths;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Extensibility.Knowledge;

namespace Diaspora.Modules.Guides.Application.Knowledge.Services;

/// <summary>
/// Assembles a country page from a directory, the catalogue, the curated shelf and the counts.
/// <para>
/// <b>This class is what the phase actually delivers.</b> Everything a country page shows lives in a
/// different place — the arrangement on the directory, the labels in the catalogue, the guides in
/// B8, the counts in three projections — and a client assembling it would need twenty round trips
/// and would get the localization fallback wrong. Composing it once, here, is what makes every
/// consumer's version identical and what makes a new client cost nothing.
/// </para>
/// <para>
/// <b>Two guide resolutions per page, and not one more.</b> The featured shelf and the learning
/// paths both produce identifiers; they are collected together and resolved in a single batched
/// call. Counts go through the repository's count rather than through a page, so a row badge costs
/// an index seek rather than twenty rows nobody renders.
/// </para>
/// <para>
/// <b>The reader never sees a hidden row and never sees an unpublished directory.</b> Filtering
/// happens here, once, rather than in each query handler — a handler that forgot would leak a
/// country's half-arranged launch page, and it would look correct in every test written by the
/// person who forgot.
/// </para>
/// </summary>
public sealed class KnowledgeDirectoryComposer
{
    private readonly IKnowledgeCategoryRepository _categories;
    private readonly ILearningPathRepository _paths;
    private readonly KnowledgeGuideFinder _finder;
    private readonly KnowledgeCardResolver _cards;
    private readonly KnowledgeStatisticsBuilder _statistics;

    public KnowledgeDirectoryComposer(
        IKnowledgeCategoryRepository categories,
        ILearningPathRepository paths,
        KnowledgeGuideFinder finder,
        KnowledgeCardResolver cards,
        KnowledgeStatisticsBuilder statistics)
    {
        _categories = categories;
        _paths = paths;
        _finder = finder;
        _cards = cards;
        _statistics = statistics;
    }

    /// <summary>The reader-facing country page.</summary>
    public async Task<CountryKnowledgeDirectoryResponse> ComposeAsync(
        CountryKnowledgeDirectory directory,
        string? locale,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(directory);

        var (displayName, summary) = directory.Localized(locale);

        var entries = await ComposeEntriesAsync(directory, locale, language, cancellationToken);

        var featuredIds = directory.FeaturedFor(null);

        var paths = await _paths.ListByCountryAsync(
            directory.CountryCode, publishedOnly: true, cancellationToken);

        var cards = await _cards.ResolveAsync(featuredIds, cancellationToken);

        return new CountryKnowledgeDirectoryResponse
        {
            CountryCode = directory.CountryCode,
            DisplayName = displayName,
            Summary = summary,
            PrimaryLanguage = directory.PrimaryLanguage,
            Languages = directory.Languages,
            IsPublished = directory.IsPublished,
            PublishedAt = directory.PublishedAt,
            Revision = directory.Revision,
            Categories = entries,
            FeaturedGuides = KnowledgeCardResolver.Order(featuredIds, cards),
            LearningPaths = [.. paths.Select(LearningPathMapper.ToSummary)],
            Statistics = await _statistics.BuildAsync(
                directory.CountryCode, directory, cancellationToken)
        };
    }

    /// <summary>
    /// The navigation rows a reader sees, labelled and counted.
    /// <para>
    /// The catalogue is fetched once for the whole arrangement, so a forty-row directory is one
    /// query rather than forty. Counts are one seek each, and there is no way around that: they are
    /// per-category and per-country, and the whole reason a directory row carries a count is that a
    /// reader deciding where to start is guided far more by "Housing — 14 guides" than by "Housing".
    /// </para>
    /// <para>
    /// A row whose catalogue entry has been archived is dropped. The alternative — rendering the raw
    /// key — shows a reader <c>cost-of-living</c> where a label should be.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<CountryDirectoryEntryResponse>> ComposeEntriesAsync(
        CountryKnowledgeDirectory directory,
        string? locale,
        string? language,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(directory);

        var visible = directory.VisibleEntries;

        if (visible.Count == 0)
        {
            return [];
        }

        var catalogue = await _categories.GetByKeysAsync(
            [.. visible.Select(entry => entry.KnowledgeCategoryKey)], cancellationToken);

        var composed = new List<CountryDirectoryEntryResponse>(visible.Count);

        foreach (var entry in visible)
        {
            if (!catalogue.TryGetValue(entry.KnowledgeCategoryKey, out var category))
            {
                continue;
            }

            var (name, description) = category.Localized(locale);

            composed.Add(new CountryDirectoryEntryResponse
            {
                KnowledgeCategoryKey = entry.KnowledgeCategoryKey,
                Label = entry.LabelOverride ?? name,
                Description = description,
                Icon = category.Icon,
                Position = entry.Position,
                GuideCount = await _finder.CountAsync(
                    directory.CountryCode,
                    category.EffectiveSourceKeys,
                    language,
                    cancellationToken),
                IsHidden = false
            });
        }

        return composed;
    }

    /// <summary>
    /// The per-category breakdown a dashboard renders: what is written against what is read.
    /// <para>
    /// Bounded, because a dashboard is a summary. A country arranged with sixty rows returns the
    /// first thirty and the directory endpoint returns the rest — a breakdown longer than that has
    /// stopped being a summary and become the directory with extra columns.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<KnowledgeCategoryCountResponse>> ComposeBreakdownAsync(
        CountryKnowledgeDirectory directory,
        IReadOnlyDictionary<string, long> viewsByCategory,
        string? locale,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(viewsByCategory);

        var visible = directory.VisibleEntries
            .Take(KnowledgeLimits.DashboardCategoryLimit)
            .ToList();

        if (visible.Count == 0)
        {
            return [];
        }

        var catalogue = await _categories.GetByKeysAsync(
            [.. visible.Select(entry => entry.KnowledgeCategoryKey)], cancellationToken);

        var breakdown = new List<KnowledgeCategoryCountResponse>(visible.Count);

        foreach (var entry in visible)
        {
            if (!catalogue.TryGetValue(entry.KnowledgeCategoryKey, out var category))
            {
                continue;
            }

            var (name, _) = category.Localized(locale);

            breakdown.Add(new KnowledgeCategoryCountResponse
            {
                KnowledgeCategoryKey = entry.KnowledgeCategoryKey,
                Name = entry.LabelOverride ?? name,
                Icon = category.Icon,
                GuideCount = await _finder.CountAsync(
                    directory.CountryCode,
                    category.EffectiveSourceKeys,
                    language: null,
                    cancellationToken),
                Views = viewsByCategory.GetValueOrDefault(entry.KnowledgeCategoryKey)
            });
        }

        return breakdown;
    }

    /// <summary>
    /// Builds the section request a provider is handed for one category.
    /// <para>
    /// Assembled here rather than in each provider so that every provider sees the same resolved
    /// context — the expanded source keys, the curated shelf, the clamped limit — and none of them
    /// has to resolve a category to fill a shelf.
    /// </para>
    /// </summary>
    public static KnowledgeSectionRequest SectionRequest(
        CountryKnowledgeDirectory directory,
        KnowledgeCategory category,
        string? locale,
        string? language,
        int limit,
        DateTimeOffset at) =>
        new()
        {
            CountryCode = directory.CountryCode,
            KnowledgeCategoryKey = category.Key,
            GuideCategoryKeys = category.EffectiveSourceKeys,
            Language = language,
            Locale = locale,
            Limit = Math.Clamp(limit, 1, KnowledgeLimits.MaxPageSize),
            CuratedGuideIds = directory.FeaturedFor(category.Key),
            At = at
        };
}
