namespace Diaspora.Modules.Housing.Domain.Enums;

/// <summary>
/// What a piece of attached media is for.
/// <para>
/// Housing stores the role and the Media identifier and never the bytes or the URL - B5's rule,
/// taken as written. <see cref="LeaseDocument"/> is a role, not a lease model: the module records
/// that a document exists and who may see it, and interprets nothing inside it.
/// </para>
/// </summary>
public enum HousingMediaRole
{
    /// <summary>The single image every card renders. At most one per listing.</summary>
    Cover = 0,

    Gallery = 1,
    FloorPlan = 2,
    Video = 3,

    /// <summary>A link to a third-party tour. Stored as a Media reference, never fetched.</summary>
    VirtualTour = 4,

    /// <summary>A certificate, an inventory, an inspection report.</summary>
    Document = 5,

    /// <summary>A tenancy document. Never parsed - see the enum summary.</summary>
    LeaseDocument = 6
}
