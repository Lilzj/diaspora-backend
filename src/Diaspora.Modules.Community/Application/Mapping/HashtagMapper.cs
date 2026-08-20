using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Hashtags;

namespace Diaspora.Modules.Community.Application.Mapping;

/// <summary>Projects hashtags.</summary>
public static class HashtagMapper
{
    public static HashtagResponse ToResponse(Hashtag hashtag)
    {
        ArgumentNullException.ThrowIfNull(hashtag);

        return new HashtagResponse
        {
            Slug = hashtag.Slug,
            DisplayText = hashtag.DisplayText,
            UsageCount = hashtag.UsageCount,
            RecentUsageCount = hashtag.RecentUsageCount,
            PrimaryCountryCode = hashtag.PrimaryCountryCode
        };
    }
}
