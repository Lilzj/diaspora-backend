namespace Diaspora.Modules.CommunityLife.Domain.Enums;

/// <summary>
/// A moderator's standing decision about an event or an organization.
/// <para>
/// Deliberately orthogonal to the lifecycle. A published event that has been hidden is still
/// published — its organizer can still fix it, its attendees still hold their RSVPs — it is simply
/// not discoverable. Collapsing the two would mean unhiding an event silently republished it.
/// </para>
/// </summary>
public enum CommunityLifeModerationState
{
    /// <summary>Nothing is wrong with it.</summary>
    Clear = 0,

    /// <summary>Reported enough times to warrant a look. Still discoverable.</summary>
    Flagged = 1,

    /// <summary>Removed from discovery. The owner can still see and fix it.</summary>
    Hidden = 2,

    /// <summary>Removed, and republication is barred pending a decision about the owner.</summary>
    Suspended = 3
}
