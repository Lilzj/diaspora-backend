using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;

namespace Diaspora.Modules.Notifications.Application.Inbox;

/// <summary>
/// The member's own inbox, sliced however the screen asks.
/// <para>
/// One query rather than a dozen, because the screen combines these freely — unread housing messages
/// from last week, starred anything, everything in a category — and a query per combination is a
/// combinatorial explosion nobody maintains. It is also the search surface: the brief's "inbox
/// search" is this query with <c>search</c> set.
/// </para>
/// </summary>
[RequirePermission(NotificationPermissions.InboxRead)]
public sealed record GetInboxQuery : IQuery<NotificationPageResponse>
{
    /// <summary><c>inbox</c>, <c>archived</c>, <c>starred</c>, <c>pinned</c> or <c>all</c>.</summary>
    public string? Scope { get; init; }

    public bool? UnreadOnly { get; init; }

    public IReadOnlyList<string>? Categories { get; init; }

    public IReadOnlyList<string>? Priorities { get; init; }

    public string? Search { get; init; }

    /// <summary>The publishing module. The brief's "sender" filter.</summary>
    public string? SourceModule { get; init; }

    public string? EntityType { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
