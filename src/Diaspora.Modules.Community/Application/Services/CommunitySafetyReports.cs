using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Integration;

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
}
