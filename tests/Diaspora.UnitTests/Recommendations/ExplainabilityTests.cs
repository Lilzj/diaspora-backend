using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.Modules.Recommendations.Extensibility.Ranking;
using Diaspora.Modules.Recommendations.Infrastructure.Explainability;
using Diaspora.UnitTests.Recommendations.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Recommendations;

/// <summary>
/// Why a member is being shown something.
/// <para>
/// <b>The one line of the card whose whole job is to be trusted.</b> A reason that oversells is worse
/// than no reason at all: the first time somebody checks and finds it was not true, every other
/// explanation the platform gives is discounted too.
/// </para>
/// </summary>
public class ExplainabilityTests
{
    private readonly DefaultRecommendationReasonBuilder _builder = new();

    /// <summary>
    /// <b>The most personal true reason wins, not the largest contribution.</b> A guide that scored
    /// highest on popularity and second on location is honestly described by the location — "popular"
    /// alone reads as "we showed you the most-clicked thing", which is a worse account of why
    /// <em>you</em> are seeing it.
    /// </summary>
    [Fact]
    public void The_most_personal_reason_wins_over_the_largest_contribution()
    {
        var scores = new Dictionary<string, RankingScore>(StringComparer.Ordinal)
        {
            ["popularity"] = new()
            {
                Value = 0.9m,
                ReasonCode = RecommendationReasonCode.PopularInCountry
            },
            ["location"] = new()
            {
                Value = 0.4m,
                ReasonCode = RecommendationReasonCode.LivesInNeighborhood,
                ReasonDetail = "Plaine Verte"
            }
        };

        var explanation = _builder.Build(
            RecommendationTestData.Candidate(),
            Contributions(("popularity", RankingStrategyKind.Popularity, 0.9m),
                          ("location", RankingStrategyKind.Location, 0.4m)),
            scores,
            RecommendationTestData.Context());

        explanation.ReasonCode.Should().Be(RecommendationReasonCode.LivesInNeighborhood);
        explanation.FallbackText.Should().Contain("Plaine Verte");
    }

    /// <summary>
    /// <b>Confidence is corroboration, never the score.</b> One strategy at 0.9 is a guess; four
    /// agreeing at 0.5 is a match. Deriving it from the score would make the cold-start path — where
    /// popularity alone produces a very high number — the most confident path in the module.
    /// </summary>
    [Fact]
    public void A_single_high_contribution_is_not_high_confidence()
    {
        var scores = new Dictionary<string, RankingScore>(StringComparer.Ordinal)
        {
            ["popularity"] = new()
            {
                Value = 1m,
                ReasonCode = RecommendationReasonCode.PopularInCountry
            }
        };

        var explanation = _builder.Build(
            RecommendationTestData.Candidate(),
            Contributions(("popularity", RankingStrategyKind.Popularity, 1m)),
            scores,
            RecommendationTestData.Context());

        explanation.Confidence.Should().NotBe(RecommendationConfidence.High);
    }

    /// <summary>
    /// <b>A cold-start recommendation is capped at low confidence however many strategies agreed.</b>
    /// Agreement between strategies that all ran on an empty profile is not corroboration.
    /// </summary>
    [Fact]
    public void A_cold_start_recommendation_is_never_high_confidence()
    {
        var scores = new Dictionary<string, RankingScore>(StringComparer.Ordinal)
        {
            ["popularity"] = new() { Value = 1m, ReasonCode = RecommendationReasonCode.PopularInCountry },
            ["location"] = new() { Value = 1m, ReasonCode = RecommendationReasonCode.LivesInCountry },
            ["trust"] = new() { Value = 1m, ReasonCode = RecommendationReasonCode.VerifiedProvider },
            ["recency"] = new() { Value = 1m, ReasonCode = RecommendationReasonCode.RecentlyPublished }
        };

        var explanation = _builder.Build(
            RecommendationTestData.Candidate(),
            Contributions(
                ("popularity", RankingStrategyKind.Popularity, 1m),
                ("location", RankingStrategyKind.Location, 1m),
                ("trust", RankingStrategyKind.Trust, 1m),
                ("recency", RankingStrategyKind.Recency, 1m)),
            scores,
            RecommendationTestData.Context(isColdStart: true));

        explanation.Confidence.Should().Be(RecommendationConfidence.Low);
    }

    /// <summary>
    /// <b>Nothing to say is still an explanation.</b> A recommendation with no contributions is
    /// possible — the fallback strategy alone — and the honest answer for a new member is that this
    /// is a starting point, not that it matches them.
    /// </summary>
    [Fact]
    public void An_unexplained_recommendation_still_gets_an_honest_reason()
    {
        var explanation = _builder.Build(
            RecommendationTestData.Candidate(),
            [],
            new Dictionary<string, RankingScore>(StringComparer.Ordinal),
            RecommendationTestData.Context(isColdStart: true));

        explanation.Should().NotBeNull();
        explanation.ReasonCode.Should().Be(RecommendationReasonCode.PopularWithNewArrivals);
        explanation.FallbackText.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// <b>Every reason code renders.</b> A code with no catalogue entry would reach a member as an
    /// empty line or a raw enum name on the card the product is asking to be trusted on.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllReasonCodes))]
    public void Every_reason_code_has_a_key_and_a_rendering(RecommendationReasonCode code)
    {
        RecommendationReasonCatalog.ResourceKeyFor(code)
            .Should().StartWith("recommendations.reason.");

        RecommendationReasonCatalog.TextFor(code, "Somewhere")
            .Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// <b>A sentence expecting a detail never renders with an empty placeholder.</b> "Because you live
    /// in ." is the kind of thing that escapes to production because nobody tests the null case of a
    /// fallback string.
    /// </summary>
    [Fact]
    public void A_missing_detail_falls_back_to_a_generic_sentence()
    {
        var rendered = RecommendationReasonCatalog.TextFor(
            RecommendationReasonCode.LivesInCountry, detail: null);

        rendered.Should().NotContain("{0}");
        rendered.Should().NotEndWith(" .");
    }

    /// <summary>
    /// An explanation cannot be constructed without a resource key and a rendering — which is what
    /// makes "every recommendation includes an explanation" a property of the type.
    /// </summary>
    [Fact]
    public void An_explanation_requires_a_key_and_a_rendering()
    {
        var act = () => RecommendationExplanation.Create(
            RecommendationReasonCode.LivesInCountry, "  ", "text", RecommendationConfidence.Low);

        act.Should().Throw<ArgumentException>();
    }

    public static TheoryData<RecommendationReasonCode> AllReasonCodes()
    {
        var data = new TheoryData<RecommendationReasonCode>();

        foreach (var code in Enum.GetValues<RecommendationReasonCode>())
        {
            data.Add(code);
        }

        return data;
    }

    private static List<StrategyContribution> Contributions(
        params (string Key, RankingStrategyKind Kind, decimal Score)[] entries) =>
        entries
            .Select(entry => StrategyContribution.Create(
                Guid.NewGuid(), entry.Key, entry.Kind, entry.Score, 1m))
            .ToList();
}
