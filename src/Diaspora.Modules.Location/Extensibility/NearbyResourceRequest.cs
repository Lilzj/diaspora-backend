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
    /// The catalogue node the centre resolved to, when it resolved to one. A provider that
    /// indexes by area rather than by coordinate answers from this without doing geometry.
    /// </summary>
    public AreaId? AreaId { get; init; }

    /// <summary>The path of that node, for providers that filter by hierarchy prefix.</summary>
    public HierarchyPath? WithinPath { get; init; }

    /// <summary>The country the centre falls in.</summary>
    public required CountryCode CountryCode { get; init; }

    /// <summary>
    /// The city the centre falls in, by name.
    /// <para>
    /// Present because every vertical filter the platform has today is a <c>cityName</c>
    /// string, and a provider backed by existing data should not have to acquire a spatial
    /// index before it can answer. It is a migration aid with a long life, not a shortcut.
    /// </para>
    /// </summary>
    public string? CityName { get; init; }

    /// <summary>The most items any single provider should return.</summary>
    public required int Limit { get; init; }
}
