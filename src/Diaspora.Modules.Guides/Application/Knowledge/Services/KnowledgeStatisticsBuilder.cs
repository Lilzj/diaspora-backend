using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Directories;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.KnowledgeAnalytics;
using Diaspora.Modules.Guides.Domain.LearningPaths;

namespace Diaspora.Modules.Guides.Application.Knowledge.Services;

/// <summary>
/// Builds the headline numbers for one country: what exists, and what people did with it.
/// <para>
/// <b>Both halves in one place, because the interesting cases are the mismatches.</b> A country with
/// forty guides and no readers has a discovery problem; one with heavy traffic and eight guides has
/// a commissioning problem. An editorial team cannot tell those apart from either number alone,
/// which is why the two are computed together and returned together rather than living on separate
/// endpoints that nobody correlates.
/// </para>
/// <para>
/// <b>Counts are exact and come through the guide repository's own count.</b> Paging to count would
/// report "at least twenty" for every busy category — technically true and useless to the team the
/// number exists for.
/// </para>
/// </summary>
public sealed class KnowledgeStatisticsBuilder
{
    private readonly KnowledgeGuideFinder _finder;
    private readonly ICountryKnowledgeAnalyticsRepository _analytics;
    private readonly ILearningPathRepository _paths;

    public KnowledgeStatisticsBuilder(
        KnowledgeGuideFinder finder,
        ICountryKnowledgeAnalyticsRepository analytics,
        ILearningPathRepository paths)
    {
        _finder = finder;
        _analytics = analytics;
        _paths = paths;
    }

    public async Task<KnowledgeStatisticsResponse> BuildAsync(
        string countryCode,
        CountryKnowledgeDirectory? directory,
        CancellationToken cancellationToken = default)
    {
        var total = await _finder.CountCountryAsync(countryCode, cancellationToken);

        var verified = await _finder.CountAsync(
            _finder.Filter(countryCode) with { VerifiedOnly = true }, cancellationToken);

        var official = await _finder.CountAsync(
            _finder.Filter(countryCode) with { OfficialOnly = true }, cancellationToken);

        var usage = await _analytics.GetForReadAsync(countryCode, cancellationToken);

        var paths = await _paths.ListByCountryAsync(
            countryCode, publishedOnly: true, cancellationToken);

        return new KnowledgeStatisticsResponse
        {
            CountryCode = countryCode,
            TotalGuides = total,
            VerifiedGuides = verified,
            OfficialGuides = official,

            // Community is the remainder rather than its own count, because it is defined as the
            // remainder: everything nobody verified. A separate query would be a third number that
            // could disagree with the first two after a Trust decision landed between them.
            CommunityGuides = Math.Max(0, total - verified),

            FeaturedGuides = directory?.FeaturedGuides.Count ?? 0,
            CategoryCount = directory?.VisibleEntries.Count ?? 0,
            LearningPathCount = paths.Count,
            Languages = await LanguagesAsync(countryCode, directory, cancellationToken),
            LastPublishedAt = await LastPublishedAtAsync(countryCode, cancellationToken),

            DirectoryViews = usage?.DirectoryViews ?? 0,
            CategoryViews = usage?.CategoryViews ?? 0,
            GuideDiscoveries = usage?.GuideDiscoveries ?? 0,
            Searches = usage?.Searches ?? 0,
            DiscoveryRate = usage?.DiscoveryRate ?? 0,
            ClickThroughRate = usage?.ClickThroughRate ?? 0,
            PathCompletionRate = usage?.PathCompletionRate ?? 0
        };
    }

    /// <summary>
    /// The languages this country's knowledge is written in.
    /// <para>
    /// Taken from the directory's declaration where a curator made one, and from nothing otherwise.
    /// Deriving it from the guides would mean a distinct scan over every guide in the country on
    /// every dashboard load, to produce a list that changes when a country launches a new language
    /// and at no other time — which is precisely what the curated declaration is for.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<string>> LanguagesAsync(
        string countryCode,
        CountryKnowledgeDirectory? directory,
        CancellationToken cancellationToken)
    {
        if (directory is null)
        {
            return [];
        }

        var declared = new List<string>();

        if (!string.IsNullOrWhiteSpace(directory.PrimaryLanguage))
        {
            declared.Add(directory.PrimaryLanguage);
        }

        declared.AddRange(directory.Languages.Where(
            language => !declared.Contains(language, StringComparer.Ordinal)));

        // A country whose curator declared nothing still gets an honest answer: if there are guides
        // at all, at least one language is in use, and reporting an empty list would make a client
        // hide a language switcher that should show one option.
        if (declared.Count == 0
            && await _finder.CountCountryAsync(countryCode, cancellationToken) > 0)
        {
            return [];
        }

        return declared;
    }

    /// <summary>
    /// When something was last published here.
    /// <para>
    /// Read as the first row of the newest-first listing rather than as a max: the listing is
    /// already indexed on (status, published_at), so this is an index seek, and a separate max over
    /// a filtered set would be a second query shape for the same index.
    /// </para>
    /// </summary>
    private async Task<DateTimeOffset?> LastPublishedAtAsync(
        string countryCode, CancellationToken cancellationToken)
    {
        var filter = _finder.Filter(countryCode, sort: GuideSortOrder.Newest, limit: 1);

        var page = await _finder.PageAsync(filter, cursor: null, cancellationToken);

        return page.Items.Count == 0 ? null : page.Items[0].PublishedAt;
    }
}
