using Diaspora.Modules.Location.Domain.Enums;

namespace Diaspora.Modules.Location.Domain.Areas;

/// <summary>
/// A populated place: city, town or village.
/// <para>
/// The most-used rung on the platform by a wide margin. Every vertical filter the frontend
/// sends today is a <c>cityName</c>, every listing DTO carries one, and every "similar
/// listings" panel is scoped by it. That is why <see cref="AdministrativeArea.CountryCode"/>
/// is denormalised onto the node: a city filter must not require walking up the tree to find
/// out which country it is in.
/// </para>
/// </summary>
public sealed class City : AdministrativeAreaView
{
    public City(AdministrativeArea area) : base(area) { }

    public override AdministrativeLevel Level => AdministrativeLevel.City;

    /// <summary>Views the node as a city, or returns null when it is not one.</summary>
    public static City? For(AdministrativeArea? area) =>
        area?.Level == AdministrativeLevel.City ? new City(area) : null;

    /// <summary>
    /// The path prefix every neighbourhood of this city shares. The city-wide radius scope
    /// resolves to exactly this predicate.
    /// </summary>
    public string NeighborhoodPathPrefix => Area.Path.DescendantPrefix;
}
