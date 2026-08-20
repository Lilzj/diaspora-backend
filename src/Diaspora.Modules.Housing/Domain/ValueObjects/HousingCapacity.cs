using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Domain.ValueObjects;

/// <summary>
/// How much property there is, and how many people it is for.
/// <para>
/// <b>Zero bedrooms is valid and is not a missing value.</b> A studio has none by definition, and
/// a model that treated zero as unset would either refuse to list studios or file them as
/// one-bedroom flats. The filter "at least one bedroom" then excludes studios correctly without
/// anybody writing a special case.
/// </para>
/// <para>
/// <b>Bathrooms are a decimal.</b> Half-bathrooms are a normal thing to advertise in several of
/// the markets this platform serves, and rounding 1.5 to either 1 or 2 misdescribes the property
/// in a way a viewer notices on arrival.
/// </para>
/// </summary>
public sealed class HousingCapacity : ValueObject
{
    private HousingCapacity(
        int bedrooms, decimal bathrooms, int? maximumOccupants, int? floorAreaSquareMetres)
    {
        Bedrooms = bedrooms;
        Bathrooms = bathrooms;
        MaximumOccupants = maximumOccupants;
        FloorAreaSquareMetres = floorAreaSquareMetres;
    }

    private HousingCapacity() { }

    /// <summary>Zero for a studio. See the class summary.</summary>
    public int Bedrooms { get; private set; }

    /// <summary>Decimal, so a half-bathroom survives. See the class summary.</summary>
    public decimal Bathrooms { get; private set; }

    /// <summary>How many people may live there. Null when the landlord did not say.</summary>
    public int? MaximumOccupants { get; private set; }

    /// <summary>
    /// Square metres. Metric only, and converted by the client rather than stored twice: two
    /// units in one row is two numbers that can disagree.
    /// </summary>
    public int? FloorAreaSquareMetres { get; private set; }

    /// <summary>A studio: no separate bedroom.</summary>
    public bool IsStudio => Bedrooms == 0;

    /// <summary>
    /// A property whose capacity nobody stated.
    /// <para>
    /// <b>A fresh instance per access, never a cached one</b>, and that is not a style choice.
    /// This is an owned type: EF tracks it as part of whichever aggregate holds it, and a single
    /// shared instance assigned to two listings makes the change tracker treat one as a duplicate
    /// of the other and write nulls into its columns. The platform has carried that defect since
    /// B6; an expression-bodied property is the whole of the fix.
    /// </para>
    /// </summary>
    public static HousingCapacity Unspecified => new(0, 0m, null, null);

    public static Result<HousingCapacity> Create(
        int bedrooms,
        decimal bathrooms,
        int? maximumOccupants = null,
        int? floorAreaSquareMetres = null)
    {
        if (bedrooms < 0 || bedrooms > HousingLimits.MaxBedrooms)
        {
            return Result.Failure<HousingCapacity>(HousingErrors.CapacityInvalid);
        }

        if (bathrooms < 0m || bathrooms > HousingLimits.MaxBathrooms)
        {
            return Result.Failure<HousingCapacity>(HousingErrors.CapacityInvalid);
        }

        if (maximumOccupants is { } occupants
            && (occupants < 1 || occupants > HousingLimits.MaxOccupants))
        {
            return Result.Failure<HousingCapacity>(HousingErrors.OccupancyInvalid);
        }

        if (floorAreaSquareMetres is { } area
            && (area < 1 || area > HousingLimits.MaxFloorArea))
        {
            return Result.Failure<HousingCapacity>(HousingErrors.CapacityInvalid);
        }

        return Result.Success(new HousingCapacity(
            bedrooms, decimal.Round(bathrooms, 1), maximumOccupants, floorAreaSquareMetres));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Bedrooms;
        yield return Bathrooms;
        yield return MaximumOccupants;
        yield return FloorAreaSquareMetres;
    }
}
