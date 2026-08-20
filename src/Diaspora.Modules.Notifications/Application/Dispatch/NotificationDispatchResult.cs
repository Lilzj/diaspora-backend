using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Dispatch;

/// <summary>
/// What a dispatch produced.
/// <para>
/// Reported rather than discarded because "nothing happened" has several meanings that an operator
/// investigating a complaint needs to tell apart: no rule matched, the member is opted out, the
/// message was a duplicate, or it went and they missed it.
/// </para>
/// </summary>
public sealed record NotificationDispatchResult
{
    public required UserId RecipientUserId { get; init; }

    /// <summary>One entry per rule that matched and produced a message.</summary>
    public IReadOnlyList<NotificationId> CreatedNotifications { get; init; } = [];

    /// <summary>Channels a message was queued on, across every notification created.</summary>
    public IReadOnlyList<NotificationChannelKind> QueuedChannels { get; init; } = [];

    /// <summary>True when a rule matched but an equivalent message already existed in the window.</summary>
    public bool WasDeduplicated { get; init; }

    /// <summary>True when no rule in the catalogue matches the event and audience.</summary>
    public bool NoRuleMatched { get; init; }

    public static NotificationDispatchResult Nothing(UserId recipientUserId, bool noRuleMatched = false) =>
        new() { RecipientUserId = recipientUserId, NoRuleMatched = noRuleMatched };
}
