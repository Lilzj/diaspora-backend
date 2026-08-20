using Diaspora.Modules.Location.Domain.Enums;

namespace Diaspora.Modules.Location.Domain.Areas;

/// <summary>
/// A first-level country subdivision: a state, province, emirate or canton.
/// <para>
/// The frontend's <c>PlatformLocation.state</c> maps here — "Greater London",
/// "Plaines Wilhems". Note that both of those examples are, strictly, regions rather than
/// states; the frontend uses one field for whatever sits between country and city, and this
/// view exists so the backend can be precise about which of the two a given node actually is
/// while still answering the frontend's single question.
/// </para>
/// </summary>
public sealed class StateProvince : AdministrativeAreaView
{
    public StateProvince(AdministrativeArea area) : base(area) { }

    public override AdministrativeLevel Level => AdministrativeLevel.StateProvince;

    /// <summary>Views the node as a state, or returns null when it is not one.</summary>
    public static StateProvince? For(AdministrativeArea? area) =>
        area?.Level == AdministrativeLevel.StateProvince ? new StateProvince(area) : null;
}
