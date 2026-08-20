using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Location.Domain.Profiles;

/// <summary>
/// How a member wants geography to behave for them: how far "nearby" reaches, in what unit
/// distances are shown, and how precisely their own position may be disclosed.
/// <para>
/// A value object owned by <see cref="LocationProfile"/> rather than a table of key-value
/// preference rows. Three typed fields that the domain actually reasons about beat an
/// untyped bag every time: <see cref="SearchRadius"/> is read by every hyperlocal query and
/// <see cref="CoordinatePrecision"/> gates every coordinate that leaves the process, and
/// neither can afford a string lookup that might miss.
/// </para>
/// <para>
/// <b>On precision.</b> Identity owns the member's privacy setting; this carries the value
/// so that Location can enforce it without calling Identity on the path of every response.
/// A stale copy fails safe: the field defaults to <see cref="CoordinatePrecision.Exact"/>
/// only because that is the platform default, and any Identity change re-projects here.
/// </para>
/// </summary>
public sealed class GeographicPreference : ValueObject
{
    /// <summary>
    /// An owned navigation rather than a scalar, so EF cannot bind it through the constructor
    /// — hence the private setters and the parameterless constructor below. That is the cost
    /// of modelling the radius honestly as scope-plus-distance instead of as the client's
    /// single signed number.
    /// </summary>
    public SearchRadius SearchRadius { get; private set; } = default!;

    public DistanceUnit DistanceUnit { get; private set; }

    public CoordinatePrecision CoordinatePrecision { get; private set; }

    /// <summary>The country a member browses by default, when it differs from where they are.</summary>
    public CountryCode? PreferredCountry { get; private set; }

    /// <summary>The catalogue node a member browses by default.</summary>
    public SharedKernel.Identity.AreaId? PreferredAreaId { get; private set; }

    // EF Core constructor.
    private GeographicPreference() { }

    private GeographicPreference(
        SearchRadius searchRadius,
        DistanceUnit distanceUnit,
        CoordinatePrecision coordinatePrecision,
        CountryCode? preferredCountry,
        SharedKernel.Identity.AreaId? preferredAreaId)
    {
        SearchRadius = searchRadius;
        DistanceUnit = distanceUnit;
        CoordinatePrecision = coordinatePrecision;
        PreferredCountry = preferredCountry;
        PreferredAreaId = preferredAreaId;
    }

    /// <summary>
    /// The defaults a new profile starts with. The 5 km radius is the frontend store's own
    /// initial value, asserted equal by test so the two cannot drift.
    /// </summary>
    public static GeographicPreference Default => new(
        SearchRadius.Default,
        DistanceUnit.Kilometers,
        CoordinatePrecision.Exact,
        null,
        null);

    public GeographicPreference WithSearchRadius(SearchRadius radius)
    {
        ArgumentNullException.ThrowIfNull(radius);
        return new GeographicPreference(
            radius, DistanceUnit, CoordinatePrecision, PreferredCountry, PreferredAreaId);
    }

    public GeographicPreference WithDistanceUnit(DistanceUnit unit) =>
        new(SearchRadius, unit, CoordinatePrecision, PreferredCountry, PreferredAreaId);

    public GeographicPreference WithCoordinatePrecision(CoordinatePrecision precision) =>
        new(SearchRadius, DistanceUnit, precision, PreferredCountry, PreferredAreaId);

    public GeographicPreference WithPreferredCountry(CountryCode? country) =>
        new(SearchRadius, DistanceUnit, CoordinatePrecision, country, PreferredAreaId);

    public GeographicPreference WithPreferredArea(SharedKernel.Identity.AreaId? areaId) =>
        new(SearchRadius, DistanceUnit, CoordinatePrecision, PreferredCountry, areaId);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return SearchRadius;
        yield return DistanceUnit;
        yield return CoordinatePrecision;
        yield return PreferredCountry;
        yield return PreferredAreaId;
    }
}
