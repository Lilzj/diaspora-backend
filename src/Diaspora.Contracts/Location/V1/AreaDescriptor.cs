using Diaspora.SharedKernel.Identity;

namespace Diaspora.Contracts.Location.V1;

/// <summary>
/// A catalogue node as another module reads it.
/// <para>
/// <b>A projection rather than the aggregate, because the aggregate is B4's to change.</b>
/// <c>AdministrativeArea</c> carries a boundary polygon, a provenance, deletion stamps, scores and
/// the methods that relocate a subtree — none of which a vertical rendering "Peckham, London" has
/// any business holding. Handing it over made every consumer a reader of B4's internals, and the
/// read surface that was meant to prevent exactly that was the thing doing it.
/// </para>
/// <para>
/// <b>Everything here is a published type.</b> That is the test for whether a projection is
/// finished: if one field still needs B4's namespace to name, the consumer is still coupled and
/// the projection has only moved the import.
/// </para>
/// </summary>
/// <param name="AreaId">The node's identifier, as verticals store it on their own rows.</param>
/// <param name="Name">The node's name — "Peckham". Already the display form.</param>
/// <param name="Slug">Stable and lower-cased. What a URL carries.</param>
/// <param name="Path">
/// The materialised path, <c>/GB/greater-london/london/peckham</c>. The field that matters for
/// counting: every resource counter on this platform aggregates by prefix-matching it.
/// </param>
/// <param name="Level">
/// How specific the node is. An <see cref="AdministrativeLevel"/> rather than its name, because a
/// consumer comparing against a string would silently stop matching the day a level is renamed.
/// </param>
/// <param name="CountryCode">The catalogue's country for the node, not a caller's claim.</param>
/// <param name="ParentId">Null at the root of a country's hierarchy.</param>
/// <param name="Center">
/// Null for a node the catalogue has no coordinate for, which is most neighbourhoods. A consumer
/// measuring distance has to decide what to do about that rather than be handed a default.
/// </param>
public sealed record AreaDescriptor(
    AreaId AreaId,
    string Name,
    string Slug,
    HierarchyPath Path,
    AdministrativeLevel Level,
    CountryCode CountryCode,
    AreaId? ParentId,
    GeoCoordinate? Center);
