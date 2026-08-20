namespace Diaspora.Modules.CommunityLife.Domain.Enums;

/// <summary>
/// What kind of thing an event points at outside this module.
/// <para>
/// <b>This enum is how B12 satisfies the Jobs and Marketplace requirements without referencing
/// either module.</b> A career fair that lists the vacancies being recruited for, and a festival
/// that names its vendor booths, both need to carry identifiers belonging to a sibling vertical.
/// Storing them as a typed reference keeps the graph acyclic: the organizer supplies the
/// identifier, this module stores it opaquely with a label, and the client resolves it against the
/// vertical that owns it. An edge from here to Jobs or Marketplace would be the first cycle in a
/// graph B2.6 exists to keep acyclic, and it would buy a validation that the owning module can do
/// better at render time.
/// </para>
/// <para>
/// <see cref="Provider"/>, <see cref="Guide"/> and <see cref="Housing"/> are deliberately absent:
/// each of those has a published directory and gets a resolved, validated reference of its own.
/// This type is for the ones that do not.
/// </para>
/// </summary>
public enum LinkedResourceType
{
    /// <summary>A job posting being recruited for. B9 owns it; this stores the identifier.</summary>
    JobPosting = 0,

    /// <summary>A marketplace listing — a vendor booth, merchandise, equipment. B10 owns it.</summary>
    MarketplaceListing = 1,

    /// <summary>Another event — a series, a related session.</summary>
    Event = 2,

    /// <summary>An external page. Rendered as a link and never fetched.</summary>
    ExternalLink = 3
}
