using Diaspora.Modules.Guides.Application.Services.Snapshots;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Versions;

namespace Diaspora.Modules.Guides.Application.Mapping;

/// <summary>
/// Turns version snapshots into the wire shapes the history screens render. The summary shape
/// carries no content — a history list of fifty versions must not carry fifty documents.
/// </summary>
public static class GuideVersionMapper
{
    /// <summary>One history row.</summary>
    public static GuideVersionSummaryResponse ToSummary(GuideVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);

        return new GuideVersionSummaryResponse
        {
            VersionId = version.Id.Value,
            GuideId = version.GuideId.Value,
            VersionNumber = version.VersionNumber,
            Title = version.Title,
            RevisionNotes = version.RevisionNotes,
            EditorId = version.EditorId.Value,
            PublishedAt = version.PublishedAt,
            WordCount = version.WordCount,
            ReadingTimeMinutes = version.ReadingTimeMinutes
        };
    }

    /// <summary>
    /// The full version, content included. The snapshot is deserialized by the caller — the
    /// mapper stays a pure function, and the caller owns what a corrupt snapshot means.
    /// </summary>
    public static GuideVersionResponse ToResponse(GuideVersion version, GuideSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(snapshot);

        return new GuideVersionResponse
        {
            VersionId = version.Id.Value,
            GuideId = version.GuideId.Value,
            VersionNumber = version.VersionNumber,
            Title = version.Title,
            Summary = version.Summary,
            Language = version.Language,
            CategoryKey = version.CategoryKey,
            RevisionNotes = version.RevisionNotes,
            EditorId = version.EditorId.Value,
            PublishedAt = version.PublishedAt,
            WordCount = version.WordCount,
            ReadingTimeMinutes = version.ReadingTimeMinutes,
            Sections = snapshot.Sections
                .OrderBy(section => section.Order)
                .Select(ToSection)
                .ToList()
        };
    }

    private static GuideSectionResponse ToSection(GuideSectionSnapshot section) => new()
    {
        SectionId = section.SectionId,
        Title = section.Title,
        Order = section.Order,
        Blocks = section.Blocks
            .OrderBy(block => block.Order)
            .Select(ToBlock)
            .ToList()
    };

    private static GuideBlockResponse ToBlock(GuideBlockSnapshot block) => new()
    {
        BlockId = block.BlockId,
        Kind = block.Kind,
        Order = block.Order,
        Content = block.Content,
        MediaId = block.MediaId,
        Metadata = block.Metadata
    };
}
