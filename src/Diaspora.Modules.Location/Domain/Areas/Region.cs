using Diaspora.Modules.Location.Domain.Enums;

namespace Diaspora.Modules.Location.Domain.Areas;

/// <summary>
/// A grouping used for discovery rather than for administration.
/// <para>
/// A region is the rung that exists because people think in it: "the North West", "the
/// Highlands", "Greater London". It rarely matches an administrative boundary and does not
/// have to — its job is that a member who says "near Manchester" gets results from Salford,
/// which no strictly administrative hierarchy would give them.
/// </para>
/// </summary>
public sealed class Region : AdministrativeAreaView
{
    public Region(AdministrativeArea area) : base(area) { }

    public override AdministrativeLevel Level => AdministrativeLevel.Region;

    /// <summary>Views the node as a region, or returns null when it is not one.</summary>
    public static Region? For(AdministrativeArea? area) =>
        area?.Level == AdministrativeLevel.Region ? new Region(area) : null;
}
