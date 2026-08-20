using Diaspora.Modules.Location.Domain.Enums;

namespace Diaspora.Modules.Location.Domain.Areas;

/// <summary>
/// A second-level subdivision: a district, county or borough.
/// <para>
/// Optional in most countries and load-bearing in a few. Mauritius organises entirely by
/// district with no state above it, which is precisely why the hierarchy does not require
/// adjacent levels to be populated.
/// </para>
/// </summary>
public sealed class District : AdministrativeAreaView
{
    public District(AdministrativeArea area) : base(area) { }

    public override AdministrativeLevel Level => AdministrativeLevel.District;

    /// <summary>Views the node as a district, or returns null when it is not one.</summary>
    public static District? For(AdministrativeArea? area) =>
        area?.Level == AdministrativeLevel.District ? new District(area) : null;
}
