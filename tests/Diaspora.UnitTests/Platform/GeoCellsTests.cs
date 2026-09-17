using Diaspora.Platform.Communication;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Platform;

/// <summary>
/// The grid the live map's alert channels are cut on.
/// <para>
/// The server announces on these cells and every phone computes the same cells to listen on, so the
/// encoding is pinned to published geohash vectors rather than to itself — two implementations that
/// agree with each other and not with the standard would still agree on the device, and fail the day
/// a third client arrives. The app's own tests pin the same vectors.
/// </para>
/// </summary>
public class GeoCellsTests
{
    [Theory]
    [InlineData(57.64911d, 10.40744d, 11, "u4pruydqqvj")]
    [InlineData(42.605d, -5.603d, 5, "ezs42")]
    [InlineData(51.5074d, -0.1278d, 4, "gcpv")]
    public void Encoding_matches_the_published_geohash(double latitude, double longitude, int precision, string expected)
    {
        GeoCells.Encode(latitude, longitude, precision).Should().Be(expected);
    }

    [Fact]
    public void An_alert_at_a_point_is_announced_on_its_own_cell_only()
    {
        GeoCells.Cover(51.4742d, -0.0691d, 0).Should().Equal(GeoCells.Encode(51.4742d, -0.0691d));
    }

    /// <summary>
    /// Peckham sits a few kilometres from its cell's eastern edge. A 20 km flood there reaches the
    /// cell to the east, and a member watching that cell must hear about it.
    /// </summary>
    [Fact]
    public void An_alert_with_an_area_is_announced_on_every_cell_the_area_reaches()
    {
        var home = GeoCells.Encode(51.4742d, -0.0691d);
        var east = GeoCells.Encode(51.4742d, 0.14d);

        home.Should().NotBe(east, "otherwise this test would prove nothing");

        var cells = GeoCells.Cover(51.4742d, -0.0691d, 20_000);

        cells[0].Should().Be(home, "the centre's cell is announced first");
        cells.Should().Contain(east);
        cells.Should().OnlyHaveUniqueItems();
        cells.Should().AllSatisfy(cell => GeoCells.IsCell(cell).Should().BeTrue());
    }

    [Fact]
    public void The_largest_area_an_alert_may_declare_stays_inside_the_announcement_cap()
    {
        var cells = GeoCells.Cover(51.4742d, -0.0691d, 50_000);

        cells.Count.Should().BeLessThan(GeoCells.MaxCellsPerAnnouncement,
            "a real alert must never be cut short by a guard meant for a bug");
    }

    [Fact]
    public void Coordinates_that_are_not_numbers_cover_nothing_rather_than_looping_for_ever()
    {
        GeoCells.Cover(double.NaN, -0.0691d, 1_000).Should().BeEmpty();
        GeoCells.Cover(51.4742d, double.PositiveInfinity, 1_000).Should().BeEmpty();
        GeoCells.Cover(51.4742d, -0.0691d, double.NaN).Should().ContainSingle();
    }

    [Theory]
    [InlineData("gcpv", true)]
    [InlineData("gcp", false)]
    [InlineData("gcpvj", false)]
    [InlineData("gcpa", false)]
    [InlineData("GCPV", false)]
    [InlineData(null, false)]
    public void Only_a_four_character_geohash_is_a_cell(string? candidate, bool isCell)
    {
        GeoCells.IsCell(candidate).Should().Be(isCell);
    }

    [Fact]
    public void An_alert_channel_names_its_cell_and_nothing_else_is_an_alert_channel()
    {
        var channel = RealtimeChannels.ForAlertCell("gcpv");

        channel.Should().Be("geo:alerts:gcpv");
        RealtimeChannels.AlertCellOf(channel).Should().Be("gcpv");
        RealtimeChannels.PrefixOf(channel).Should().Be(RealtimeChannels.AlertCellPrefix);

        RealtimeChannels.AlertCellOf("geo:alerts:nope").Should().BeNull();
        RealtimeChannels.AlertCellOf("event:gcpv").Should().BeNull();

        var invalid = () => RealtimeChannels.ForAlertCell("london");
        invalid.Should().Throw<ArgumentException>();
    }
}
