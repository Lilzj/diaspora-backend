namespace Diaspora.Modules.Guides.Domain.Enums;

/// <summary>
/// The named, paged shelves the discovery surface exposes as endpoints of their own.
/// <para>
/// <b>An enum rather than six query types, and the trade is deliberate.</b> The brief names six
/// discovery queries — featured, popular, recently updated, beginner, official, verified — and they
/// differ in exactly one predicate each. Six MediatR handlers would be six copies of the same
/// clamping, filtering, paging and mapping code, five of which would eventually drift. One handler
/// switching on this enum keeps the six endpoints the brief asks for while keeping one
/// implementation of what a page of knowledge is.
/// </para>
/// <para>
/// The distinction from <see cref="KnowledgeSectionKind"/> is audience: a section is a short shelf
/// composed onto a category page by a contributed provider, and a shelf here is a paged endpoint a
/// client scrolls. The two overlap in name and not in purpose.
/// </para>
/// </summary>
public enum KnowledgeShelf
{
    /// <summary>What a curator placed on this country's shelf. Curated order, not queried.</summary>
    Featured = 0,

    /// <summary>Most read.</summary>
    Popular = 1,

    /// <summary>Last edited. The shelf that answers whether any of it is still true.</summary>
    RecentlyUpdated = 2,

    /// <summary>Written for somebody who has just arrived.</summary>
    Beginner = 3,

    /// <summary>Written for somebody who has been here a while and hit something specific.</summary>
    Advanced = 4,

    /// <summary>Carrying an authority badge from Trust. Narrower than verified.</summary>
    Official = 5,

    /// <summary>Checked by Trust.</summary>
    Verified = 6,

    /// <summary>Everything nobody verified. Most of the knowledge on the platform.</summary>
    Community = 7
}
