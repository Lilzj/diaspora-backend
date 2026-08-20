using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.ValueObjects;

namespace Diaspora.Modules.Location.Domain.Spatial;

/// <summary>
/// A catalogue node and how far it is from whatever was asked about.
/// <para>
/// The distance travels with the node rather than being recomputed by the caller, because
/// the provider may have obtained it from a spatial index that the caller cannot reproduce —
/// and because recomputing it in the mapping layer is how two parts of one response come to
/// disagree.
/// </para>
/// </summary>
public sealed record AreaProximityResult
{
    public required AdministrativeArea Area { get; init; }

    /// <summary>Null for queries with no centre, such as a bounding-box sweep.</summary>
    public Distance? Distance { get; init; }
}
