using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Infrastructure.Ranking;
using Diaspora.UnitTests.Recommendations.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Recommendations;

/// <summary>
/// What each shipped strategy actually claims.
/// <para>
/// <b>Every one of these asserts a behaviour somebody would otherwise have to trust.</b> A strategy
/// returns a number between 0 and 1 and the number looks plausible whatever it does — which is
/// exactly why the interesting properties (the pool is normalised against itself, a global candidate
/// is not scored zero, an unverified provider is ranked and not hidden) need to be pinned rather
/// than eyeballed.
/// </para>
/// </summary>
public class RankingStrategyTests
{
    // ── Popularity ───────────────────────────────────────────────────────

    /// <summary>
    /// <b>Scored against the pool rather than a constant.</b> Engagement counts differ by orders of
    /// magnitude between verticals, so an absolute threshold would make one vertical permanently
    /// popular and another permanently obscure — and would need retuning per country as the platform
    /// grew.
    /// </summary>
    [Fact]
    public async Task Popularity_normalises_within_the_pool()
    {
        var strategy = new PopularityRankingStrategy();

        var candidates = new[]
        {
            RecommendationTestData.Candidate(popularity: 1m, engagement: 1000),
            RecommendationTestData.Candidate(popularity: 0m, engagement: 10)
        };

        var scores = await strategy.RankAsync(RecommendationTestData.RankingContext(
            candidates,
            RecommendationTestData.Strategy(
                PopularityRankingStrategy.StrategyKey, RankingStrategyKind.Popularity)));

        scores[0].Value.Should().Be(1m, "the most engaged candidate in the pool is the ceiling");
        scores[1].Value.Should().BeLessThan(0.05m);
    }

    /// <summary>
    /// <b>The reason names the smallest place the platform can honestly claim.</b> Saying "popular in
    /// your neighbourhood" about a national figure is the kind of small dishonesty that costs the
    /// whole explanation its credibility the first time somebody checks.
    /// </summary>
    [Fact]
    public async Task Popularity_claims_the_narrowest_place_it_can_justify()
    {
        var strategy = new PopularityRankingStrategy();
        var profile = RecommendationTestData.Profile(countryCode: "MU", cityName: "Port Louis");

        var candidates = new[]
        {
            RecommendationTestData.Candidate(countryCode: "MU", cityName: "Port Louis"),
            RecommendationTestData.Candidate(countryCode: "MU", cityName: "Curepipe"),
            RecommendationTestData.Candidate(countryCode: "NG")
        };

        var scores = await strategy.RankAsync(RecommendationTestData.RankingContext(
            candidates,
            RecommendationTestData.Strategy(
                PopularityRankingStrategy.StrategyKey, RankingStrategyKind.Popularity),
            RecommendationTestData.Context(profile)));

        scores[0].ReasonCode.Should().Be(RecommendationReasonCode.PopularInCity);
        scores[1].ReasonCode.Should().Be(RecommendationReasonCode.PopularInCountry);
        scores[2].ReasonCode.Should().Be(RecommendationReasonCode.TrendingNow);
    }

    // ── Recency ──────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The half-life is configuration, and this is the test that makes it worth being so.</b> The
    /// same candidate is fresh on the guides surface and stale on the jobs surface, which is the
    /// whole reason weights and parameters are catalogue rows rather than constants.
    /// </summary>
    [Fact]
    public async Task Recency_honours_the_configured_half_life()
    {
        var strategy = new RecencyRankingStrategy();
        var candidate = RecommendationTestData.Candidate(
            publishedAt: RecommendationTestData.Now.AddDays(-30));

        var jobs = await strategy.RankAsync(RecommendationTestData.RankingContext(
            [candidate],
            RecommendationTestData.Strategy(
                RecencyRankingStrategy.StrategyKey,
                RankingStrategyKind.Recency,
                parameters: new Dictionary<string, string>
                {
                    [RecencyRankingStrategy.HalfLifeParameter] = "7"
                })));

        var guides = await strategy.RankAsync(RecommendationTestData.RankingContext(
            [candidate],
            RecommendationTestData.Strategy(
                RecencyRankingStrategy.StrategyKey,
                RankingStrategyKind.Recency,
                parameters: new Dictionary<string, string>
                {
                    [RecencyRankingStrategy.HalfLifeParameter] = "365"
                })));

        jobs[0].Value.Should().BeLessThan(0.1m, "a month-old posting is probably filled");
        guides[0].Value.Should().BeGreaterThan(0.9m, "a month-old guide is current");
    }

    // ── Location ─────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Neighbourhood beats city beats country, and it is a ladder rather than a distance.</b> This
    /// module holds no coordinates, and two neighbourhoods a kilometre apart across a border are not
    /// near each other in any sense a member cares about.
    /// </summary>
    [Fact]
    public async Task Location_scores_the_finest_matching_grain_highest()
    {
        var strategy = new LocationRankingStrategy();

        var profile = RecommendationTestData.Profile(
            countryCode: "MU", cityName: "Port Louis", neighborhoodName: "Plaine Verte");

        var candidates = new[]
        {
            RecommendationTestData.Candidate(
                countryCode: "MU", cityName: "Port Louis", neighborhoodName: "Plaine Verte"),
            RecommendationTestData.Candidate(countryCode: "MU", cityName: "Port Louis"),
            RecommendationTestData.Candidate(countryCode: "MU")
        };

        var scores = await strategy.RankAsync(RecommendationTestData.RankingContext(
            candidates,
            RecommendationTestData.Strategy(
                LocationRankingStrategy.StrategyKey, RankingStrategyKind.Location),
            RecommendationTestData.Context(profile)));

        scores[0].Value.Should().BeGreaterThan(scores[1].Value);
        scores[1].Value.Should().BeGreaterThan(scores[2].Value);

        scores[0].ReasonCode.Should().Be(RecommendationReasonCode.LivesInNeighborhood);
        scores[1].ReasonCode.Should().Be(RecommendationReasonCode.LivesInCity);
        scores[2].ReasonCode.Should().Be(RecommendationReasonCode.LivesInCountry);
    }

    /// <summary>
    /// <b>A candidate that belongs to nowhere scores the floor, not zero.</b> A guide about visa
    /// applications has no country; scoring it zero would rank the platform's most broadly useful
    /// content below a local listing on every location-weighted surface, which on the guides surface
    /// would be actively wrong.
    /// </summary>
    [Fact]
    public async Task Location_gives_global_content_a_floor_rather_than_zero()
    {
        var strategy = new LocationRankingStrategy();

        var scores = await strategy.RankAsync(RecommendationTestData.RankingContext(
            [RecommendationTestData.Candidate(countryCode: null)],
            RecommendationTestData.Strategy(
                LocationRankingStrategy.StrategyKey,
                RankingStrategyKind.Location,
                parameters: new Dictionary<string, string>
                {
                    [LocationRankingStrategy.GlobalFloorParameter] = "0.7"
                })));

        scores[0].Value.Should().Be(0.7m);
    }

    // ── Trust ────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Trust ranks and never gates, and this is the test that stops somebody making it a
    /// filter.</b> A platform in a country where verification has just launched would have an empty
    /// marketplace if the unverified scored zero — and exclusion is a policy's job, where it is
    /// auditable, reversible and visible in the diagnostics.
    /// </summary>
    [Fact]
    public async Task Trust_scores_an_unverified_candidate_above_zero()
    {
        var strategy = new TrustRankingStrategy();

        var scores = await strategy.RankAsync(RecommendationTestData.RankingContext(
            [
                RecommendationTestData.Candidate(isVerified: false, quality: 0m),
                RecommendationTestData.Candidate(isVerified: true, quality: 0m)
            ],
            RecommendationTestData.Strategy(
                TrustRankingStrategy.StrategyKey, RankingStrategyKind.Trust)));

        scores[0].Value.Should().BeGreaterThan(0m, "an unverified candidate is ranked, never hidden");
        scores[1].Value.Should().BeGreaterThan(scores[0].Value);
        scores[1].ReasonCode.Should().Be(RecommendationReasonCode.VerifiedProvider);
    }

    // ── Interest match ───────────────────────────────────────────────────

    /// <summary>
    /// <b>The strongest single match wins rather than the matches being summed.</b> Summing rewards a
    /// candidate for carrying many weak tags over one carrying the exact category a member cares
    /// about, and produces explanations like "because you're interested in six things".
    /// </summary>
    [Fact]
    public async Task Interest_match_takes_the_strongest_single_match()
    {
        var strategy = new InterestMatchRankingStrategy();

        var profile = RecommendationTestData.Profile(interests:
        [
            (InterestKind.Category, "immigration", 1m),
            (InterestKind.Tag, "weak", 0.05m)
        ]);

        var strong = RecommendationTestData.Candidate(categoryKey: "immigration");
        var weak = RecommendationTestData.Candidate(
            categoryKey: "unrelated", attributes: [(InterestKind.Tag, "weak")]);

        var scores = await strategy.RankAsync(RecommendationTestData.RankingContext(
            [strong, weak],
            RecommendationTestData.Strategy(
                InterestMatchRankingStrategy.StrategyKey, RankingStrategyKind.InterestMatch),
            RecommendationTestData.Context(profile)));

        scores[0].Value.Should().BeGreaterThan(scores[1].Value);
        scores[0].ReasonCode.Should().Be(RecommendationReasonCode.MatchesCategory);
    }

    /// <summary>
    /// <b>A member who turned personalization off gets zeroes, not a degraded ranking.</b> The
    /// opt-out has to reach every strategy, and this is the one whose whole input is the profile.
    /// </summary>
    [Fact]
    public async Task Interest_match_returns_nothing_when_personalization_is_off()
    {
        var strategy = new InterestMatchRankingStrategy();

        var profile = RecommendationTestData.Profile(interests:
        [
            (InterestKind.Category, "immigration", 1m)
        ]);

        var preference = RecommendationTestData.Preference(
            profile.UserId, personalizationEnabled: false);

        var scores = await strategy.RankAsync(RecommendationTestData.RankingContext(
            [RecommendationTestData.Candidate(categoryKey: "immigration")],
            RecommendationTestData.Strategy(
                InterestMatchRankingStrategy.StrategyKey, RankingStrategyKind.InterestMatch),
            RecommendationTestData.Context(profile, preference)));

        scores.Should().ContainSingle().Which.Value.Should().Be(0m);
    }

    // ── Cold start ───────────────────────────────────────────────────────

    /// <summary>
    /// <b>The ladder puts guides above marketplace, which is the product opinion the module is
    /// honest about holding.</b> Somebody who has just moved country needs to understand the
    /// paperwork before they need a sofa.
    /// </summary>
    [Fact]
    public async Task Cold_start_ranks_the_ladder_in_order()
    {
        var strategy = new ColdStartRankingStrategy();

        var candidates = new[]
        {
            RecommendationTestData.Candidate(RecommendationTargetType.Guide),
            RecommendationTestData.Candidate(RecommendationTargetType.MarketplaceListing)
        };

        var scores = await strategy.RankAsync(RecommendationTestData.RankingContext(
            candidates,
            RecommendationTestData.Strategy(
                ColdStartRankingStrategy.StrategyKey, RankingStrategyKind.ColdStart),
            RecommendationTestData.Context(isColdStart: true)));

        scores[0].Value.Should().BeGreaterThan(scores[1].Value);
        scores[0].ReasonCode.Should().Be(RecommendationReasonCode.StartHere);
    }

    /// <summary>
    /// <b>Once a member has done anything, the ladder stops.</b> Their own behaviour is a better
    /// guide than a default ordering, and leaving it running would keep pushing them back towards it.
    /// </summary>
    [Fact]
    public async Task Cold_start_contributes_nothing_to_a_known_member()
    {
        var strategy = new ColdStartRankingStrategy();

        var scores = await strategy.RankAsync(RecommendationTestData.RankingContext(
            [RecommendationTestData.Candidate(RecommendationTargetType.Guide)],
            RecommendationTestData.Strategy(
                ColdStartRankingStrategy.StrategyKey, RankingStrategyKind.ColdStart),
            RecommendationTestData.Context(isColdStart: false)));

        scores.Should().ContainSingle().Which.Value.Should().Be(0m);
    }

    /// <summary>
    /// A ladder parameter that parses to nothing falls back to the shipped order rather than
    /// scoring every candidate zero — which would return an empty first page to every new member.
    /// </summary>
    [Fact]
    public async Task Cold_start_falls_back_when_the_configured_ladder_is_nonsense()
    {
        var strategy = new ColdStartRankingStrategy();

        var scores = await strategy.RankAsync(RecommendationTestData.RankingContext(
            [RecommendationTestData.Candidate(RecommendationTargetType.Guide)],
            RecommendationTestData.Strategy(
                ColdStartRankingStrategy.StrategyKey,
                RankingStrategyKind.ColdStart,
                parameters: new Dictionary<string, string>
                {
                    [ColdStartRankingStrategy.LadderParameter] = "nonsense,rubbish"
                }),
            RecommendationTestData.Context(isColdStart: true)));

        scores[0].Value.Should().BeGreaterThan(0m);
    }

    // ── Fallback ─────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Every surviving candidate gets something, so no page is empty for an arithmetic reason.</b>
    /// A member in a newly launched country on a surface whose strategies all need history would
    /// otherwise see nothing at all — not because there is nothing, but because nothing scored.
    /// </summary>
    [Fact]
    public async Task Fallback_scores_everything_above_zero()
    {
        var strategy = new FallbackRankingStrategy();

        var scores = await strategy.RankAsync(RecommendationTestData.RankingContext(
            [
                RecommendationTestData.Candidate(popularity: 0m),
                RecommendationTestData.Candidate(popularity: 1m)
            ],
            RecommendationTestData.Strategy(
                FallbackRankingStrategy.StrategyKey, RankingStrategyKind.Fallback)));

        scores.Should().OnlyContain(score => score.Value > 0m);

        scores[1].Value.Should().BeGreaterThan(
            scores[0].Value, "the tie-break is popularity, so two requests produce the same page");
    }

    // ── The contract every strategy is held to ───────────────────────────

    /// <summary>
    /// <b>A strategy returns one score per candidate, in order.</b> The engine relies on positional
    /// correspondence and discards a strategy that breaks it — because a strategy returning a
    /// different count would silently misattribute every score to the wrong candidate, which is worse
    /// than not running at all.
    /// </summary>
    [Fact]
    public async Task Every_strategy_returns_one_score_per_candidate()
    {
        var candidates = new[]
        {
            RecommendationTestData.Candidate(),
            RecommendationTestData.Candidate(),
            RecommendationTestData.Candidate()
        };

        var strategies = new Modules.Recommendations.Extensibility.Ranking.IRankingStrategy[]
        {
            new PopularityRankingStrategy(),
            new RecencyRankingStrategy(),
            new LocationRankingStrategy(),
            new TrustRankingStrategy(),
            new InterestMatchRankingStrategy(),
            new RuleWeightingRankingStrategy(),
            new DiversityRankingStrategy(),
            new ColdStartRankingStrategy(),
            new FallbackRankingStrategy()
        };

        foreach (var strategy in strategies)
        {
            var scores = await strategy.RankAsync(RecommendationTestData.RankingContext(
                candidates, RecommendationTestData.Strategy(strategy.Key, strategy.Kind)));

            scores.Should().HaveCount(
                candidates.Length, "{0} broke the positional contract", strategy.Key);

            scores.Should().OnlyContain(
                score => score.Value >= 0m && score.Value <= 1m,
                "{0} returned a score outside 0–1, which would out-weigh its own catalogue row",
                strategy.Key);
        }
    }
}
