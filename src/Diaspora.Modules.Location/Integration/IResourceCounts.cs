using Diaspora.Contracts.Location.V1;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Location.Integration;

/// <summary>
/// How much of the platform is in a place.
/// <para>
/// The counts behind every "12 listings, 3 events" line on a neighbourhood page. B4 does not know
/// what a listing is: each vertical registers an <c>ILocationResourceCounter</c>, and this fans
/// out to whichever ones are loaded. A deployment without Housing reports no housing key rather
/// than a housing count of zero — <b>an absent key means "nobody can say" and a zero means "none
/// here"</b>, and a neighbourhood page that confused the two would tell a member a place was empty
/// when the truth is that the platform was not asked.
/// </para>
/// <para>
/// Published on 21 November 2026. The aggregator behind it had always taken published types, so
/// two modules injected the concrete class and reached into <c>Location.Application.Services</c>
/// for a seam that was already shaped like one.
/// </para>
/// </summary>
public interface IResourceCounts
{
    /// <summary>
    /// What is filed under one node, by resource type.
    /// <para>
    /// Takes the path as well as the identifier because counting is a prefix match: everything
    /// under <c>/GB/greater-london/london/peckham</c> counts for Peckham, including rows filed in
    /// a node below it.
    /// </para>
    /// </summary>
    Task<IReadOnlyDictionary<string, int>> CountInAreaAsync(
        AreaId areaId, HierarchyPath path, CancellationToken cancellationToken = default);

    /// <summary>The same, narrowed to the resource types a caller actually renders.</summary>
    Task<IReadOnlyDictionary<string, int>> CountInAreaAsync(
        AreaId areaId,
        HierarchyPath path,
        IReadOnlyCollection<string>? resourceTypes,
        CancellationToken cancellationToken = default);

    /// <summary>What is filed anywhere in a country.</summary>
    Task<IReadOnlyDictionary<string, int>> CountInCountryAsync(
        CountryCode countryCode, CancellationToken cancellationToken = default);

    /// <summary>The resource types this deployment can actually answer for.</summary>
    IReadOnlyList<string> ResourceTypes { get; }
}
