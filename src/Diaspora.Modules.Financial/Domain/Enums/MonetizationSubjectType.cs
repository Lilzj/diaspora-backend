namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>
/// What a purchase is about, named without this module knowing what any of them are.
/// <para>
/// <b>The list matches the client's own <c>ContentType</c>.</b> Creator Studio already treats boost
/// as one action across eight content types rather than as eight per-vertical features, and this
/// enum is that decision made structural.
/// </para>
/// <para>
/// A subject is a type plus an identifier and nothing else. This module never resolves one, never
/// joins to one and never validates that it exists — doing any of those would need a reference to
/// the module that owns it, which is the one thing the module graph forbids.
/// </para>
/// </summary>
public enum MonetizationSubjectType
{
    /// <summary>The purchase is about the buyer rather than about a thing. A subscription.</summary>
    Member = 1,

    MarketplaceListing = 2,
    HousingListing = 3,
    Job = 4,
    Provider = 5,
    Organization = 6,
    Event = 7,
    Guide = 8,
    Update = 9,
    Community = 10,

    /// <summary>
    /// One member's standing in one organization's membership programme. B18's
    /// <c>ProgrammeMembership</c> identifier, and — as with every other member here — never resolved.
    /// <para>
    /// <b>Added in preference to registering a <c>MembershipTier</c> product in the catalogue.</b> A
    /// catalogue product carries its own price, and a membership tier's price is B18's, per
    /// organization and per tier; a single product could not hold it and one product per tier would
    /// make B15's catalogue a mirror of somebody else's table that drifts the first time a tier is
    /// repriced. The client has sent <c>subjectType: "CommunityMembership"</c> since Phase 12
    /// shipped, so acting on it is the smaller change and the one already on the wire.
    /// </para>
    /// </summary>
    CommunityMembership = 11
}
