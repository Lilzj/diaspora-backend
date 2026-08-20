using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Location.Extensibility;

/// <summary>
/// Implemented by a vertical to contribute a count to a neighbourhood hub or country page.
/// <para>
/// The neighbourhood hub renders six numbers — providers, jobs, housing, marketplace, events,
/// organizations — and the country page renders five more. The mock computes them by filtering
/// six local arrays, which is exactly the coupling this seam exists to avoid: Location must
/// not know that Housing exists in order to say how many homes are in Peckham.
/// </para>
/// <para>
/// Separate from <see cref="INearbyResourceProvider"/> because counting and listing have
/// wholly different costs. A count is a single aggregate a vertical can serve from an index or
/// a cached rollup; making it a special case of "fetch the items and count them" would make a
/// hub screen fetch several hundred rows to render six integers.
/// </para>
/// </summary>
public interface ILocationResourceCounter
{
    /// <summary>
    /// The key the count appears under. Matches the frontend's <c>resourceCounts</c> keys —
    /// <c>providers</c>, <c>jobs</c>, <c>housing</c>, <c>marketplace</c>, <c>events</c>,
    /// <c>organizations</c>.
    /// </summary>
    string ResourceType { get; }

    /// <summary>
    /// How many of this vertical's things sit in the area.
    /// <para>
    /// Approximate is acceptable and expected. These are headline numbers on a browse screen,
    /// not a ledger, and a cached rollup that is an hour stale is a far better trade than an
    /// exact count that makes the screen slow.
    /// </para>
    /// </summary>
    Task<int> CountInAreaAsync(
        AreaId areaId,
        HierarchyPath path,
        CancellationToken cancellationToken = default);

    /// <summary>How many of this vertical's things sit in a country.</summary>
    Task<int> CountInCountryAsync(
        CountryCode countryCode, CancellationToken cancellationToken = default);
}
