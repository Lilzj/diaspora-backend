using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;

namespace Diaspora.Modules.Notifications.Application.Analytics;

/// <summary>The measured record over a window. Staff only; it names no member.</summary>
[Authorize(Permission = NotificationPermissions.AnalyticsRead)]
public sealed record GetNotificationAnalyticsQuery : IQuery<NotificationAnalyticsResponse>
{
    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    public string? CategoryKey { get; init; }

    public string? Channel { get; init; }

    public string? TemplateKey { get; init; }
}
