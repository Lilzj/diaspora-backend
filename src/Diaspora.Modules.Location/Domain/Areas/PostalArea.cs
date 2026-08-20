using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.ValueObjects;

namespace Diaspora.Modules.Location.Domain.Areas;

/// <summary>
/// A postcode sector or ZIP area.
/// <para>
/// The finest rung the platform issues today. It exists mainly as the join between a typed
/// address and a coordinate: a member entering "SE15 5DQ" should land in Peckham without
/// having to know that is what Peckham is called.
/// </para>
/// </summary>
public sealed class PostalArea : AdministrativeAreaView
{
    public PostalArea(AdministrativeArea area) : base(area) { }

    public override AdministrativeLevel Level => AdministrativeLevel.PostalArea;

    /// <summary>Views the node as a postal area, or returns null when it is not one.</summary>
    public static PostalArea? For(AdministrativeArea? area) =>
        area?.Level == AdministrativeLevel.PostalArea ? new PostalArea(area) : null;

    /// <summary>The code itself, when one was recorded.</summary>
    public PostalCode? Code => Area.PostalCode;
}
