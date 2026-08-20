using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Tags;

/// <summary>
/// Serves the tag cloud.
/// <para>
/// <b>Counted over a bounded sample of the most recently published guides, and that is the
/// definition, not an approximation of a better one.</b> A cloud is a navigational aid: what
/// members should see is which tags are live now, not which accumulated the most rows since the
/// platform started. Sampling the recent window says that honestly and costs a fixed number of
/// pages.
/// </para>
/// <para>
/// The repository publishes no tag aggregation and this module does not reach around it into
/// persistence, so the count is assembled here from the card graph — which already carries tags,
/// so the pages cost nothing extra. One call site to improve when persistence grows a grouped
/// query.
/// </para>
/// </summary>
public sealed class ListGuideTagsQueryHandler
    : IQueryHandler<ListGuideTagsQuery, IReadOnlyList<GuideTagResponse>>
{
    /// <summary>Tags returned when the caller names no ceiling. A chip cloud, not a dictionary.</summary>
    private const int DefaultCloudSize = 50;

    /// <summary>The widest cloud worth rendering.</summary>
    private const int MaxCloudSize = 200;

    /// <summary>Pages of guides sampled. Ten pages of fifty is the recent window.</summary>
    private const int SamplePages = 10;

    private readonly IGuideRepository _guides;
    private readonly GuideAuthorizationService _authorization;

    public ListGuideTagsQueryHandler(
        IGuideRepository guides, GuideAuthorizationService authorization)
    {
        _guides = guides;
        _authorization = authorization;
    }

    public async Task<Result<IReadOnlyList<GuideTagResponse>>> Handle(
        ListGuideTagsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var limit = Math.Clamp(query.Limit ?? DefaultCloudSize, 1, MaxCloudSize);

        var filter = new GuideQueryFilter
        {
            Statuses = [GuideStatus.Published],
            CountryCode = string.IsNullOrWhiteSpace(query.CountryCode)
                ? null
                : query.CountryCode.Trim().ToUpperInvariant(),
            CategoryKey = string.IsNullOrWhiteSpace(query.CategoryKey)
                ? null
                : query.CategoryKey.Trim().ToLowerInvariant(),
            ViewerId = _authorization.CallerId,
            ViewerIsStaff = _authorization.IsStaff,
            Sort = GuideSortOrder.Newest,
            Limit = GuideLimits.MaxPageSize
        };

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        string? cursor = null;

        for (var page = 0; page < SamplePages; page++)
        {
            var results = await _guides.QueryAsync(
                filter, cursor, GuideLimits.MaxPageSize, cancellationToken);

            foreach (var tag in results.Items.SelectMany(guide => guide.Tags))
            {
                counts[tag.Slug] = counts.GetValueOrDefault(tag.Slug) + 1;
            }

            if (!results.HasMore || results.NextCursor is null)
            {
                break;
            }

            cursor = results.NextCursor;
        }

        var cloud = counts
            .OrderByDescending(entry => entry.Value)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .Take(limit)
            .Select(entry => new GuideTagResponse { Tag = entry.Key, Count = entry.Value })
            .ToList();

        return Result.Success<IReadOnlyList<GuideTagResponse>>(cloud);
    }
}
