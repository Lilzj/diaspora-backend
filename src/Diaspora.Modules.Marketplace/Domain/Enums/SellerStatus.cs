namespace Diaspora.Modules.Marketplace.Domain.Enums;

/// <summary>
/// Whether a seller may trade.
/// <para>
/// <see cref="Suspended"/> is the seller-suspension hook the brief asks for, and it is
/// deliberately a state on this aggregate rather than a Trust concept: Trust decides whether
/// somebody is who they say they are, and the marketplace decides whether they may keep selling
/// here. A suspended seller keeps every listing it has — the listings go dark, and come back if
/// the suspension lifts.
/// </para>
/// </summary>
public enum SellerStatus
{
    Active = 0,

    /// <summary>Barred from listing and selling, reversibly. Existing listings go dark.</summary>
    Suspended = 1,

    /// <summary>Closed by the member. Listings archive; the profile stays for the audit trail.</summary>
    Closed = 2
}
