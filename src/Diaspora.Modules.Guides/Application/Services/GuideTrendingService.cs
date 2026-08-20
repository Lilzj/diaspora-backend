using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Guides.Application.Services;

/// <summary>
/// Scores what is earning attention now, as opposed to what earned it once.
/// <para>
/// The score is helpfulness-weighted engagement decayed exponentially by age: helpful votes
/// count most because they are the deliberate signal, views count on a square root because raw
/// views reward clickbait linearly, and the decay half-life is the deployment's trending
/// window. Deliberately explicable — a member asking "why is this trending" can be given the
/// three sentences above.
/// </para>
/// <para>
/// <b>Pure and deterministic.</b> The static core takes the instant as a parameter; the
/// instance wrapper injects the clock and the configured window, so a handler writes one call
/// and a test pins the arithmetic to a fixed moment.
/// </para>
/// </summary>
public sealed class GuideTrendingService
{
    /// <summary>The window used when a deployment configures nothing. Two weeks.</summary>
    public const int DefaultWindowDays = 14;

    private const double HelpfulWeight = 4d;
    private const double BookmarkWeight = 2d;
    private const double CommentWeight = 1.5d;

    private readonly IClock _clock;
    private readonly IOptions<GuideOptions> _options;

    public GuideTrendingService(IClock clock, IOptions<GuideOptions> options)
    {
        _clock = clock;
        _options = options;
    }

    /// <summary>The guide's trending score right now, under the deployment's window.</summary>
    public double Score(Guide guide) =>
        ScoreAt(guide, _clock.UtcNow, _options.Value.TrendingWindowDays);

    /// <summary>
    /// The deterministic core. Engagement first, then decay: a guide published at
    /// <paramref name="at"/> scores its raw engagement, one half-life later it scores half.
    /// Unpublished guides score zero — a draft cannot trend.
    /// </summary>
    public static double ScoreAt(Guide guide, DateTimeOffset at, int windowDays)
    {
        ArgumentNullException.ThrowIfNull(guide);

        if (guide.PublishedAt is not { } publishedAt)
        {
            return 0d;
        }

        var engagement = guide.Engagement;

        var raw = (engagement.HelpfulCount * HelpfulWeight)
            + (engagement.BookmarkCount * BookmarkWeight)
            + (engagement.CommentCount * CommentWeight)
            + Math.Sqrt(Math.Max(0, engagement.Views));

        var ageDays = Math.Max(0d, (at - publishedAt).TotalDays);
        var halfLife = Math.Max(1, windowDays);

        return raw * Math.Pow(0.5d, ageDays / halfLife);
    }
}
