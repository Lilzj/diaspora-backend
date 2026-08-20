using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.Modules.Recommendations.Extensibility.Engine;
using Diaspora.Modules.Recommendations.Infrastructure.Diversity;
using Diaspora.Modules.Recommendations.Infrastructure.Similarity;
using Diaspora.Modules.Recommendations.Options;
using Diaspora.SharedKernel.Identity;
using Diaspora.UnitTests.Recommendations.TestData;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.UnitTests.Recommendations;

/// <summary>
/// The two components that stop a personalised feed collapsing in on itself.
/// <para>
/// <b>Similarity is the feedback loop and diversity is the damping term.</b> Without diversity the
/// most-viewed listing gets more views for being shown, which makes it more popular, which shows it
/// more — and within weeks a country's marketplace surface is the same six items for everybody.
/// </para>
/// </summary>
public class DiversityAndSimilarityTests
{
    private readonly GroupCappingDiversifier _diversifier =
        new(Options.Create(new RecommendationOptions { MaximumItemsPerGroup = 2 }));

    private readonly AttributeOverlapSimilarityProvider _similarity = new();

    // ── Diversity ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>One employer cannot take the whole page.</b> The cap is what stops a popularity-weighted
    /// ranking degenerating, and it is also a fairness property: the largest seller in a country
    /// should not own the surface.
    /// </summary>
    [Fact]
    public void One_owner_cannot_take_more_than_the_cap()
    {
        var owner = UserId.New();

        var ranked = Enumerable.Range(0, 6)
            .Select(index => Scored(
                RecommendationTestData.Candidate(
                    owner: owner, categoryKey: $"category-{index}", popularity: 1m),
                score: 1m - (index * 0.01m)))
            .ToList();

        var page = _diversifier.Diversify(ranked, RecommendationTestData.Context(), limit: 6);

        // Every candidate shares one owner, so the cap defers four of them — and then the page is
        // short, so they come back. What must not happen is the page being cut to two.
        page.Should().HaveCount(6, "a diversifier demotes and reorders; it never shortens a page");

        page.Take(2).Should().OnlyContain(item => item.Candidate.OwnerUserId == owner);
    }

    /// <summary>
    /// <b>A mixed pool comes back mixed.</b> With enough variety the cap never has to defer anything,
    /// and the ranking's own ordering survives.
    /// </summary>
    [Fact]
    public void A_varied_page_is_left_in_ranked_order()
    {
        var ranked = Enumerable.Range(0, 5)
            .Select(index => Scored(
                RecommendationTestData.Candidate(
                    owner: UserId.New(), categoryKey: $"category-{index}"),
                score: 1m - (index * 0.1m)))
            .ToList();

        var page = _diversifier.Diversify(ranked, RecommendationTestData.Context(), limit: 5);

        page.Select(item => item.Score).Should().BeInDescendingOrder();
    }

    /// <summary>
    /// <b>Nothing is ever removed.</b> A page shorter than the platform had content for trades one
    /// bad page for a worse one.
    /// </summary>
    [Fact]
    public void The_diversifier_never_returns_fewer_than_the_limit_allows()
    {
        var owner = UserId.New();

        var ranked = Enumerable.Range(0, 10)
            .Select(_ => Scored(
                RecommendationTestData.Candidate(owner: owner, categoryKey: "same"), 1m))
            .ToList();

        var page = _diversifier.Diversify(ranked, RecommendationTestData.Context(), limit: 8);

        page.Should().HaveCount(8);
    }

    // ── Similarity ───────────────────────────────────────────────────────

    /// <summary>
    /// <b>Sharing a category and attributes beats sharing neither.</b> The shipped provider is
    /// deliberately unsophisticated — it will never notice that "flat" and "apartment" mean the same
    /// thing, which is exactly what an embedding adapter is for.
    /// </summary>
    [Fact]
    public async Task A_candidate_sharing_a_category_scores_higher()
    {
        var reference = RecommendationTestData.Candidate(
            categoryKey: "rental", attributes: [(InterestKind.Tag, "furnished")]);

        var similar = RecommendationTestData.Candidate(
            categoryKey: "rental", attributes: [(InterestKind.Tag, "furnished")]);

        var different = RecommendationTestData.Candidate(
            categoryKey: "electronics", attributes: [(InterestKind.Tag, "used")]);

        var scores = await _similarity.ScoreAsync([similar, different], [reference]);

        scores[0].Should().BeGreaterThan(scores[1]);
    }

    /// <summary>
    /// <b>An empty reference set returns zeroes rather than throwing.</b> A member with no history is
    /// the normal case on this path, not an error.
    /// </summary>
    [Fact]
    public async Task No_references_produces_zeroes()
    {
        var scores = await _similarity.ScoreAsync(
            [RecommendationTestData.Candidate(), RecommendationTestData.Candidate()], []);

        scores.Should().HaveCount(2).And.OnlyContain(score => score == 0m);
    }

    /// <summary>
    /// <b>A thing is not similar to itself in any useful sense.</b> Scoring it would put whatever the
    /// member last looked at at the top of their next page, forever.
    /// </summary>
    [Fact]
    public async Task A_candidate_is_not_similar_to_itself()
    {
        var candidate = RecommendationTestData.Candidate();

        var scores = await _similarity.ScoreAsync([candidate], [candidate]);

        scores.Should().ContainSingle().Which.Should().Be(0m);
    }

    /// <summary>One score per candidate, in order — the same positional contract a strategy has.</summary>
    [Fact]
    public async Task Similarity_returns_one_score_per_candidate_in_order()
    {
        var candidates = new[]
        {
            RecommendationTestData.Candidate(categoryKey: "a"),
            RecommendationTestData.Candidate(categoryKey: "b"),
            RecommendationTestData.Candidate(categoryKey: "c")
        };

        var scores = await _similarity.ScoreAsync(
            candidates, [RecommendationTestData.Candidate(categoryKey: "a")]);

        scores.Should().HaveCount(3);
        scores[0].Should().BeGreaterThan(scores[1]);
    }

    private static ScoredRecommendation Scored(
        Modules.Recommendations.Domain.Candidates.RecommendationCandidate candidate, decimal score) =>
        new()
        {
            Candidate = candidate,
            Score = score,
            PrimaryStrategyKey = "popularity",
            Explanation = RecommendationExplanation.Create(
                RecommendationReasonCode.PopularInCountry,
                "recommendations.reason.popular_in_country",
                "Popular in Mauritius.",
                RecommendationConfidence.Low),
            Contributions = []
        };
}
