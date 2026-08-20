namespace Diaspora.Modules.Community.Domain.Enums;

/// <summary>
/// What a moderator did. Names verbatim from the client, which renders each as a queue button.
/// <para>
/// <see cref="Suspend"/> is present because the client declares it, and is deliberately
/// <b>not</b> implemented as an account action here: suspending a member is Identity business,
/// and B6 recording that a moderator decided to suspend is not the same as B6 doing it. The
/// decision is stored and an integration event is published; Identity acts on it.
/// </para>
/// </summary>
public enum ModerationActionType
{
    /// <summary>The content is fine. Clears open reports.</summary>
    Approve = 0,

    /// <summary>The content breaches the guidelines and is removed.</summary>
    Reject = 1,

    /// <summary>Hidden from feeds; the author still sees it.</summary>
    Hide = 2,

    Archive = 3,

    Pin = 4,

    Unpin = 5,

    /// <summary>Recorded as a decision and published. Acted on by Identity, not by B6.</summary>
    Suspend = 6,

    /// <summary>Undoes a hide, a removal or an archive.</summary>
    Restore = 7
}
