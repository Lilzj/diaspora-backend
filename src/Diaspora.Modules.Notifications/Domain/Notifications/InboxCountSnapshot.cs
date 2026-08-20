namespace Diaspora.Modules.Notifications.Domain.Notifications;

/// <summary>
/// What the notification table actually says about one member, measured.
/// <para>
/// The input to counter reconciliation. Maintained counters drift — a process that died between two
/// saves, a write path added without a settlement — and the honest response to that is a periodic
/// recount rather than a claim that it cannot happen.
/// </para>
/// </summary>
public sealed record InboxCountSnapshot
{
    public required int Unread { get; init; }

    public required int Total { get; init; }

    public required int Archived { get; init; }

    public required int Pinned { get; init; }

    public required int Starred { get; init; }

    /// <summary>Per category, keyed on the lower-cased catalogue key.</summary>
    public required IReadOnlyDictionary<string, InboxCategoryCounts> ByCategory { get; init; }
}
