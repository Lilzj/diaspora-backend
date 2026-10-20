using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Integration;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.SharedKernel.Geography;

namespace Diaspora.Modules.Community.Application.Services;

/// <summary>The community's corroborated safety reports, counted per place for other modules.</summary>
public sealed class CommunitySafetyReports : ICommunitySafetyReports
{
    /// <summary>The categories a member files a safety report under.</summary>
    public static readonly IReadOnlyList<PostCategory> SafetyCategories = [PostCategory.Police, PostCategory.Emergency];

    private readonly ICommunityPostRepository _posts;

    public CommunitySafetyReports(ICommunityPostRepository posts) => _posts = posts;

    public async Task<CommunitySafetyReportCounts> CountCorroboratedAsync(
        string hierarchyPath,
        DateTimeOffset asOf,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        var from = asOf - window;

        var counts = await _posts.CountCorroboratedAsync(hierarchyPath, SafetyCategories, from, cancellationToken);

        return new CommunitySafetyReportCounts
        {
            From = from,
            To = asOf,
            ByCategory = SafetyCategories.ToDictionary(
                category => category.ToString(),
                category => counts.TryGetValue(category, out var count) ? count : 0,
                StringComparer.Ordinal)
        };
    }

    public async Task<CommunitySafetyReportCounts> CountCorroboratedWithinAsync(
        double latitude,
        double longitude,
        double radiusKm,
        DateTimeOffset asOf,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        var from = asOf - window;
        var circle = CircleArea.Around(latitude, longitude, radiusKm);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var capped = false;

        foreach (var category in SafetyCategories)
        {
            // The same population as the path-based count: what happened (published, since expired
            // or cancelled — not withdrawn, not moderated away), public, corroborated, in the window.
            var (posts, read) = await CommunityCircleReader.ReadAsync(
                _posts,
                new PostQueryFilter
                {
                    Statuses = [PostStatus.Published, PostStatus.Expired, PostStatus.Cancelled],
                    AllowedVisibility = [VisibilityLevel.Public],
                    ExcludeExpired = false,
                    Category = category,
                    PublishedAfter = from
                },
                circle,
                cancellationToken);

            capped |= read >= CircleCount.MaximumRowsRead;

            counts[category.ToString()] = posts.Count(post =>
                post.VerificationStatus == PostVerificationStatus.CommunityVerified
                && post.PublishedAt is { } published
                && published >= from
                && published <= asOf
                && circle.Contains(post.Location.Latitude, post.Location.Longitude));
        }

        return new CommunitySafetyReportCounts
        {
            From = from,
            To = asOf,
            ByCategory = counts,
            IsLowerBound = capped
        };
    }

    public async Task<CommunitySafetyReportPoints> ListCorroboratedPointsAsync(
        double south,
        double west,
        double north,
        double east,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        // A box across ±180° is two boxes to the repository, which compares longitudes plainly.
        (double West, double East)[] strips = west > east
            ? [(west, 180d), (-180d, east)]
            : [(west, east)];

        var points = new List<CommunitySafetyReportPoint>();
        var capped = false;

        foreach (var category in SafetyCategories)
        {
            foreach (var (stripWest, stripEast) in strips)
            {
                // The same population as both counts: what happened (published, since expired or
                // cancelled — not withdrawn, not moderated away), public, corroborated, in the window.
                var filter = new PostQueryFilter
                {
                    Statuses = [PostStatus.Published, PostStatus.Expired, PostStatus.Cancelled],
                    AllowedVisibility = [VisibilityLevel.Public],
                    ExcludeExpired = false,
                    Category = category,
                    PublishedAfter = from,
                    BoundingBox = (south, stripWest, north, stripEast),
                    Limit = CommunityLimits.MaxFeedPageSize,
                    Cursor = null
                };

                var read = 0;

                while (true)
                {
                    var page = await _posts.QueryAsync(filter, cancellationToken);

                    foreach (var post in page.Items)
                    {
                        read++;

                        if (post.VerificationStatus == PostVerificationStatus.CommunityVerified
                            && post.PublishedAt is { } published
                            && published >= from
                            && published <= to
                            && post.Location.Latitude is { } latitude
                            && post.Location.Longitude is { } longitude
                            && latitude >= south && latitude <= north
                            && longitude >= stripWest && longitude <= stripEast)
                        {
                            points.Add(new CommunitySafetyReportPoint(
                                latitude, longitude, category.ToString(), published));
                        }
                    }

                    if (!page.HasMore || page.NextCursor is null)
                    {
                        break;
                    }

                    if (read >= MapPointReader.MaximumRowsRead)
                    {
                        capped = true;
                        break;
                    }

                    filter = filter with { Cursor = FeedCursor.Decode(page.NextCursor) };
                }
            }
        }

        return new CommunitySafetyReportPoints
        {
            From = from,
            To = to,
            Points = points,
            IsLowerBound = capped
        };
    }
}
