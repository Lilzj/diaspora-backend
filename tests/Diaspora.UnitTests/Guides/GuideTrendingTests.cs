using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Options;
using Diaspora.UnitTests.Common;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.UnitTests.Guides;

/// <summary>
/// What is earning attention now, as opposed to what earned it once.
/// <para>
/// The score is deliberately explicable: helpful votes count most because they are the
/// deliberate signal, views count on a square root because raw views reward clickbait
/// linearly, and everything decays exponentially with a half-life of the deployment's trending
/// window. A member who asks "why is this trending" can be given those three sentences, which
/// is a property an opaque model would not have.
/// </para>
/// <para>
/// <b>The core is static and takes the instant as a parameter.</b> That is what makes a
/// trending list reproducible: the same candidates at the same moment rank the same way in a
/// test as in production, and a support conversation about yesterday's ordering is answerable.
/// </para>
/// </summary>
public class GuideTrendingTests
{
    private static readonly DateTimeOffset Published = GuideLifecycleTests.Now;

    private const int Window = GuideTrendingService.DefaultWindowDays;

    [Fact]
    public void A_draft_cannot_trend()
    {
        var guide = GuideLifecycleTests.Drafted();

        guide.ApplyVoteDelta(500, 0);
        guide.ApplyBookmarkDelta(500);

        GuideTrendingService.ScoreAt(guide, Published, Window).Should().Be(0d,
            "an unpublished guide has no readers, whatever its counters say");
    }

    [Fact]
    public void The_score_at_a_fixed_instant_is_the_same_every_time_it_is_asked()
    {
        var guide = Scored(helpful: 10, views: 400, bookmarks: 5, comments: 2);

        var at = Published.AddDays(3);

        var first = GuideTrendingService.ScoreAt(guide, at, Window);
        var second = GuideTrendingService.ScoreAt(guide, at, Window);

        second.Should().Be(first, "a trending list an operator cannot reproduce cannot be explained");
    }

    [Fact]
    public void A_guide_scored_at_the_instant_it_published_scores_its_raw_engagement()
    {
        // 4 per helpful vote, 2 per bookmark, 1.5 per comment, and the square root of views.
        var guide = Scored(helpful: 10, views: 400, bookmarks: 5, comments: 2);

        GuideTrendingService.ScoreAt(guide, Published, Window)
            .Should().BeApproximately((10 * 4d) + (5 * 2d) + (2 * 1.5d) + 20d, 0.000001d);
    }

    [Fact]
    public void One_helpful_vote_outweighs_fifteen_anonymous_views()
    {
        // Views are what a clickbait title buys; a helpful vote is what somebody chose to say
        // after reading. Weighting them the same is how a knowledge base rots.
        var voted = Scored(helpful: 1, views: 0);
        var viewed = Scored(helpful: 0, views: 15);

        GuideTrendingService.ScoreAt(voted, Published, Window)
            .Should().BeGreaterThan(GuideTrendingService.ScoreAt(viewed, Published, Window));
    }

    [Fact]
    public void Views_are_counted_on_a_square_root_so_a_hundred_times_the_traffic_is_ten_times_the_score()
    {
        var modest = Scored(views: 100);
        var viral = Scored(views: 10_000);

        var modestScore = GuideTrendingService.ScoreAt(modest, Published, Window);
        var viralScore = GuideTrendingService.ScoreAt(viral, Published, Window);

        viralScore.Should().BeApproximately(modestScore * 10d, 0.000001d,
            "raw views must not reward volume linearly");
    }

    [Fact]
    public void The_score_halves_over_one_trending_window()
    {
        var guide = Scored(helpful: 10, views: 400);

        var fresh = GuideTrendingService.ScoreAt(guide, Published, Window);
        var oneWindowOn = GuideTrendingService.ScoreAt(guide, Published.AddDays(Window), Window);

        oneWindowOn.Should().BeApproximately(fresh / 2d, 0.000001d,
            "the window is a half-life, which is what makes the decay explicable");

        GuideTrendingService.ScoreAt(guide, Published.AddDays(Window * 2), Window)
            .Should().BeApproximately(fresh / 4d, 0.000001d);
    }

    [Fact]
    public void The_score_only_ever_falls_as_a_guide_ages()
    {
        var guide = Scored(helpful: 10, views: 400, bookmarks: 3, comments: 4);

        var previous = double.MaxValue;

        foreach (var day in Enumerable.Range(0, 60))
        {
            var score = GuideTrendingService.ScoreAt(guide, Published.AddDays(day), Window);

            score.Should().BeLessThan(previous);
            previous = score;
        }

        previous.Should().BeGreaterThan(0d, "decay approaches zero without ever reaching it");
    }

    [Fact]
    public void A_younger_guide_with_the_same_engagement_outranks_an_older_one()
    {
        var older = Scored(helpful: 10, views: 400);
        var younger = Scored(helpful: 10, views: 400, publishedAt: Published.AddDays(10));

        var at = Published.AddDays(12);

        GuideTrendingService.ScoreAt(younger, at, Window)
            .Should().BeGreaterThan(GuideTrendingService.ScoreAt(older, at, Window),
                "trending is about now, and the older guide already had its turn");
    }

    [Fact]
    public void A_clock_running_behind_the_publication_does_not_inflate_the_score()
    {
        // Clock skew between hosts is ordinary; a negative age raised to a negative power would
        // hand a guide an unbounded score.
        var guide = Scored(helpful: 10, views: 400);

        GuideTrendingService.ScoreAt(guide, Published.AddHours(-6), Window)
            .Should().Be(GuideTrendingService.ScoreAt(guide, Published, Window));
    }

    [Fact]
    public void A_window_of_zero_or_less_falls_back_to_a_single_day_rather_than_dividing_by_it()
    {
        var guide = Scored(helpful: 10, views: 400);

        GuideTrendingService.ScoreAt(guide, Published.AddDays(1), 0)
            .Should().Be(GuideTrendingService.ScoreAt(guide, Published.AddDays(1), 1));

        GuideTrendingService.ScoreAt(guide, Published.AddDays(1), -14)
            .Should().Be(GuideTrendingService.ScoreAt(guide, Published.AddDays(1), 1));
    }

    [Fact]
    public void A_guide_nobody_engaged_with_scores_zero_however_fresh_it_is()
    {
        var guide = GuideLifecycleTests.Published();

        GuideTrendingService.ScoreAt(guide, Published, Window).Should().Be(0d);
    }

    [Fact]
    public void The_injected_service_asks_the_static_core_with_the_clock_and_the_configured_window()
    {
        var guide = Scored(helpful: 10, views: 400, bookmarks: 2);

        var clock = new FakeClock(Published.AddDays(5));
        var options = Options.Create(new GuideOptions { TrendingWindowDays = 7 });

        new GuideTrendingService(clock, options).Score(guide)
            .Should().Be(GuideTrendingService.ScoreAt(guide, clock.UtcNow, 7),
                "a handler writes one call and a test pins the arithmetic to a fixed moment");
    }

    [Fact]
    public void The_default_window_is_the_one_a_deployment_gets_when_it_configures_nothing()
    {
        new GuideOptions().TrendingWindowDays.Should().Be(GuideTrendingService.DefaultWindowDays);
    }

    private static Guide Scored(
        int helpful = 0,
        long views = 0,
        int bookmarks = 0,
        int comments = 0,
        DateTimeOffset? publishedAt = null)
    {
        var guide = GuideLifecycleTests.Drafted();

        guide.Publish(publishedAt ?? Published).IsSuccess.Should().BeTrue();

        if (helpful > 0)
        {
            guide.ApplyVoteDelta(helpful, 0);
        }

        if (bookmarks > 0)
        {
            guide.ApplyBookmarkDelta(bookmarks);
        }

        if (comments > 0)
        {
            guide.ApplyCommentCount(comments);
        }

        for (var view = 0; view < views; view++)
        {
            guide.RecordView();
        }

        return guide;
    }
}
