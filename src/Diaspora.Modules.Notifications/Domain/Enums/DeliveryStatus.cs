namespace Diaspora.Modules.Notifications.Domain.Enums;

/// <summary>
/// What one channel did with one message.
/// <para>
/// Per channel, and that is the point: a message can be sitting unread in the inbox
/// (<see cref="Delivered"/>), suppressed on push because the member is asleep
/// (<see cref="Suppressed"/>) and waiting for tomorrow's digest on email
/// (<see cref="Deferred"/>) all at once. A single status on the notification could express none of
/// that, and every question an operator asks during an incident is about exactly this distinction.
/// </para>
/// </summary>
public enum DeliveryStatus
{
    /// <summary>Accepted for delivery, not yet attempted.</summary>
    Pending = 0,

    /// <summary>Held until a later moment — quiet hours ending, a digest window, a scheduled reminder.</summary>
    Deferred = 1,

    /// <summary>Handed to the transport, waiting for its answer.</summary>
    Sending = 2,

    /// <summary>The transport accepted it.</summary>
    Delivered = 3,

    /// <summary>The attempt failed and another is scheduled.</summary>
    Retrying = 4,

    /// <summary>Every attempt failed, or one failed in a way retrying cannot fix.</summary>
    Failed = 5,

    /// <summary>
    /// Deliberately not sent. The member muted the category, switched the channel off, or is inside
    /// do-not-disturb with a message that does not override it. Recorded rather than discarded,
    /// because "we chose not to tell them" is a fact an operator needs during a complaint.
    /// </summary>
    Suppressed = 6,

    /// <summary>Folded into a digest instead of sent on its own.</summary>
    Digested = 7,

    /// <summary>No channel implementation is registered for this kind. The honest answer for SMS today.</summary>
    Unsupported = 8
}
