using Diaspora.Modules.Notifications.Domain.Enums;

namespace Diaspora.Modules.Notifications.Domain.Analytics;

/// <summary>How the analytics surface is sliced.</summary>
public sealed record NotificationAnalyticsFilter
{
    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    public string? CategoryKey { get; init; }

    public NotificationChannelKind? Channel { get; init; }

    public string? TemplateKey { get; init; }

    /// <summary>Rows returned before aggregation. Bounded so a wide range cannot be a table scan.</summary>
    public int? Limit { get; init; }
}
