using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Directories;
using Diaspora.Modules.Guides.Integration;

namespace Diaspora.Modules.Guides.Application.Knowledge.Mapping;

/// <summary>
/// Turns a country directory into the shapes the curator surface and the consuming modules read.
/// <para>
/// The reader-facing country page is not built here — it is composed, from a directory, a
/// catalogue, several shelves and a counter projection, which is more than a mapper should know
/// about. That is <c>KnowledgeDirectoryComposer</c>. This maps the parts that are a straight
/// projection of one aggregate.
/// </para>
/// </summary>
public static class CountryDirectoryMapper
{
    /// <summary>The curator's view: hidden rows, unpublished state, every translation.</summary>
    public static CountryDirectoryDetailResponse ToDetail(
        CountryKnowledgeDirectory directory,
        IReadOnlyDictionary<string, string> labels,
        IReadOnlyDictionary<Guid, GuideCardResponse> cards)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(cards);

        return new CountryDirectoryDetailResponse
        {
            DirectoryId = directory.Id.Value,
            CountryCode = directory.CountryCode,
            DisplayName = directory.DisplayName,
            Summary = directory.Summary,
            PrimaryLanguage = directory.PrimaryLanguage,
            Languages = directory.Languages,
            IsPublished = directory.IsPublished,
            PublishedAt = directory.PublishedAt,
            Revision = directory.Revision,
            Entries =
            [
                .. directory.Entries
                    .OrderBy(entry => entry.Position)
                    .Select(entry => new CountryDirectoryEntryResponse
                    {
                        KnowledgeCategoryKey = entry.KnowledgeCategoryKey,
                        Label = entry.LabelOverride
                            ?? labels.GetValueOrDefault(
                                entry.KnowledgeCategoryKey, entry.KnowledgeCategoryKey),
                        Description = null,
                        Icon = null,
                        Position = entry.Position,

                        // Counts are a composed read. A curator arranging forty rows does not need
                        // forty count queries to drag one of them, and the reader-facing page
                        // computes them where they are actually rendered.
                        GuideCount = 0,
                        IsHidden = entry.IsHidden
                    })
            ],
            FeaturedGuides =
            [
                .. directory.FeaturedGuides
                    .OrderBy(featured => featured.KnowledgeCategoryKey ?? string.Empty,
                        StringComparer.Ordinal)
                    .ThenBy(featured => featured.Position)
                    .Select(featured => new CountryFeaturedGuideResponse
                    {
                        GuideId = featured.GuideId,
                        KnowledgeCategoryKey = featured.KnowledgeCategoryKey,
                        Position = featured.Position,
                        Note = featured.Note,
                        FeaturedAt = featured.FeaturedAt,

                        // Null for a guide unpublished since it was placed — which is exactly the
                        // row a curator most needs to see, because it is the one to remove.
                        Guide = cards.GetValueOrDefault(featured.GuideId)
                    })
            ],
            Localizations =
            [
                .. directory.Localizations
                    .OrderBy(localization => localization.Locale, StringComparer.Ordinal)
                    .Select(localization => new CountryDirectoryLocalizationResponse
                    {
                        Locale = localization.Locale,
                        DisplayName = localization.DisplayName,
                        Summary = localization.Summary
                    })
            ]
        };
    }

    /// <summary>One tile on the browse-by-country grid.</summary>
    public static CountryDirectorySummaryResponse ToSummary(
        CountryKnowledgeDirectory directory, string? locale, int guideCount, long directoryViews)
    {
        ArgumentNullException.ThrowIfNull(directory);

        var (displayName, summary) = directory.Localized(locale);

        return new CountryDirectorySummaryResponse
        {
            CountryCode = directory.CountryCode,
            DisplayName = displayName,
            Summary = summary,
            PrimaryLanguage = directory.PrimaryLanguage,
            IsPublished = directory.IsPublished,
            CategoryCount = directory.VisibleEntries.Count,
            GuideCount = guideCount,
            DirectoryViews = directoryViews
        };
    }

    /// <summary>The consuming-module shape. Flat primitives, counts supplied by the caller.</summary>
    public static CountryKnowledgeSummary ToKnowledgeSummary(
        CountryKnowledgeDirectory directory,
        string? locale,
        int guideCount,
        int learningPathCount)
    {
        ArgumentNullException.ThrowIfNull(directory);

        var (displayName, summary) = directory.Localized(locale);

        return new CountryKnowledgeSummary
        {
            CountryCode = directory.CountryCode,
            DisplayName = displayName,
            Summary = summary,
            PrimaryLanguage = directory.PrimaryLanguage,
            IsPublished = directory.IsPublished,
            GuideCount = guideCount,
            CategoryCount = directory.VisibleEntries.Count,
            LearningPathCount = learningPathCount
        };
    }
}
