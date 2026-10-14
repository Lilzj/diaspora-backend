using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.ValueObjects;
using Diaspora.Modules.Housing.Integration;
using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Services;

/// <summary>
/// The one class in B12 that reads B4's area catalogue, and the one that reads B11's neighbourhood
/// profiles.
/// <para>
/// <b>Every neighbourhood in this module is a real node in Location's hierarchy, and this class is
/// what makes that true rather than aspirational.</b> A client sends an area identifier; this
/// resolves it, confirms it really is at neighbourhood level, and returns the names and the
/// materialised path an event or an organization caches.
/// </para>
/// <para>
/// <b>The ownership split is three-way and this is where it is visible.</b> Location owns the
/// <em>place</em> — its name, slug, hierarchy path and centroid. Housing owns the <em>profile</em> —
/// what living there is like — and publishes it through <c>INeighborhoodDirectory</c>. Community
/// Life owns neither, and holds only the identifier plus a cached name so a card renders without a
/// second call. That is the whole of what the brief's "neighborhood ownership remains with B11's
/// published contracts" costs.
/// </para>
/// <para>
/// <b>Refusing a non-neighbourhood area is the important check.</b> An event placed at city level
/// would never appear in any neighbourhood search, and a client sending a country identifier by
/// mistake would silently get one.
/// </para>
/// </summary>
public sealed class CommunityLifeNeighborhoodResolver
{
    private readonly IAdministrativeAreaRepository _areas;
    private readonly ICountryRepository _countries;
    private readonly INeighborhoodDirectory? _neighborhoods;

    public CommunityLifeNeighborhoodResolver(
        IAdministrativeAreaRepository areas,
        ICountryRepository countries,
        INeighborhoodDirectory? neighborhoods = null)
    {
        _areas = areas;
        _countries = countries;
        _neighborhoods = neighborhoods;
    }

    /// <summary>
    /// The location context an aggregate stores, from a client-supplied area.
    /// <para>
    /// <b>Since 14 Oct 2026 the area is accepted and not written.</b> The owner retired the place
    /// catalogue for filing and discovery: an event, a series or an organization is stored at its
    /// coordinates, its country and the town its organizer typed, and is found by them. Old app
    /// builds still send a neighbourhood area id; it neither narrows nor files anything, and the
    /// context comes back exactly as the caller built it. Kept as a method so every write path
    /// passes through one place that says so, until Phase 3 removes the catalogue.
    /// </para>
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance", "CA1822:Mark members as static",
        Justification = "The seam every write path calls; Phase 3 deletes it with the catalogue.")]
    public Task<Result<CommunityLifeLocationContext>> ResolveAsync(
        CommunityLifeLocationContext context,
        Guid? areaId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = areaId;
        _ = cancellationToken;

        return Task.FromResult(Result.Success(context));
    }

    /// <summary>
    /// B4's materialised path for an area, for a subtree-scoped search.
    /// <para>
    /// Null when the area does not exist, which the caller reads as "no scope" rather than "no
    /// results": a stale identifier in a bookmarked filter should widen the search, not empty it.
    /// </para>
    /// </summary>
    public async Task<string?> AreaPathAsync(
        Guid? areaId, CancellationToken cancellationToken = default)
    {
        if (areaId is not { } id || id == Guid.Empty)
        {
            return null;
        }

        var area = await _areas.GetByIdAsync(AreaId.From(id), cancellationToken);

        return area?.Path.Value;
    }

    /// <summary>Whether a country code names a country B4 knows about.</summary>
    public async Task<bool> CountryExistsAsync(
        string? countryCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Trim().Length != 2)
        {
            return false;
        }

        // Fully qualified: both Location and the platform Domain declare a CountryCode, and the
        // repository takes Location's. An unqualified name here resolves to whichever using
        // directive happens to come first, which is a bug that compiles. B11 records the same trap.
        var code = Modules.Location.Domain.ValueObjects.CountryCode.TryCreate(
            countryCode.Trim().ToUpperInvariant());

        if (code.IsFailure)
        {
            return false;
        }

        var country = await _countries.GetByCodeAsync(code.Value, cancellationToken);

        return country is not null;
    }

    /// <summary>
    /// What B11 says a neighbourhood is like, or null when nobody has written a profile.
    /// <para>
    /// <b>Optional by design.</b> A deployment that does not load Housing has no neighbourhood
    /// profiles at all, so the directory is injected nullable and a missing one degrades to
    /// rendering the place without the extras — which is also the honest answer for most
    /// neighbourhoods for a long time.
    /// </para>
    /// </summary>
    public async Task<NeighborhoodSummary?> ProfileAsync(
        Guid? areaId, CancellationToken cancellationToken = default)
    {
        if (_neighborhoods is null || areaId is not { } id || id == Guid.Empty)
        {
            return null;
        }

        return await _neighborhoods.GetAsync(id, cancellationToken);
    }

    /// <summary>Profiles for several areas at once. The N+1 guard for a neighbourhood strip.</summary>
    public async Task<IReadOnlyDictionary<Guid, NeighborhoodSummary>> ProfilesAsync(
        IReadOnlyCollection<Guid> areaIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(areaIds);

        if (_neighborhoods is null || areaIds.Count == 0)
        {
            return new Dictionary<Guid, NeighborhoodSummary>();
        }

        return await _neighborhoods.GetManyAsync(areaIds, cancellationToken);
    }
}
