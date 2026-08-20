using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Extensibility.Feeds;
using Diaspora.Modules.Community.Extensibility.Ranking;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Community;

/// <summary>The four built-in rankings, and the contract every ranking must keep.</summary>
public class RankingTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Chronological_puts_the_newest_first()
    {
        var older = Item(Post(publishedAt: Now.AddHours(-5)));
        var newer = Item(Post(publishedAt: Now.AddHours(-1)));

        var ranked = new ChronologicalRanking().Rank([older, newer], Context());

        ranked[0].Reference.Should().Be(newer.Reference);
    }

    [Fact]
    public void Trending_prefers_engagement_over_age()
    {
        var quietAndNew = Post(publishedAt: Now.AddMinutes(-30));
        var busyAndOlder = Post(publishedAt: Now.AddHours(-6));

        var context = Context(reactionScores: new Dictionary<string, double>(StringComparer.Ordinal)
        {
            [busyAndOlder.Reference.Key] = 200d,
            [quietAndNew.Reference.Key] = 1d
        });

        var ranked = new TrendingRanking().Rank([Item(quietAndNew), Item(busyAndOlder)], context);

        ranked[0].Reference.Should().Be(busyAndOlder.Reference);
    }

    [Fact]
    public void Trending_does_not_let_a_minutes_old_post_with_one_reaction_dominate()
    {
        // The two-hour offset exists precisely so this cannot happen.
        var seconds = Post(publishedAt: Now.AddSeconds(-30));
        var established = Post(publishedAt: Now.AddHours(-3));

        var context = Context(reactionScores: new Dictionary<string, double>(StringComparer.Ordinal)
        {
            [seconds.Reference.Key] = 1d,
            [established.Reference.Key] = 60d
        });

        var ranked = new TrendingRanking().Rank([Item(seconds), Item(established)], context);

        ranked[0].Reference.Should().Be(established.Reference);
    }

    [Fact]
    public void A_comment_counts_for_more_than_a_reaction()
    {
        var reacted = Post(publishedAt: Now.AddHours(-2));
        var discussed = Post(publishedAt: Now.AddHours(-2));

        var context = Context(
            reactionScores: new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [reacted.Reference.Key] = 2d
            },
            commentCounts: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [discussed.Reference.Key] = 1
            });

        var ranked = new TrendingRanking().Rank([Item(reacted), Item(discussed)], context);

        ranked[0].Reference.Should().Be(discussed.Reference);
    }

    [Fact]
    public void Trust_weighting_lifts_a_community_verified_post_over_an_unverified_newer_one()
    {
        var verified = Post(publishedAt: Now.AddHours(-4));
        Confirm(verified);

        var unverified = Post(publishedAt: Now.AddHours(-3));

        var ranked = new TrustWeightedRanking().Rank([Item(unverified), Item(verified)], Context());

        ranked[0].Reference.Should().Be(verified.Reference);
    }

    [Fact]
    public void Trust_weighting_demotes_a_disputed_post_without_removing_it()
    {
        var disputed = Post(publishedAt: Now.AddMinutes(-5));
        disputed.RecordDispute(10);

        var ordinary = Post(publishedAt: Now.AddHours(-10));

        var ranked = new TrustWeightedRanking().Rank([Item(disputed), Item(ordinary)], Context());

        ranked.Should().HaveCount(2, "a ranking must never drop an item");
        ranked[^1].Reference.Should().Be(disputed.Reference);
    }

    [Fact]
    public void Location_weighting_floats_an_urgent_nearby_post()
    {
        var nearUrgent = Post(urgency: UrgencyLevel.Urgent, publishedAt: Now.AddHours(-8));
        var nearCalm = Post(publishedAt: Now.AddMinutes(-1));

        var context = Context(distances: new Dictionary<string, double>(StringComparer.Ordinal)
        {
            [nearUrgent.Reference.Key] = 2d,
            [nearCalm.Reference.Key] = 1d
        });

        var ranked = new LocationWeightedRanking().Rank([Item(nearCalm), Item(nearUrgent)], context);

        ranked[0].Reference.Should().Be(nearUrgent.Reference);
    }

    [Fact]
    public void A_post_with_no_distance_sorts_last_rather_than_first()
    {
        var placed = Post();
        var unplaced = Post();

        var context = Context(distances: new Dictionary<string, double>(StringComparer.Ordinal)
        {
            [placed.Reference.Key] = 40d
        });

        var ranked = new LocationWeightedRanking().Rank([Item(unplaced), Item(placed)], context);

        ranked[0].Reference.Should().Be(placed.Reference);
        ranked[^1].DistanceKm.Should().BeNull("an unknown distance is not zero");
    }

    [Theory]
    [InlineData(typeof(ChronologicalRanking))]
    [InlineData(typeof(TrendingRanking))]
    [InlineData(typeof(TrustWeightedRanking))]
    [InlineData(typeof(LocationWeightedRanking))]
    public void No_ranking_ever_drops_or_duplicates_an_item(Type strategyType)
    {
        var strategy = (IRankingStrategy)Activator.CreateInstance(strategyType)!;

        var items = Enumerable.Range(0, 12)
            .Select(offset => Item(Post(publishedAt: Now.AddMinutes(-offset))))
            .ToList();

        var ranked = strategy.Rank(items, Context());

        ranked.Should().HaveCount(items.Count);
        ranked.Select(item => item.Reference.Key).Should().BeEquivalentTo(
            items.Select(item => item.Reference.Key));
    }

    private static void Confirm(CommunityPost post)
    {
        var (total, verified) = Diaspora.Modules.Community.Domain.Policies
            .ConfirmationThresholds.For(post.Urgency);

        for (var index = 0; index < total; index++)
        {
            post.RecordCorroboration(index < verified, 1);
        }
    }

    private static RankingContext Context(
        IReadOnlyDictionary<string, double>? reactionScores = null,
        IReadOnlyDictionary<string, int>? commentCounts = null,
        IReadOnlyDictionary<string, double>? distances = null) =>
        new()
        {
            Request = new FeedRequest { FeedKey = "home", AllowedVisibility = [VisibilityLevel.Public] },
            Now = Now,
            ReactionScores = reactionScores ?? new Dictionary<string, double>(StringComparer.Ordinal),
            CommentCounts = commentCounts ?? new Dictionary<string, int>(StringComparer.Ordinal),
            Distances = distances ?? new Dictionary<string, double>(StringComparer.Ordinal)
        };

    private static FeedItem Item(CommunityPost post) => FeedItem.ForPost(post, 0d);

    private static CommunityPost Post(
        UrgencyLevel urgency = UrgencyLevel.Normal, DateTimeOffset? publishedAt = null)
    {
        var post = CommunityPost.Draft(
            UserId.New(),
            PublishingIdentity.Personal(),
            PostBody.Create("Something happened.").Value,
            null,
            PostCategory.Community,
            urgency,
            ContentAudience.Public,
            PostLocationContext.None,
            publishedAt ?? Now).Value;

        post.Publish(publishedAt ?? Now);

        return post;
    }
}
