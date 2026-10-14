using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Guides.Infrastructure.Location;

/// <summary>
/// Contributes the guide count to Location's neighbourhood hub and country page.
/// <para>
/// <c>guides</c> is one of the resource-count keys B4's contract freeze already published — the
/// country page's <c>resourceCounts</c> names <c>providers</c> and <c>guides</c> explicitly — so
/// B8 claims a key reserved for it rather than inventing one. This is the whole of what B8 adds
/// to Location: one class, in this module, and nothing at all in that one.
/// </para>
/// <para>
/// Counting is separate from listing because the costs differ by two orders of magnitude, which
/// is why B4 split the seams. Both counts are a single aggregate over an indexed column, and
/// both are published-only: a hub that counted drafts would advertise knowledge nobody can read.
/// </para>
/// </summary>
public sealed class GuideResourceCounter : ILocationResourceCounter
{
    /// <summary>The resource-count key B4's contract freeze reserved for guides.</summary>
    public const string Key = "guides";

    private readonly IGuideRepository _guides;

    public GuideResourceCounter(IGuideRepository guides) => _guides = guides;

    public string ResourceType => Key;

    public async Task<int> CountInAreaAsync(
        AreaId areaId, HierarchyPath path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        // Prefix match on the materialised path, so "London" counts everything beneath it
        // without a recursive descent. A guide scoped only to its country contributes to no
        // neighbourhood, which is correct: knowledge about the UK is not knowledge about Peckham.
        return await _guides.CountInAreaAsync(path.Value, cancellationToken);
    }

    public async Task<int> CountInCountryAsync(
        CountryCode countryCode, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(countryCode);

        return await _guides.CountInCountryAsync(countryCode.Value, cancellationToken);
    }

    /// <summary>
    /// Where the published guides sit inside a viewport, by the pin each guide carries
    /// (14 Oct 2026). A guide with no pin (most of them, since a guide is usually about a country)
    /// is not on the map.
    /// </summary>
    public Task<MapPointPage> ListPointsAsync(
        MapPointQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filter = new GuideQueryFilter
        {
            Statuses = [GuideStatus.Published],
            CountryCode = query.CountryCode,
            MinLatitude = query.South,
            MaxLatitude = query.North,
            MinLongitude = query.West,
            MaxLongitude = query.East,

            // A public surface: no viewer, so no draft or hidden guide is in scope.
            ViewerId = null,
            ViewerIsStaff = false,
            Sort = GuideSortOrder.Newest
        };

        return MapPointReader.ReadAsync(
            query,
            async (cursor, size, token) =>
            {
                var page = await _guides.QueryAsync(filter, cursor, size, token);
                return (page.Items, page.HasMore ? page.NextCursor : null);
            },
            guide => (guide.Latitude, guide.Longitude),
            GuideLimits.MaxPageSize,
            cancellationToken);
    }
}
