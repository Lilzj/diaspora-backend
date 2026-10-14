using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.SharedKernel.Geography;

namespace Diaspora.Modules.Housing.Infrastructure.Location;

/// <summary>
/// Tells the area page how many homes are listed within a circle around a point.
/// <para>
/// <b>Measured from the coordinate a stranger is shown, never the stored one.</b> A home whose
/// address is not public is placed at its fuzzed point, exactly as a search result places it, so a
/// count over a small circle cannot be used to learn more about where a home is than its card says.
/// The rectangle is snapped outward to that precision for the same reason the nearby provider snaps
/// it.
/// </para>
/// <para>
/// The audience is the public one: available or published homes that are on the market, with no
/// viewer, so no draft, hidden or unlisted home is counted.
/// </para>
/// </summary>
public sealed class HousingCircleCounter : ICircleResourceCounter
{
    private readonly IHousingListingRepository _listings;

    public HousingCircleCounter(IHousingListingRepository listings) => _listings = listings;

    public string ResourceType => HousingResourceCounter.Key;

    public async Task<CircleCount> CountWithinAsync(
        CircleArea circle, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(circle);

        var candidates = await _listings.QueryNearestCandidatesAsync(
            PublicWithin(circle), CircleCount.MaximumRowsRead, cancellationToken);

        var inside = candidates.Count(candidate =>
        {
            var (latitude, longitude) = PropertyAddress.PublicCoordinates(
                candidate.Latitude, candidate.Longitude, candidate.Visibility);

            return circle.Contains(latitude, longitude);
        });

        return CircleCount.Of(inside, candidates.Count);
    }

    /// <summary>
    /// Publicly visible homes inside the rectangle around <paramref name="circle"/>, snapped outward
    /// to the published precision. Shared with the rent statistics so a count and a median describe
    /// the same homes.
    /// </summary>
    public static HousingListingQueryFilter PublicWithin(CircleArea circle)
    {
        ArgumentNullException.ThrowIfNull(circle);

        var scale = Math.Pow(10d, PropertyAddress.FuzzedPrecision);

        return new HousingListingQueryFilter
        {
            BoundsSouth = Math.Floor(circle.South * scale) / scale,
            BoundsNorth = Math.Ceiling(circle.North * scale) / scale,
            BoundsWest = Math.Floor(circle.West * scale) / scale,
            BoundsEast = Math.Ceiling(circle.East * scale) / scale,
            Statuses =
            [
                HousingListingStatus.Available,
                HousingListingStatus.Published
            ],
            AvailableOnly = true,
            ViewerId = null,
            ViewerIsStaff = false
        };
    }
}
