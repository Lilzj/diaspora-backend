namespace Diaspora.Modules.Notifications.Domain.Enums;

/// <summary>
/// Where a notification sits in its life.
/// <para>
/// <b>This is the message's status, not a channel's.</b> A notification going to three channels has
/// one of these and three <see cref="DeliveryStatus"/> values, because "did it arrive" is a question
/// about a transport and "has the member dealt with it" is a question about the message.
/// </para>
/// <para>
/// The order is the lifecycle order, and the transition table depends on it.
/// </para>
/// </summary>
public enum NotificationStatus
{
    /// <summary>The message exists. No channel has been asked to do anything yet.</summary>
    Created = 0,

    /// <summary>At least one channel has been asked to deliver it.</summary>
    Queued = 1,

    /// <summary>At least one channel reported success. Not the same as read.</summary>
    Delivered = 2,

    /// <summary>The member read it.</summary>
    Opened = 3,

    /// <summary>The member followed it somewhere. Implies opened.</summary>
    Clicked = 4,

    /// <summary>The member refused it — swiped away, tapped "not interested". Implies read for counting purposes.</summary>
    Dismissed = 5,

    /// <summary>Filed. Out of the inbox, still in the record, still searchable.</summary>
    Archived = 6,

    /// <summary>
    /// Aged out by the retention sweep. Distinct from archived because nobody chose it, and a
    /// member asking "where did that go" deserves a different answer.
    /// </summary>
    Expired = 7,

    /// <summary>Every channel failed permanently. The member was never told.</summary>
    Failed = 8
}
