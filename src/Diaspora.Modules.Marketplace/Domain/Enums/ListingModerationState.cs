namespace Diaspora.Modules.Marketplace.Domain.Enums;

/// <summary>
/// What moderation has decided about a listing.
/// <para>
/// Separate from <see cref="ListingStatus"/> and from soft deletion on purpose: a moderator
/// hiding a listing must not destroy the seller's own state, so lifting the decision restores
/// exactly what was there. All four values are reversible.
/// </para>
/// </summary>
public enum ListingModerationState
{
    /// <summary>Nothing outstanding.</summary>
    Clear = 0,

    /// <summary>In the queue, still visible. What a report produces.</summary>
    Flagged = 1,

    /// <summary>Off every public surface pending review.</summary>
    Hidden = 2,

    /// <summary>Hidden, and the seller told why. The end of a substantiated fraud report.</summary>
    Suspended = 3
}
