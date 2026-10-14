using Diaspora.Modules.Location.Domain.Search;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Location.Extensibility;

/// <summary>
/// The proximity question Location asks every vertical on a member's behalf.
/// <para>
/// Carries the resolved geography and nothing else — no HTTP context, no claims principal, no
/// module-specific filter. A provider gets a point, a radius and the caller's identity, and
/// answers about its own domain. That narrowness is what makes the contract stable: a Jobs
/// module written against this today keeps compiling when Housing adds a filter Jobs has
/// never heard of.
/// </para>
/// </summary>
public sealed record NearbyResourceRequest
{
    /// <summary>The validated proximity question. Already scope-resolved.</summary>
    public required NearbySearch Search { get; init; }

    /// <summary>The caller, so a provider can exclude their own content or apply visibility.</summary>
    public UserId? UserId { get; init; }

    /// <summary>
    /// The catalogue node the centre resolved to.
    /// <para>
    /// <b>Never set by the nearby endpoint since 14 Oct 2026</b> (Phase 1 of retiring the place
    /// catalogue): a member's discovery query is answered from coordinates, the country and the
    /// typed town, and no provider may narrow by a node. Kept so the contract compiles until
    /// Phase 3 removes it.
    /// </para>
    /// </summary>
    public AreaId? AreaId { get; init; }

    /// <summary>The path of that node. Never set since 14 Oct 2026; see <see cref="AreaId"/>.</summary>
    public HierarchyPath? WithinPath { get; init; }

    /// <summary>
    /// The country the request named, or the member's saved one. Null when the request was only a
    /// point: nothing infers a country from a coordinate any more, and a circle needs none.
    /// </summary>
    public CountryCode? CountryCode { get; init; }

    /// <summary>
    /// Whether <see cref="Search"/> is a circle — answered by its rectangle over a provider's
    /// stored coordinates, with the corners trimmed by distance — rather than a town- or
    /// country-wide question answered by <see cref="CityName"/> and <see cref="CountryCode"/>.
    /// </summary>
    public bool IsCircle => Search.Radius.Scope == Domain.Enums.RadiusScope.Radius;

    /// <summary>
    /// The town the request named, by name, matched case-insensitively against the town an owner
    /// typed. For a town-wide search it is the question; for a circle it only keeps rows that have
    /// no coordinates but name that town.
    /// </summary>
    public string? CityName { get; init; }

    /// <summary>The most items any single provider should return.</summary>
    public required int Limit { get; init; }
}
