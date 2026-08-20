namespace Diaspora.Modules.Housing.Domain.Enums;

/// <summary>
/// What moderation has decided about a listing.
/// <para>
/// <b>Flagged is still visible, and that is deliberate.</b> A flag is a queue entry, not a
/// verdict; hiding on the first report would hand any competitor a takedown button. Hidden is
/// the verdict.
/// </para>
/// </summary>
public enum HousingModerationState
{
    /// <summary>Nothing outstanding.</summary>
    Clear = 0,

    /// <summary>Queued for review. Still discoverable.</summary>
    Flagged = 1,

    /// <summary>Removed from discovery by a moderator. The landlord can still see and fix it.</summary>
    Hidden = 2,

    /// <summary>Removed and barred from republication pending a decision on the landlord.</summary>
    Suspended = 3
}
