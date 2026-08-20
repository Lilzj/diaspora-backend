namespace Diaspora.Modules.Notifications.Contracts.V1.Responses;

/// <summary>
/// One page of an inbox.
/// <para>
/// The platform's cursor shape — <c>items</c>, <c>nextCursor</c>, <c>hasMore</c> — matching every
/// other paged surface on the API so a client's infinite-scroll hook is written once.
/// </para>
/// <para>
/// <c>unreadCount</c> rides along because the badge and the first page are always fetched together,
/// and a second round trip for a number the server already has is a round trip on every app launch.
/// </para>
/// </summary>
public sealed record NotificationPageResponse
{
    public required IReadOnlyList<NotificationResponse> Items { get; init; }

    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }

    public required int UnreadCount { get; init; }
}
