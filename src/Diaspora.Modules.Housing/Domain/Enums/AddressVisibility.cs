namespace Diaspora.Modules.Housing.Domain.Enums;

/// <summary>
/// How much of a property's address the platform will show, and to whom.
/// <para>
/// <b>This is the module's most important privacy control and it defaults to the safest value.</b>
/// A precise address on a public page tells everyone which door is empty on the first of the
/// month, tells a scammer exactly which property to advertise as their own, and tells anybody who
/// dislikes the current occupant where to find them. The default is
/// <see cref="NeighborhoodOnly"/>, and a landlord raises it deliberately.
/// </para>
/// <para>
/// Ordered least to most revealing, so "at most street level" is a comparison rather than a set.
/// </para>
/// </summary>
public enum AddressVisibility
{
    /// <summary>The neighbourhood and city, and nothing narrower. The default.</summary>
    NeighborhoodOnly = 0,

    /// <summary>The street without a number. Enough to judge a commute, not enough to knock.</summary>
    StreetOnly = 1,

    /// <summary>The full address once a viewing is scheduled. The common real-world answer.</summary>
    OnViewingConfirmed = 2,

    /// <summary>The full address to any signed-in member. For a hostel, where it is public anyway.</summary>
    AuthenticatedMembers = 3,

    /// <summary>The full address to anybody. A business premises, not a home.</summary>
    Public = 4
}
