using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Geography;

namespace Diaspora.Modules.CommunityLife.Infrastructure.Location;

/// <summary>
/// Tells the area page how many upcoming or running events have a venue within a circle around a
/// point.
/// <para>
/// Published, public and not hidden, ending now or later — the population the repository's box
/// query already defines for the nearby seam — and measured at the venue's own coordinate. An
/// online event, or one whose venue has no pin, is not within anything.
/// </para>
/// </summary>
public sealed class CommunityLifeCircleCounter : ICircleResourceCounter
{
    /// <summary>The repository's own cap on one box read.</summary>
    private const int BoxReadCap = 200;

    private readonly ICommunityEventRepository _events;
    private readonly IClock _clock;

    public CommunityLifeCircleCounter(ICommunityEventRepository events, IClock clock)
    {
        _events = events;
        _clock = clock;
    }

    public string ResourceType => CommunityLifeResourceCounter.Key;

    public async Task<CircleCount> CountWithinAsync(
        CircleArea circle, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(circle);

        var events = await _events.FindWithinBoxAsync(
            circle.South, circle.North, circle.West, circle.East, _clock.UtcNow, BoxReadCap, cancellationToken);

        var inside = events.Count(communityEvent =>
            circle.Contains(communityEvent.Venue.Latitude, communityEvent.Venue.Longitude));

        return new CircleCount(inside, events.Count >= BoxReadCap);
    }
}
