using Diaspora.Modules.GeographicIntelligence.Domain.Heat;
using Diaspora.Modules.GeographicIntelligence.Domain.Scores;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.GeographicIntelligence;

/// <summary>
/// The arithmetic behind every published figure, tested on its own.
/// <para>
/// <b>These are the two places a fabrication could enter B21 without anybody noticing.</b> A band
/// encoding that interpolated, or a percentile that produced a confident number from three data
/// points, would look exactly like real geographic intelligence at the wire. Everything else in the
/// module is composition and can be read; these are the functions where correctness is arithmetic.
/// </para>
/// </summary>
public sealed class ScoreEncodingTests
{
    // ── Band encoding ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Five bands land on the five midpoints and nowhere between them. If this ever produces a
    /// value not in this set, the encoding has started interpolating and B11's whole reason for
    /// recording bands instead of numbers has been undone downstream.
    /// </summary>
    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 30)]
    [InlineData(3, 50)]
    [InlineData(4, 70)]
    [InlineData(5, 90)]
    public void A_band_encodes_to_the_midpoint_of_its_fifth(int band, int expected) =>
        BandScale.ToScore(band).Should().Be(expected);

    /// <summary>
    /// Two neighbourhoods assessed the same way get the same number. A client charting these must
    /// never see a difference the assessment did not contain.
    /// </summary>
    [Fact]
    public void The_same_band_always_encodes_to_the_same_score() =>
        BandScale.ToScore(4).Should().Be(BandScale.ToScore(4));

    /// <summary>
    /// The band names B11 publishes on its integration contract, parsed. This is the seam where a
    /// spelling change on that contract surfaces — the alternative being a dimension that quietly
    /// reads unassessed forever.
    /// </summary>
    [Theory]
    [InlineData("VeryLow", 1)]
    [InlineData("Low", 2)]
    [InlineData("Moderate", 3)]
    [InlineData("High", 4)]
    [InlineData("VeryHigh", 5)]
    [InlineData("veryhigh", 5)]
    [InlineData("  High  ", 4)]
    public void The_band_names_housing_publishes_all_parse(string name, int expected) =>
        BandScale.ParseBand(name).Should().Be(expected);

    /// <summary>
    /// <b><c>Unknown</c> must not parse.</b> B11's own note is that Unknown is the default
    /// everywhere and is not a low rating — a neighbourhood nobody has assessed must not sort below
    /// one somebody rated badly. Parsing it to band 1 would publish "VeryLow" for every place on
    /// the platform nobody has looked at.
    /// </summary>
    [Theory]
    [InlineData("Unknown")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("Excellent")]
    public void An_unassessed_or_unrecognised_band_does_not_parse(string? name) =>
        BandScale.ParseBand(name).Should().BeNull();

    /// <summary>The methodology names the assessor and the band, because §26 requires both.</summary>
    [Fact]
    public void The_band_methodology_states_the_band_and_who_assessed_it()
    {
        var methodology = BandScale.Methodology("High", "a Diaspora moderator");

        methodology.Should().Contain("High").And.Contain("a Diaspora moderator");
        methodology.Should().Contain(
            "midpoint", "the sentence must say the number carries no precision beyond the band");
    }

    // ── Percentile ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The floor. A percentile over three places reads as authoritative and is noise, so below the
    /// configured population it is not published at all.
    /// </summary>
    [Fact]
    public void A_population_below_the_floor_produces_no_rank() =>
        PercentileRank.Of(5, [1, 2, 3], minimumPopulation: 5).Should().BeNull();

    /// <summary>
    /// <b>An all-tied population scores 50, not 100.</b> The midpoint definition exists for this:
    /// most neighbourhoods have zero of most things, and a rule that scored every member of a
    /// population where all values tie at the top would rank an empty place first.
    /// </summary>
    [Fact]
    public void Every_member_of_an_all_tied_population_is_typical() =>
        PercentileRank.Of(0, [0, 0, 0, 0, 0, 0], minimumPopulation: 5).Should().Be(50);

    /// <summary>The highest value in a spread population ranks at the top, and the lowest at the bottom.</summary>
    [Theory]
    [InlineData(0, 10)]
    [InlineData(50, 90)]
    public void A_value_ranks_by_where_it_sits(int value, int expected) =>
        PercentileRank.Of(value, [0, 1, 2, 3, 50], minimumPopulation: 5).Should().Be(expected);

    /// <summary>A rank is always inside the range a client will render as a bar.</summary>
    [Theory]
    [InlineData(-100)]
    [InlineData(0)]
    [InlineData(1_000_000)]
    public void A_rank_is_always_between_zero_and_a_hundred(int value) =>
        PercentileRank.Of(value, [0, 1, 2, 3, 4, 5], minimumPopulation: 5)
            .Should().BeInRange(0, 100);

    /// <summary>
    /// A population of one is refused whatever the configured floor says. Ranking something against
    /// itself is not a statistic, and a deployment lowering the floor must not be able to buy one.
    /// </summary>
    [Fact]
    public void A_population_of_one_is_refused_even_below_the_configured_floor() =>
        PercentileRank.Of(3, [3], minimumPopulation: 1).Should().BeNull();

    /// <summary>
    /// The methodology names the population and its size, so a client can show the n. A percentile
    /// whose population is unstated is exactly the unattributed number §26 forbids.
    /// </summary>
    [Fact]
    public void The_rank_methodology_names_the_population_and_its_size()
    {
        var methodology = PercentileRank.Methodology("listings currently advertised", 37, "GB");

        methodology.Should().Contain("37").And.Contain("GB")
            .And.Contain("listings currently advertised");
    }

    // ── The layer vocabulary ────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Every supported layer maps to a counter key some vertical actually registers.</b> A layer
    /// pointing at a resource type nobody counts would answer an empty heat map with no error —
    /// which renders as a quiet district, which is a claim about the world.
    /// </summary>
    [Theory]
    [InlineData("community-activity", "updates")]
    [InlineData("housing-availability", "housing")]
    [InlineData("marketplace-activity", "marketplace")]
    [InlineData("jobs", "jobs")]
    [InlineData("events", "events")]
    public void A_supported_layer_names_a_real_counter_key(string layer, string resourceType)
    {
        var definition = HeatLayerCatalog.Find(layer);

        definition.Should().NotBeNull();
        definition!.ResourceType.Should().Be(resourceType);
        definition.Unit.Should().NotBeNullOrWhiteSpace("a legend that cannot name its unit is decoration");
    }

    /// <summary>
    /// The three the platform genuinely cannot compute are refused rather than answered empty.
    /// Price and cost need a value per cell and the counter registry answers only "how many";
    /// safety has no source anywhere.
    /// </summary>
    [Theory]
    [InlineData("housing-price")]
    [InlineData("cost-of-living")]
    [InlineData("safety")]
    [InlineData("unicorns")]
    [InlineData("")]
    [InlineData(null)]
    public void A_layer_with_no_real_source_is_not_in_the_catalogue(string? layer) =>
        HeatLayerCatalog.Find(layer).Should().BeNull();

    /// <summary>The nine dimension ids are the client's union, spelled kebab-case.</summary>
    [Fact]
    public void The_dimension_ids_match_the_clients_closed_union() =>
        ScoreDimensionId.All.Should().BeEquivalentTo(
        [
            "safety", "cost-of-living", "community-activity", "housing",
            "transport", "services", "events", "jobs", "amenities"
        ],
            "a tenth value, or one spelled in PascalCase, fails the client's type check rather "
            + "than rendering as unknown");
}
