using Diaspora.Modules.Guides.Application.Knowledge.Mapping;
using Diaspora.Modules.Guides.Domain.Directories;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Domain.LearningPaths;
using Diaspora.Modules.Guides.Domain.Recommendations;
using Diaspora.Modules.Guides.Integration;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Knowledge.Services;

/// <summary>
/// The read surface every other module uses to reach country-specific knowledge.
/// <para>
/// <b>Nothing here answers from unpublished state.</b> A consumer holding a country code for a
/// directory a curator is still arranging gets null, exactly as it would for a country the platform
/// has never heard of — because a consuming module has no way to distinguish "not ready" from "not
/// there", and rendering a half-arranged launch page inside somebody else's screen is the one
/// failure mode a shared discovery layer must not have.
/// </para>
/// <para>
/// <b>Deliberately narrower than the query surface.</b> A consumer needs what a country covers,
/// which guides answer a subject, and where onboarding starts. Everything curation-shaped is absent,
/// and so is anything that takes a member — personalized onboarding is out of scope for this phase,
/// and a signature that accepted a user identifier would be an invitation to build it here where no
/// consumer could see it happening.
/// </para>
/// </summary>
public sealed class KnowledgeDirectoryService : IKnowledgeDirectory
{
    private const int MaxCountriesListed = 250;

    private readonly ICountryKnowledgeDirectoryRepository _directories;
    private readonly IKnowledgeCategoryRepository _categories;
    private readonly ILearningPathRepository _paths;
    private readonly IKnowledgeRecommendationRepository _recommendations;
    private readonly KnowledgeGuideFinder _finder;
    private readonly IGuideDirectory _guides;

    public KnowledgeDirectoryService(
        ICountryKnowledgeDirectoryRepository directories,
        IKnowledgeCategoryRepository categories,
        ILearningPathRepository paths,
        IKnowledgeRecommendationRepository recommendations,
        KnowledgeGuideFinder finder,
        IGuideDirectory guides)
    {
        _directories = directories;
        _categories = categories;
        _paths = paths;
        _recommendations = recommendations;
        _finder = finder;
        _guides = guides;
    }

    public async Task<CountryKnowledgeSummary?> FindCountryAsync(
        string countryCode, string? locale = null, CancellationToken cancellationToken = default)
    {
        var directory = await _directories.GetForReadAsync(countryCode, cancellationToken);

        if (directory is null || !directory.IsPublished)
        {
            return null;
        }

        var guideCount = await _finder.CountCountryAsync(
            directory.CountryCode, cancellationToken);

        var paths = await _paths.ListByCountryAsync(
            directory.CountryCode, publishedOnly: true, cancellationToken);

        return CountryDirectoryMapper.ToKnowledgeSummary(
            directory, locale, guideCount, paths.Count);
    }

    /// <summary>
    /// Every published country.
    /// <para>
    /// Guide counts are one query each, and that is affordable precisely because the answer is
    /// bounded by how many countries exist rather than by how much content they hold. A consumer
    /// wanting this repeatedly should hold it — it changes when a country launches.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<CountryKnowledgeSummary>> ListCountriesAsync(
        string? locale = null, CancellationToken cancellationToken = default)
    {
        var directories = await _directories.ListHeadersAsync(
            publishedOnly: true, cancellationToken);

        var summaries = new List<CountryKnowledgeSummary>(directories.Count);

        foreach (var directory in directories.Take(MaxCountriesListed))
        {
            var guideCount = await _finder.CountCountryAsync(
                directory.CountryCode, cancellationToken);

            // Header reads carry no entries, so the category count on this shape is zero by
            // construction. A consumer wanting the arrangement asks for the categories.
            summaries.Add(CountryDirectoryMapper.ToKnowledgeSummary(
                directory, locale, guideCount, learningPathCount: 0));
        }

        return summaries;
    }

    public async Task<IReadOnlyList<KnowledgeCategoryDescriptor>> ListCategoriesAsync(
        string countryCode, string? locale = null, CancellationToken cancellationToken = default)
    {
        var directory = await _directories.GetForReadAsync(countryCode, cancellationToken);

        if (directory is null || !directory.IsPublished)
        {
            return [];
        }

        var visible = directory.VisibleEntries;

        if (visible.Count == 0)
        {
            return [];
        }

        var catalogue = await _categories.GetByKeysAsync(
            [.. visible.Select(entry => entry.KnowledgeCategoryKey)], cancellationToken);

        var descriptors = new List<KnowledgeCategoryDescriptor>(visible.Count);

        foreach (var entry in visible)
        {
            if (!catalogue.TryGetValue(entry.KnowledgeCategoryKey, out var category))
            {
                continue;
            }

            var count = await _finder.CountAsync(
                directory.CountryCode,
                category.EffectiveSourceKeys,
                language: null,
                cancellationToken);

            var descriptor = KnowledgeCategoryMapper.ToDescriptor(category, locale, count);

            // A country's own wording wins over the catalogue's, exactly as it does on the country
            // page — a consumer rendering a different label from the directory would be a visible
            // inconsistency in the same product.
            descriptors.Add(entry.LabelOverride is null
                ? descriptor
                : descriptor with { Name = entry.LabelOverride });
        }

        return descriptors;
    }

    /// <summary>
    /// The guides that answer one subject for one country, curated first.
    /// <para>
    /// <b>Curated guides lead, then the most helpful, and the merge is the whole value.</b> A caller
    /// asking for the housing guides in Mauritius wants what an editor put first if there is one and
    /// what readers found useful otherwise — and every module that would have written this alone
    /// would have picked one of those two and been wrong for half the countries.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<GuideSummary>> ListCategoryGuidesAsync(
        string countryCode,
        string knowledgeCategoryKey,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var directory = await _directories.GetForReadAsync(countryCode, cancellationToken);

        if (directory is null || !directory.IsPublished)
        {
            return [];
        }

        var category = await _categories.GetByKeyAsync(knowledgeCategoryKey, cancellationToken);

        if (category is null)
        {
            return [];
        }

        var ordered = new List<Guid>(directory.FeaturedFor(category.Key));

        var filter = _finder.Filter(
            directory.CountryCode,
            category.EffectiveSourceKeys,
            language: null,
            GuideSortOrder.MostHelpful,
            limit);

        foreach (var guideId in await _finder.FindIdsAsync(filter, cancellationToken))
        {
            if (!ordered.Contains(guideId))
            {
                ordered.Add(guideId);
            }
        }

        return await ResolveAsync(ordered.Take(limit).ToList(), cancellationToken);
    }

    public async Task<IReadOnlyList<GuideSummary>> ListFeaturedGuidesAsync(
        string countryCode, int limit = 10, CancellationToken cancellationToken = default)
    {
        var directory = await _directories.GetForReadAsync(countryCode, cancellationToken);

        if (directory is null || !directory.IsPublished)
        {
            return [];
        }

        return await ResolveAsync(
            [.. directory.FeaturedFor(null).Take(limit)], cancellationToken);
    }

    public async Task<IReadOnlyList<LearningPathDescriptor>> ListLearningPathsAsync(
        string countryCode, CancellationToken cancellationToken = default)
    {
        var directory = await _directories.GetForReadAsync(countryCode, cancellationToken);

        if (directory is null || !directory.IsPublished)
        {
            return [];
        }

        var paths = await _paths.ListByCountryAsync(
            directory.CountryCode, publishedOnly: true, cancellationToken);

        return [.. paths.Select(LearningPathMapper.ToDescriptor)];
    }

    public async Task<IReadOnlyList<GuideSummary>> ListRecommendedReadsAsync(
        GuideId sourceGuideId, int limit = 5, CancellationToken cancellationToken = default)
    {
        var recommendation = await _recommendations.GetBySourceForReadAsync(
            sourceGuideId, cancellationToken);

        if (recommendation is null || recommendation.Reads.Count == 0)
        {
            return [];
        }

        var ordered = recommendation.Reads
            .OrderBy(read => read.Position)
            .Select(read => read.GuideId)
            .Take(Math.Max(1, limit))
            .ToList();

        return await ResolveAsync(ordered, cancellationToken);
    }

    /// <summary>
    /// Resolves identifiers through B8's own directory contract, preserving curated order.
    /// <para>
    /// Through <c>IGuideDirectory</c> rather than the repository, deliberately: it is the surface
    /// B8 publishes for exactly this, it applies B8's own visibility rules, and it means the
    /// Knowledge Directory resolves a guide the same way any other consuming module would.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<GuideSummary>> ResolveAsync(
        List<Guid> guideIds, CancellationToken cancellationToken)
    {
        if (guideIds.Count == 0)
        {
            return [];
        }

        var resolved = await _guides.FindManyAsync(
            [.. guideIds.Select(GuideId.From)], cancellationToken);

        var ordered = new List<GuideSummary>(guideIds.Count);

        foreach (var guideId in guideIds)
        {
            if (resolved.TryGetValue(guideId, out var summary))
            {
                ordered.Add(summary);
            }
        }

        return ordered;
    }
}
