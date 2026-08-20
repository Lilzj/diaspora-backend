namespace Diaspora.Modules.Notifications.Contracts.V1.Responses;

/// <summary>One slice of the analytics — a channel, a category or a template.</summary>
public sealed record NotificationAnalyticsBreakdownResponse
{
    public required string Key { get; init; }

    public required long Queued { get; init; }

    public required long Delivered { get; init; }

    public required long Opened { get; init; }

    public required long Clicked { get; init; }

    public required long Failed { get; init; }

    public required double DeliveryRate { get; init; }

    public required double OpenRate { get; init; }

    public required double ClickThroughRate { get; init; }
}
