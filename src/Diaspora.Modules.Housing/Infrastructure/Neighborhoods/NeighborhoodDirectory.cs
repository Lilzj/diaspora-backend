using Diaspora.Modules.Housing.Application.Mapping;
using Diaspora.Modules.Housing.Domain.Neighborhoods;
using Diaspora.Modules.Housing.Integration;

namespace Diaspora.Modules.Housing.Infrastructure.Neighborhoods;

/// <summary>
/// The published neighbourhood directory, backed by Housing's profile store.
/// <para>
/// <b>The one class that makes B11's central claim true rather than aspirational.</b> Jobs,
/// Marketplace, Providers, Guides, Community and anything shipped later can render a neighbourhood
/// — its name, what living there is like, what languages are spoken, how many homes are in it — by
/// depending on <see cref="INeighborhoodDirectory"/> and a flat summary record. None of them
/// references a Housing aggregate, a Housing identifier or a Housing enum, so none of them can be
/// broken by a refactor inside this module.
/// </para>
/// <para>
/// <b>Every method degrades rather than throws.</b> A neighbourhood nobody has described yet is the
/// ordinary case — most neighbourhoods, for a long time — so a missing profile is null and a
/// consumer renders the place without the extras. A consumer should never have to catch anything to
/// show a chip.
/// </para>
/// <para>
/// <b>Reads are untracked and unfiltered by viewer.</b> A neighbourhood profile is public content:
/// there is no draft state a consumer could accidentally surface, because writing unapproves and
/// <c>IsApproved</c> travels on the summary for a consumer that wants to gate on it.
/// </para>
/// </summary>
public sealed class NeighborhoodDirectory : INeighborhoodDirectory
{
    /// <summary>
    /// The ceiling on <see cref="FindAsync"/>. Every consumer of this is rendering a picker or a
    /// discovery strip; one that genuinely wants to page the whole set is asking a Housing question
    /// and should use Housing's own endpoint.
    /// </summary>
    private const int MaxFindLimit = 50;

    private readonly INeighborhoodProfileRepository _profiles;

    public NeighborhoodDirectory(INeighborhoodProfileRepository profiles) =>
        _profiles = profiles;

    public async Task<NeighborhoodSummary?> GetAsync(
        Guid areaId, CancellationToken cancellationToken = default)
    {
        if (areaId == Guid.Empty)
        {
            return null;
        }

        var profile = await _profiles.GetByAreaForReadAsync(areaId, cancellationToken);

        return profile is null ? null : NeighborhoodMapper.ToSummary(profile);
    }

    public async Task<IReadOnlyDictionary<Guid, NeighborhoodSummary>> GetManyAsync(
        IReadOnlyCollection<Guid> areaIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(areaIds);

        if (areaIds.Count == 0)
        {
            return new Dictionary<Guid, NeighborhoodSummary>();
        }

        var profiles = await _profiles.GetManyByAreaAsync(areaIds, cancellationToken);

        return profiles.ToDictionary(
            entry => entry.Key, entry => NeighborhoodMapper.ToSummary(entry.Value));
    }

    public async Task<NeighborhoodSummary?> GetBySlugAsync(
        string slug, string? countryCode = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        var profile = await _profiles.GetBySlugAsync(slug, countryCode, cancellationToken);

        return profile is null ? null : NeighborhoodMapper.ToSummary(profile);
    }

    public async Task<IReadOnlyList<NeighborhoodSummary>> FindAsync(
        string? countryCode = null,
        string? areaPathPrefix = null,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var filter = new NeighborhoodQueryFilter
        {
            CountryCode = countryCode,
            AreaPathPrefix = areaPathPrefix,

            // Places with housing in them, most active first. A consumer rendering a picker wants
            // the neighbourhoods members are actually in, not an alphabetical list of every node
            // in the hierarchy that happens to have a profile.
            WithListingsOnly = true,
            OrderByPopularity = true,
            Limit = Math.Clamp(limit, 1, MaxFindLimit)
        };

        var page = await _profiles.QueryAsync(
            filter, cursor: null, filter.EffectiveLimit, cancellationToken);

        return [.. page.Items.Select(NeighborhoodMapper.ToSummary)];
    }
}
