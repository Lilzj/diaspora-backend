using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Location.Domain.Areas;

/// <summary>
/// The base of the level-specific views over <see cref="AdministrativeArea"/>.
/// <para>
/// A view is not a second aggregate and owns no state: it is a typed lens that says "this
/// node is a city" in the type system rather than in a comment, and hangs the behaviour that
/// only applies at that level off the type that has it. <see cref="Neighborhood.HubSlug"/>
/// makes sense on a neighbourhood and nowhere else; putting it on the aggregate would put it
/// on every state and postal sector too.
/// </para>
/// <para>
/// This is the pattern that lets one table serve six named domain concepts without either
/// six tables or six sets of near-identical behaviour. A view never persists — the aggregate
/// beneath it is the only thing a repository ever sees.
/// </para>
/// </summary>
public abstract class AdministrativeAreaView
{
    protected AdministrativeAreaView(AdministrativeArea area)
    {
        ArgumentNullException.ThrowIfNull(area);

        if (area.Level != Level)
        {
            throw new ArgumentException(
                $"Cannot view a {area.Level} as a {Level}.", nameof(area));
        }

        Area = area;
    }

    /// <summary>The node this view reads.</summary>
    public AdministrativeArea Area { get; }

    /// <summary>The level this view requires. Checked at construction.</summary>
    public abstract AdministrativeLevel Level { get; }

    public AreaId Id => Area.Id;

    public PlaceName Name => Area.Name;

    public LocationSlug Slug => Area.Slug;

    public CountryCode CountryCode => Area.CountryCode;

    public HierarchyPath Path => Area.Path;

    public GeoCoordinate? Center => Area.Center;

    public BoundingBox? Extent => Area.Extent;

    public override string ToString() => $"{Level}: {Name.Value}";
}
