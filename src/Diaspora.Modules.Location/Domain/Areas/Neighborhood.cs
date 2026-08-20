using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.ValueObjects;

namespace Diaspora.Modules.Location.Domain.Areas;

/// <summary>
/// A named part of a city: neighbourhood, suburb, ward or area.
/// <para>
/// The rung the product is actually built around. Peckham, Brixton, Rose Hill — the
/// neighbourhood hub is a whole screen, the location chip renders a neighbourhood name
/// before a city name, and "In Your Neighborhood" is the strongest proximity signal the
/// frontend has. It is also the rung the frontend addresses by slug rather than by
/// identifier, which is why <see cref="HubSlug"/> is on this view and nowhere else.
/// </para>
/// </summary>
public sealed class Neighborhood : AdministrativeAreaView
{
    public Neighborhood(AdministrativeArea area) : base(area) { }

    public override AdministrativeLevel Level => AdministrativeLevel.Neighborhood;

    /// <summary>Views the node as a neighbourhood, or returns null when it is not one.</summary>
    public static Neighborhood? For(AdministrativeArea? area) =>
        area?.Level == AdministrativeLevel.Neighborhood ? new Neighborhood(area) : null;

    /// <summary>
    /// The slug the frontend's neighbourhood hub route uses.
    /// <para>
    /// <c>NeighborhoodCard</c> builds its link as
    /// <c>name.toLowerCase().replace(/\s+/g, '-')</c> and pushes <c>/neighborhood/{slug}</c>.
    /// The backend must resolve exactly that string, so the slug is stored rather than
    /// recomputed — a stored slug survives a rename, and a recomputed one turns every
    /// existing link into a 404 the day a name is corrected.
    /// </para>
    /// </summary>
    public LocationSlug HubSlug => Area.Slug;

    /// <summary>0–100, or null until a data source exists. See known gap #1.</summary>
    public int? SafetyScore => Area.SafetyScore;

    /// <summary>0–100, or null until a data source exists.</summary>
    public int? WalkabilityScore => Area.WalkabilityScore;

    /// <summary>Null until a data source exists.</summary>
    public CostOfLivingBand? CostOfLiving => Area.CostOfLiving;

    /// <summary>Whether any liveability index has been populated for this node.</summary>
    public bool HasQualityIndexes =>
        SafetyScore is not null || WalkabilityScore is not null || CostOfLiving is not null;
}
