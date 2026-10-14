using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Sections;
using Diaspora.Modules.Providers.Integration;

namespace Diaspora.Modules.Guides.Application.Mapping;

/// <summary>
/// Turns a guide aggregate into the full detail shape the wire reads.
/// <para>
/// <b>Everything cross-cutting is handed in, never fetched here.</b> Provider summaries come
/// from one batched B7 lookup, author standing from one projection read — a mapper that
/// fetched per row would turn one detail page into a dozen round trips, and a mapper that can
/// fetch is a mapper nobody can call from a loop safely.
/// </para>
/// <para>
/// Static throughout: no state, only translation (the B6.0.1 CA1822 discipline — static from
/// the start rather than after the Release build says so).
/// </para>
/// </summary>
public static class GuideMapper
{
    /// <summary>
    /// The full detail response.
    /// <para>
    /// <paramref name="providerSummaries"/> may be missing entries — a directory outage
    /// degrades to bare references — and <paramref name="author"/> may be null when Trust has
    /// never said anything about this author. The trust mark's reason is included only
    /// <paramref name="forAuthor"/>: a revocation reason is feedback, not a public notice.
    /// </para>
    /// </summary>
    public static GuideResponse ToResponse(
        Guide guide,
        IReadOnlyDictionary<Guid, ProviderSummary>? providerSummaries = null,
        GuideAuthorResponse? author = null,
        bool forAuthor = false)
    {
        ArgumentNullException.ThrowIfNull(guide);

        return new GuideResponse
        {
            GuideId = guide.Id.Value,
            Slug = guide.Slug.Value,
            Title = guide.Title.Value,
            Summary = guide.Summary?.Summary,
            Excerpt = guide.Summary?.Excerpt,
            CategoryKey = guide.CategoryKey,
            Language = guide.Language.Code,
            TranslationGroupId = guide.TranslationGroupId,
            Status = GuideWireNames.ToWire(guide.Status),
            Visibility = GuideWireNames.ToWire(guide.Visibility),
            ModerationState = GuideWireNames.ToWire(guide.ModerationState),
            IsFeatured = guide.IsFeatured,
            PublishedAt = guide.PublishedAt,
            FirstPublishedAt = guide.FirstPublishedAt,
            ScheduledPublishAt = guide.ScheduledPublishAt,
            ArchivedAt = guide.ArchivedAt,
            CreatedAt = guide.CreatedAt,
            UpdatedAt = guide.ModifiedAt,
            CountryCode = guide.Location.CountryCode,
            RegionName = guide.Location.RegionName,
            CityName = guide.Location.CityName,
            AreaId = guide.Location.AreaId,
            Latitude = guide.Latitude,
            Longitude = guide.Longitude,
            Difficulty = GuideWireNames.ToWire(guide.Reading.Difficulty),
            ReadingTimeMinutes = guide.Reading.ReadingTimeMinutes,
            EstimatedCompletionMinutes = guide.Reading.EstimatedCompletionMinutes,
            WordCount = guide.WordCount,
            CurrentVersion = guide.CurrentVersion,
            Revision = guide.Revision,
            AuthorId = guide.AuthorId.Value,
            Author = author,
            Sections = guide.Sections.Select(ToSection).ToList(),
            Tags = guide.Tags.Select(tag => tag.Slug).ToList(),
            MediaReferences = guide.MediaReferences.Select(ToMediaReference).ToList(),
            Providers = guide.ProviderReferences
                .Select(reference => ToProviderReference(reference, providerSummaries))
                .ToList(),
            TrustMark = ToTrustMark(guide, forAuthor),
            Engagement = ToEngagement(guide),
            Seo = ToSeo(guide)
        };
    }

    /// <summary>One section, blocks in reading order.</summary>
    public static GuideSectionResponse ToSection(GuideSection section)
    {
        ArgumentNullException.ThrowIfNull(section);

        return new GuideSectionResponse
        {
            SectionId = section.Id,
            Title = section.Title,
            Order = section.Order,
            Blocks = section.Blocks.Select(ToBlock).ToList()
        };
    }

    private static GuideBlockResponse ToBlock(GuideBlock block) => new()
    {
        BlockId = block.Id,
        Kind = GuideWireNames.ToWire(block.Kind),
        Order = block.Order,
        Content = block.Content,
        MediaId = block.MediaId,
        Metadata = block.Metadata
    };

    private static GuideMediaReferenceResponse ToMediaReference(GuideMediaReference reference) =>
        new()
        {
            MediaId = reference.MediaId,
            Role = GuideWireNames.ToWire(reference.Role),
            Order = reference.Order,
            Caption = reference.Caption
        };

    private static GuideProviderReferenceResponse ToProviderReference(
        GuideProviderReference reference,
        IReadOnlyDictionary<Guid, ProviderSummary>? summaries)
    {
        var summary = summaries?.GetValueOrDefault(reference.ProviderId);

        return new GuideProviderReferenceResponse
        {
            ProviderId = reference.ProviderId,
            Role = reference.Role,
            Note = reference.Note,
            Order = reference.Order,
            Name = summary?.Name,
            Slug = summary?.Slug,
            IsVerified = summary?.IsVerified,
            LogoMediaId = summary?.LogoMediaId
        };
    }

    private static GuideTrustMarkResponse ToTrustMark(Guide guide, bool forAuthor) => new()
    {
        State = GuideWireNames.ToWire(guide.TrustMark.State),
        Badge = guide.TrustMark.Badge,

        // The reason is the author's feedback, not the public's notice. A public profile that
        // said "revoked for inaccuracy" would be a defamation risk dressed as transparency.
        Reason = forAuthor ? guide.TrustMark.Reason : null,
        VerifiedAt = guide.TrustMark.VerifiedAt
    };

    private static GuideEngagementResponse ToEngagement(Guide guide) => new()
    {
        Views = guide.Engagement.Views,
        UniqueViews = guide.Engagement.UniqueViews,
        HelpfulCount = guide.Engagement.HelpfulCount,
        NotHelpfulCount = guide.Engagement.NotHelpfulCount,
        BookmarkCount = guide.Engagement.BookmarkCount,
        ShareCount = guide.Engagement.ShareCount,
        CommentCount = guide.Engagement.CommentCount
    };

    private static GuideSeoResponse? ToSeo(Guide guide)
    {
        var seo = guide.Seo;

        if (seo.MetaTitle is null
            && seo.MetaDescription is null
            && seo.CanonicalUrl is null
            && seo.Keywords is null)
        {
            return null;
        }

        return new GuideSeoResponse
        {
            MetaTitle = seo.MetaTitle,
            MetaDescription = seo.MetaDescription,
            CanonicalUrl = seo.CanonicalUrl,
            Keywords = seo.Keywords
        };
    }
}
