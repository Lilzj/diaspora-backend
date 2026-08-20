using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Guides;

namespace Diaspora.Modules.Guides.Application.Mapping;

/// <summary>
/// Turns a guide into the card a list renders. The card graph carries no sections — the
/// repository's query path deliberately does not load them, and this mapper is written so it
/// never needs them.
/// </summary>
public static class GuideCardMapper
{
    public static GuideCardResponse ToCard(Guide guide)
    {
        ArgumentNullException.ThrowIfNull(guide);

        return new GuideCardResponse
        {
            GuideId = guide.Id.Value,
            Slug = guide.Slug.Value,
            Title = guide.Title.Value,
            Excerpt = guide.Summary?.Excerpt ?? guide.Summary?.Summary,
            CategoryKey = guide.CategoryKey,
            CountryCode = guide.Location.CountryCode,
            CityName = guide.Location.CityName,
            Language = guide.Language.Code,
            Status = GuideWireNames.ToWire(guide.Status),
            Difficulty = GuideWireNames.ToWire(guide.Reading.Difficulty),
            ReadingTimeMinutes = guide.Reading.ReadingTimeMinutes,
            IsVerified = guide.TrustMark.State == GuideVerificationState.Verified,
            IsFeatured = guide.IsFeatured,
            AuthorId = guide.AuthorId.Value,
            PublishedAt = guide.PublishedAt,
            UpdatedAt = guide.ModifiedAt,
            HeroMediaId = HeroOf(guide),
            Tags = guide.Tags.Select(tag => tag.Slug).ToList(),
            Views = guide.Engagement.Views,
            HelpfulCount = guide.Engagement.HelpfulCount,
            CommentCount = guide.Engagement.CommentCount,
            BookmarkCount = guide.Engagement.BookmarkCount
        };
    }

    /// <summary>The hero image, falling back to the cover — the one asset a card shows.</summary>
    private static Guid? HeroOf(Guide guide)
    {
        var references = guide.MediaReferences;

        return references.FirstOrDefault(reference => reference.Role == GuideMediaRole.Hero)?.MediaId
            ?? references.FirstOrDefault(reference => reference.Role == GuideMediaRole.Cover)?.MediaId;
    }
}
