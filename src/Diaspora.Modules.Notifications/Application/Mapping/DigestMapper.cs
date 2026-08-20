using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.Modules.Notifications.Domain.Digests;

namespace Diaspora.Modules.Notifications.Application.Mapping;

/// <summary>Turns digests into wire responses.</summary>
public static class DigestMapper
{
    public static NotificationDigestResponse ToResponse(NotificationDigest digest)
    {
        ArgumentNullException.ThrowIfNull(digest);

        return new NotificationDigestResponse
        {
            Id = digest.Id.Value,
            Frequency = digest.Frequency.ToString(),
            Status = digest.Status.ToString(),
            CategoryKey = digest.CategoryKey,
            CountryCode = digest.CountryCode,
            WindowStart = digest.WindowStart,
            WindowEnd = digest.WindowEnd,
            GeneratedAt = digest.GeneratedAt,
            OpenedAt = digest.OpenedAt,
            TotalCount = digest.TotalCount,
            Entries = digest.Entries
                .Select(entry => new DigestEntryResponse
                {
                    NotificationId = entry.NotificationId.Value,
                    CategoryKey = entry.CategoryKey,
                    Title = entry.Title,
                    Summary = entry.Summary,
                    TargetRoute = entry.Route,
                    OccurredAt = entry.OccurredAt
                })
                .ToList()
        };
    }
}
