using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Domain.Notifications;

/// <summary>
/// Everything the inbox can be sliced by, in one object.
/// <para>
/// One filter type rather than a dozen repository methods, because the inbox screen combines these
/// freely — unread housing messages from last week, starred anything, everything in a category —
/// and a method per combination is a combinatorial explosion nobody maintains.
/// </para>
/// <para>
/// <see cref="Scope"/> is deliberately not expressible as a status list. "Inbox" means live and not
/// archived; "archived" means archived <em>or</em> expired; "all" means both. Making a client
/// enumerate the statuses would freeze the lifecycle into the wire contract.
/// </para>
/// </summary>
public sealed record NotificationQueryFilter
{
    public required UserId RecipientUserId { get; init; }

    /// <summary><c>inbox</c>, <c>archived</c>, <c>starred</c>, <c>pinned</c> or <c>all</c>.</summary>
    public string Scope { get; init; } = "inbox";

    public bool? UnreadOnly { get; init; }

    public IReadOnlyList<string>? CategoryKeys { get; init; }

    public IReadOnlyList<NotificationPriority>? Priorities { get; init; }

    /// <summary>Free text over title and body. Case-insensitive, provider-translated.</summary>
    public string? Search { get; init; }

    /// <summary>The publishing module — the brief's "sender".</summary>
    public string? SourceModule { get; init; }

    public DateTimeOffset? CreatedAfter { get; init; }

    public DateTimeOffset? CreatedBefore { get; init; }

    /// <summary>Only messages whose deep link points at this kind of thing.</summary>
    public string? EntityType { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
