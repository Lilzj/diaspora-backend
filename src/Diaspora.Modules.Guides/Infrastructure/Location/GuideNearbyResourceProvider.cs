using System.Globalization;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Location.Extensibility;

namespace Diaspora.Modules.Guides.Infrastructure.Location;

/// <summary>
/// Contributes guides to Location's <c>GET /nearby</c>.
/// <para>
/// The resource type is <c>guides</c> — one of the keys B4's contract freeze published, and the
/// one the frontend's housing detail screen already calls separately before assembling its
/// "nearby ecosystem" panel client-side. B8 claims a reserved key rather than inventing one, and
/// adds nothing whatsoever to Location.
/// </para>
/// <para>
/// <b>A guide is jurisdiction-scoped, not point-scoped.</b> It has a country, and optionally a
/// city and an area, but never a coordinate — procedural knowledge about a visa route is not at a
/// latitude. So every item's <c>DistanceKm</c> is zero and ordering is editorial rather than
/// geometric: verified first, then most helpful. Computing a distance from the area centroid
/// would produce a number the client renders as a fact about a document that has no location, and
/// it would disagree with B4's own distances in the same response.
/// </para>
/// </summary>
public sealed class GuideNearbyResourceProvider : INearbyResourceProvider
{
    /// <summary>One of the resource-type keys Location froze. Not a new one.</summary>
    public const string Key = "guides";

    private readonly IGuideRepository _guides;

    public GuideNearbyResourceProvider(IGuideRepository guides) => _guides = guides;

    public string ResourceType => Key;

    public async Task<NearbyResourceGroup> FindNearbyAsync(
        NearbyResourceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // No catalogue node (14 Oct 2026). A circle is the rectangle around it over guides'
        // pins, with unpinned guides kept when they name the requested town; a town- or
        // country-wide search is the town's name and the country.
        var circle = request.IsCircle ? request.Search.Bounds : null;

        var filter = new GuideQueryFilter
        {
            Statuses = [GuideStatus.Published],
            CountryCode = request.CountryCode?.Value,
            CityName = circle is null ? request.CityName : null,
            UnpinnedCityName = circle is null ? null : request.CityName,
            MinLatitude = circle?.South,
            MaxLatitude = circle?.North,
            MinLongitude = circle?.West,
            MaxLongitude = circle?.East,

            // Nearby is a public surface with no viewer audience of its own, so no viewer is
            // supplied and no draft, unlisted or hidden guide can reach it. B8 must not assume a
            // caller's audience on Location's behalf.
            ViewerId = null,
            ViewerIsStaff = false,
            Sort = GuideSortOrder.MostHelpful,
            Limit = Math.Clamp(request.Limit, 1, GuideLimits.MaxPageSize)
        };

        var page = await _guides.QueryAsync(
            filter, cursor: null, filter.EffectiveLimit, cancellationToken);

        var items = page.Items
            .OrderByDescending(guide => guide.TrustMark.State == GuideVerificationState.Verified)
            .ThenByDescending(guide => guide.Engagement.HelpfulCount)
            .Select(guide => new NearbyResourceItem
            {
                Id = guide.Id.Value.ToString(),
                Title = guide.Title.Value,
                Category = guide.CategoryKey,

                // Knowledge has a jurisdiction, not a coordinate. See the class summary.
                DistanceKm = 0d,
                CityName = guide.Location.CityName,
                AreaName = guide.Location.RegionName,
                Payload = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["slug"] = guide.Slug.Value,
                    ["language"] = guide.Language.Code,
                    ["isVerified"] = guide.TrustMark.State == GuideVerificationState.Verified
                        ? bool.TrueString
                        : bool.FalseString,
                    ["isFeatured"] = guide.IsFeatured ? bool.TrueString : bool.FalseString,
                    ["helpfulCount"] = guide.Engagement.HelpfulCount
                        .ToString(CultureInfo.InvariantCulture),
                    ["readingTimeMinutes"] = guide.Reading.ReadingTimeMinutes
                        .ToString(CultureInfo.InvariantCulture)
                }
            })
            .ToList();

        return new NearbyResourceGroup { ResourceType = ResourceType, Items = items };
    }
}
