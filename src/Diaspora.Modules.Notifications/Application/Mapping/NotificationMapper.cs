using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.Modules.Notifications.Domain.Catalog;
using Diaspora.Modules.Notifications.Domain.Notifications;
using Diaspora.Modules.Notifications.Domain.Policies;

namespace Diaspora.Modules.Notifications.Application.Mapping;

/// <summary>
/// Turns notifications into wire responses.
/// <para>
/// <b>Static and stateless, so the mapping cannot acquire a dependency.</b> A mapper that could
/// resolve something would eventually resolve a repository, and a list of fifty notifications would
/// become fifty queries. The category's display label is passed in by the caller, which has already
/// loaded the catalogue once for the whole page.
/// </para>
/// </summary>
public static class NotificationMapper
{
    public static NotificationResponse ToResponse(
        Notification notification, IReadOnlyDictionary<string, NotificationCategory> categories)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(categories);

        return new NotificationResponse
        {
            Id = notification.Id.Value,
            Title = notification.Content.Title,
            Body = notification.Content.Body,

            // The label the client already renders. Falls back to the key so a category that shipped
            // after this deployment still shows something a human can read.
            Category = categories.TryGetValue(notification.CategoryKey, out var category)
                ? category.DisplayName
                : notification.CategoryKey,
            CategoryKey = notification.CategoryKey,
            IsRead = notification.Engagement.IsOpened,
            CreatedAt = notification.CreatedAt,
            TargetRoute = notification.PrimaryLink?.Route,
            Priority = notification.Priority.ToString(),
            Status = notification.Status.ToString(),
            IsArchived = notification.IsArchived,
            IsPinned = notification.IsPinned,
            IsStarred = notification.IsStarred,
            ReadAt = notification.Engagement.OpenedAt,
            ExpiresAt = notification.ExpiresAt,
            ImageMediaId = notification.Content.ImageMediaId,
            SourceModule = notification.Source.Module,
            EntityType = notification.PrimaryLink?.EntityType ?? notification.Source.SubjectType,
            EntityId = notification.PrimaryLink?.EntityId ?? notification.Source.SubjectId,
            Actions = notification.Actions
                .Select(action => new NotificationActionResponse
                {
                    Key = action.Key,
                    Label = action.Label,
                    TargetRoute = action.Link?.Route,
                    Style = action.Style.ToString()
                })
                .ToList()
        };
    }

    public static NotificationDetailResponse ToDetail(
        Notification notification,
        IReadOnlyDictionary<string, NotificationCategory> categories,
        bool includeDeliveries)
    {
        ArgumentNullException.ThrowIfNull(notification);

        return new NotificationDetailResponse
        {
            Notification = ToResponse(notification, categories),
            Deliveries = includeDeliveries
                ? notification.Deliveries.Select(ToResponse).ToList()
                : [],
            Attempts = includeDeliveries
                ? notification.Attempts
                    .Select(attempt => new NotificationDeliveryAttemptResponse
                    {
                        Id = attempt.AttemptId,
                        Channel = attempt.Channel.ToString(),
                        AttemptNumber = attempt.AttemptNumber,
                        Succeeded = attempt.Succeeded,
                        ReasonCode = attempt.ReasonCode,
                        Detail = attempt.Detail,
                        AttemptedAt = attempt.AttemptedAt
                    })
                    .ToList()
                : []
        };
    }

    public static NotificationDeliveryResponse ToResponse(NotificationDelivery delivery)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        return new NotificationDeliveryResponse
        {
            Id = delivery.DeliveryId,
            Channel = delivery.Channel.ToString(),
            Status = delivery.Status.ToString(),
            DeliverAfter = delivery.DeliverAfter,
            NextAttemptAt = delivery.NextAttemptAt,
            AttemptCount = delivery.AttemptCount,
            LastAttemptAt = delivery.LastAttemptAt,
            CompletedAt = delivery.CompletedAt,
            ReasonCode = delivery.ReasonCode,
            ProviderMessageId = delivery.ProviderMessageId
        };
    }

    /// <summary>Parses a client's status filter into the domain vocabulary, ignoring what it cannot.</summary>
    public static IReadOnlyList<TEnum> ParseEnums<TEnum>(IReadOnlyList<string>? values)
        where TEnum : struct, Enum
    {
        if (values is not { Count: > 0 })
        {
            return [];
        }

        var parsed = new List<TEnum>(values.Count);

        foreach (var value in values)
        {
            if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var result))
            {
                parsed.Add(result);
            }
        }

        return parsed;
    }

    /// <summary>Parses one enum value, or null. Used where an unrecognised filter should be ignored.</summary>
    public static TEnum? ParseEnum<TEnum>(string? value)
        where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, ignoreCase: true, out var result) ? result : null;
}
