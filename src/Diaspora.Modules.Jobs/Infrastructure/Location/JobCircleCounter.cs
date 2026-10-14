using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.SharedKernel.Geography;

namespace Diaspora.Modules.Jobs.Infrastructure.Location;

/// <summary>
/// Tells the area page how many open jobs are pinned within a circle around a point.
/// <para>
/// Open, public postings — the default <see cref="JobQueryFilter"/> with no viewer — measured at the
/// pin the employer gave. A posting with no pin, a remote one included, is not within anything.
/// </para>
/// </summary>
public sealed class JobCircleCounter : ICircleResourceCounter
{
    private readonly IJobPostingRepository _postings;

    public JobCircleCounter(IJobPostingRepository postings) => _postings = postings;

    public string ResourceType => JobResourceCounter.Key;

    public async Task<CircleCount> CountWithinAsync(
        CircleArea circle, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(circle);

        var filter = new JobQueryFilter
        {
            MinLatitude = circle.South,
            MaxLatitude = circle.North,
            MinLongitude = circle.West,
            MaxLongitude = circle.East,
            OpenOnly = true
        };

        var read = 0;
        var inside = 0;
        string? cursor = null;

        while (read < CircleCount.MaximumRowsRead)
        {
            var page = await _postings.QueryAsync(filter, cursor, JobLimits.MaxPageSize, cancellationToken);

            read += page.Items.Count;
            inside += page.Items.Count(posting => circle.Contains(posting.Latitude, posting.Longitude));

            if (!page.HasMore || page.NextCursor is null)
            {
                break;
            }

            cursor = page.NextCursor;
        }

        return CircleCount.Of(inside, read);
    }
}
