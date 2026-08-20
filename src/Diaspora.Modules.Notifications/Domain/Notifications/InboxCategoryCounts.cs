namespace Diaspora.Modules.Notifications.Domain.Notifications;

/// <summary>One category's measured counts, for reconciliation.</summary>
public sealed record InboxCategoryCounts
{
    public required int Unread { get; init; }

    public required int Total { get; init; }
}
