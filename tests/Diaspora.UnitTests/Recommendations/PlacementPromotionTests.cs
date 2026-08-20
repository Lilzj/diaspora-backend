using Diaspora.Modules.Recommendations.Application.Consumers.Financial;
using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Infrastructure.Ranking;
using Diaspora.UnitTests.Recommendations.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Recommendations;

/// <summary>
/// PSR-1.1's Recommendation ↔ Financial integration.
/// <para>
/// <b>B15 published <c>PlacementActivatedIntegrationEvent</c> with a <c>PriorityWeight</c> for this
/// module to read, and stated so in its own event catalogue — "B14 decides weighting, ranking and
/// personalization; B15 never learns what it did."</b> Nothing consumed it for two phases. These
/// tests pin the behaviour that closes it, and in particular they pin the property that makes the
/// whole arrangement defensible: a paid position and an editorial one are two different fields, score
/// through two different rules, and explain themselves with two different reason codes.
/// </para>
/// </summary>
public class PlacementPromotionTests
{
    // ── The aggregate ────────────────────────────────────────────────────

    /// <summary>
    /// <b>Promotion does not touch <c>IsFeatured</c>, which is the line B15 drew and this integration
    /// had to honour.</b> If a placement set the featured flag, "did this rank because it is good or
    /// because it was paid for" would become permanently unanswerable — B15's words, and the reason
    /// the candidate carries two fields rather than one.
    /// </summary>
    [Fact]
    public void A_paid_placement_never_sets_the_editorial_featured_flag()
    {
        var candidate = RecommendationTestData.Candidate(isFeatured: false);

        candidate.Promote(0.8m, RecommendationTestData.Now.AddDays(7));

        candidate.PromotionWeight.Should().Be(0.8m);
        candidate.IsFeatured.Should().BeFalse("a purchase is not an editorial decision");
    }

    /// <summary>
    /// <b>A weight above the ceiling is clamped rather than rejected.</b> Refusing it would put this
    /// module in the position of vetoing a purchase B15 has already taken money for; honouring it
    /// unclamped would let one mispriced product outrank every organic result on the platform.
    /// </summary>
    [Fact]
    public void A_weight_beyond_the_ceiling_is_clamped()
    {
        var candidate = RecommendationTestData.Candidate();

        candidate.Promote(99m);

        candidate.PromotionWeight.Should().Be(RecommendationCandidate.MaximumPromotionWeight);
    }

    /// <summary>
    /// <b>Two overlapping placements are two purchases, not a doubled weight.</b> Summing would make
    /// a member who bought twice by accident outrank one who bought once by design.
    /// </summary>
    [Fact]
    public void A_second_activation_replaces_rather_than_accumulates()
    {
        var candidate = RecommendationTestData.Candidate();

        candidate.Promote(0.5m);
        candidate.Promote(0.3m);

        candidate.PromotionWeight.Should().Be(0.3m);
    }

    /// <summary>
    /// Ending clears the window as well as the weight. A cleared weight with a live window left
    /// behind would read, to anything inspecting the row, as a promotion running and worth nothing.
    /// </summary>
    [Fact]
    public void Ending_a_placement_clears_both_the_weight_and_the_window()
    {
        var candidate = RecommendationTestData.Candidate(
            promotionWeight: 0.8m, promotedUntil: RecommendationTestData.Now.AddDays(7));

        candidate.EndPromotion();

        candidate.PromotionWeight.Should().Be(0m);
        candidate.PromotedUntil.Should().BeNull();
        candidate.IsPromotedAt(RecommendationTestData.Now).Should().BeFalse();
    }

    /// <summary>
    /// <b>The window is what closes B15's ten-minute sweep gap.</b> A placement that lapsed a minute
    /// ago has not yet had its end event published — the lifecycle sweep runs every ten minutes — so
    /// without this the member would keep the position they stopped paying for until the sweep
    /// noticed.
    /// </summary>
    [Fact]
    public void An_expired_placement_stops_counting_before_its_end_event_arrives()
    {
        var candidate = RecommendationTestData.Candidate(
            promotionWeight: 0.9m, promotedUntil: RecommendationTestData.Now.AddMinutes(-1));

        candidate.PromotionWeight.Should().Be(0.9m, "nothing has told the module it ended");
        candidate.IsPromotedAt(RecommendationTestData.Now).Should().BeFalse("but the window has closed");
    }

    /// <summary>A placement sold with no end date runs until something ends it.</summary>
    [Fact]
    public void A_placement_with_no_end_runs_indefinitely()
    {
        var candidate = RecommendationTestData.Candidate(promotionWeight: 0.5m);

        candidate.IsPromotedAt(RecommendationTestData.Now.AddYears(5)).Should().BeTrue();
    }

    // ── Ranking ──────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The reason code is <c>Promoted</c> and never <c>HighlyRated</c>.</b> This is the single
    /// most important assertion in the file: the platform explains its own ordering to the member,
    /// and a paid position explaining itself as merit would be the platform stating something untrue
    /// about itself, in writing.
    /// </summary>
    [Fact]
    public async Task A_promoted_candidate_explains_itself_as_paid_rather_than_as_merit()
    {
        var strategy = new RuleWeightingRankingStrategy();

        var candidates = new[]
        {
            RecommendationTestData.Candidate(promotionWeight: 1m, isFeatured: true)
        };

        var scores = await strategy.RankAsync(RecommendationTestData.RankingContext(
            candidates,
            RecommendationTestData.Strategy(
                RuleWeightingRankingStrategy.StrategyKey, RankingStrategyKind.RuleWeighting)));

        scores[0].ReasonCode.Should().Be(RecommendationReasonCode.Promoted);
    }

    /// <summary>
    /// <b>Money buys a strong position and never the top one.</b> A deployment naming a target
    /// outright — the onboarding guide a new arrival needs — outranks a full-weight placement, which
    /// is the ordering that keeps the platform's own editorial voice above its revenue.
    /// </summary>
    [Fact]
    public async Task An_explicit_operator_boost_outranks_a_full_weight_placement()
    {
        var strategy = new RuleWeightingRankingStrategy();
        var namedTarget = Guid.NewGuid();

        var candidates = new[]
        {
            RecommendationTestData.Candidate(targetId: namedTarget),
            RecommendationTestData.Candidate(promotionWeight: 1m)
        };

        var scores = await strategy.RankAsync(RecommendationTestData.RankingContext(
            candidates,
            RecommendationTestData.Strategy(
                RuleWeightingRankingStrategy.StrategyKey,
                RankingStrategyKind.RuleWeighting,
                parameters: new Dictionary<string, string>
                {
                    [RuleWeightingRankingStrategy.BoostedTargetsParameter] = namedTarget.ToString("D")
                })));

        scores[0].Value.Should().BeGreaterThan(scores[1].Value);
        scores[0].ReasonCode.Should().Be(RecommendationReasonCode.StartHere);
        scores[1].ReasonCode.Should().Be(RecommendationReasonCode.Promoted);
    }

    /// <summary>
    /// A purchase outranks a moderator's free featured flag — otherwise the flag would be worth more
    /// than the product B15 sells, and the product would be unsellable.
    /// </summary>
    [Fact]
    public async Task A_placement_outranks_a_free_featured_flag()
    {
        var strategy = new RuleWeightingRankingStrategy();

        var candidates = new[]
        {
            RecommendationTestData.Candidate(promotionWeight: 1m),
            RecommendationTestData.Candidate(isFeatured: true)
        };

        var scores = await strategy.RankAsync(RecommendationTestData.RankingContext(
            candidates,
            RecommendationTestData.Strategy(
                RuleWeightingRankingStrategy.StrategyKey, RankingStrategyKind.RuleWeighting)));

        scores[0].Value.Should().BeGreaterThan(scores[1].Value);
        scores[1].ReasonCode.Should().Be(RecommendationReasonCode.HighlyRated);
    }

    /// <summary>
    /// <b>A deployment can switch paid influence off entirely without B15 stopping selling.</b> The
    /// multiplier at zero means the placement is still recorded, still reported, still refundable —
    /// and contributes nothing to an ordering. A platform that could not do this would have no
    /// answer to a regulator asking it to stop.
    /// </summary>
    [Fact]
    public async Task A_zero_multiplier_removes_paid_influence_without_removing_the_placement()
    {
        var strategy = new RuleWeightingRankingStrategy();
        var candidates = new[] { RecommendationTestData.Candidate(promotionWeight: 1m) };

        var scores = await strategy.RankAsync(RecommendationTestData.RankingContext(
            candidates,
            RecommendationTestData.Strategy(
                RuleWeightingRankingStrategy.StrategyKey,
                RankingStrategyKind.RuleWeighting,
                parameters: new Dictionary<string, string>
                {
                    [RuleWeightingRankingStrategy.PromotionScoreParameter] = "0"
                })));

        scores[0].Value.Should().Be(0m);
        scores[0].ReasonCode.Should().NotBe(RecommendationReasonCode.Promoted);
        candidates[0].PromotionWeight.Should().Be(1m, "the placement itself is untouched");
    }

    /// <summary>
    /// The weight B15 priced is carried through rather than flattened. A premium listing, a featured
    /// provider and a boost are three products at three prices; scoring them identically would
    /// discard the distinction the member paid for.
    /// </summary>
    [Fact]
    public async Task A_heavier_placement_scores_above_a_lighter_one()
    {
        var strategy = new RuleWeightingRankingStrategy();

        var candidates = new[]
        {
            RecommendationTestData.Candidate(promotionWeight: 1m),
            RecommendationTestData.Candidate(promotionWeight: 0.25m)
        };

        var scores = await strategy.RankAsync(RecommendationTestData.RankingContext(
            candidates,
            RecommendationTestData.Strategy(
                RuleWeightingRankingStrategy.StrategyKey, RankingStrategyKind.RuleWeighting)));

        scores[0].Value.Should().BeGreaterThan(scores[1].Value);
        scores.Should().OnlyContain(score => score.Value <= 1m);
    }

    // ── The subject vocabulary ───────────────────────────────────────────

    /// <summary>
    /// Every subject type B15 can sell a placement over, and which target type it becomes. Written
    /// out rather than derived, so a new monetizable subject is a decision somebody records here.
    /// </summary>
    [Theory]
    [InlineData("MarketplaceListing", RecommendationTargetType.MarketplaceListing)]
    [InlineData("HousingListing", RecommendationTargetType.HousingListing)]
    [InlineData("Job", RecommendationTargetType.Job)]
    [InlineData("Provider", RecommendationTargetType.Provider)]
    [InlineData("Organization", RecommendationTargetType.Organization)]
    [InlineData("Event", RecommendationTargetType.Event)]
    [InlineData("Guide", RecommendationTargetType.Guide)]
    [InlineData("Community", RecommendationTargetType.Community)]
    public void Every_recommendable_subject_translates(string subjectType, RecommendationTargetType expected) =>
        PlacementSubjectMap.Translate(subjectType).Should().Be(expected);

    /// <summary>
    /// <b>The two unmapped subjects, and why.</b> <c>Member</c> is what B15 uses for a purchase about
    /// the buyer rather than about a thing — a subscription — and <c>Update</c> is a community post
    /// this module has no target type for. Both produce nothing, because the alternative is a
    /// titleless row in the candidate pool whose only distinguishing property is that somebody paid
    /// for it.
    /// </summary>
    [Theory]
    [InlineData("Member")]
    [InlineData("Update")]
    [InlineData("SomethingNobodyHasBuiltYet")]
    [InlineData("")]
    [InlineData(null)]
    public void A_subject_with_nothing_to_recommend_translates_to_nothing(string? subjectType) =>
        PlacementSubjectMap.Translate(subjectType).Should().BeNull();

    /// <summary>Case and padding come from a wire payload and must not decide the mapping.</summary>
    [Fact]
    public void The_subject_vocabulary_survives_the_wire() =>
        PlacementSubjectMap.Translate("  marketplacelisting  ")
            .Should().Be(RecommendationTargetType.MarketplaceListing);
}
