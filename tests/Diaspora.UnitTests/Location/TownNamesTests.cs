using Diaspora.Modules.Location.Domain.Towns;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Location;

/// <summary>How a typed place name is compared with the world list, without any AI (5 Nov 2026).</summary>
public sealed class TownNamesTests
{
    [Theory]
    [InlineData("Mahébourg", "mahebourg")]
    [InlineData("Grand-Baie", "grand baie")]
    [InlineData("  PORT   Louis ", "port louis")]
    [InlineData("Saint-Pierre (Réunion)", "saint pierre reunion")]
    [InlineData(null, "")]
    public void A_name_is_written_one_way_before_it_is_compared(string? name, string expected)
    {
        TownNames.Normalize(name).Should().Be(expected);
    }

    [Theory]
    [InlineData("grand bay", "grand baie", true)]
    [InlineData("grandbaie", "grand baie", true)]
    [InlineData("curpipe", "curepipe", true)]
    [InlineData("granby", "grand baie", false)]
    [InlineData("rose", "rose hill", false)]
    public void A_close_spelling_counts_and_a_different_name_does_not(string typed, string name, bool close)
    {
        (TownNames.Closeness(typed, name) >= TownNames.CloseEnough).Should().Be(close);
    }

    [Theory]
    [InlineData("rose hill", "beau bassin rose hill", 1.0)]
    [InlineData("vacoas", "vacoas phoenix", 1.0)]
    [InlineData("phenix", "vacoas phoenix", 0.85)]
    public void Words_inside_a_name_can_be_the_town(string typed, string name, double atLeast)
    {
        TownNames.PartCloseness(typed, name).Should().BeGreaterThanOrEqualTo(atLeast);
    }

    [Fact]
    public void The_whole_name_is_not_a_part_of_itself()
    {
        // The whole name is Closeness's to judge; its own words alone are not close to it.
        TownNames.PartCloseness("grand baie", "grand baie").Should().BeLessThan(TownNames.CloseEnough);
    }

    [Theory]
    [InlineData("la", "la gaulette")]
    [InlineData("bay", "grand bay")]
    public void A_short_word_is_never_compared_with_parts(string typed, string name)
    {
        // A word of three letters or fewer ("la", "st", "bay") would find a town in every country.
        TownNames.PartCloseness(typed, name).Should().Be(0);
    }

    [Theory]
    [InlineData("grand baie", true)]
    [InlineData("порт луи", false)]
    [InlineData("x", false)]
    public void Only_names_a_member_can_type_here_are_kept_for_search(string normalized, bool kept)
    {
        TownNames.IsSearchable(normalized).Should().Be(kept);
    }

    [Fact]
    public void A_town_keeps_its_names_between_bars_without_repeats()
    {
        var town = WorldTown.Create(1, "Grand Baie", ["Grand Bay", "grand-baie", "Гран-Бе"], "mu", -20.01, 57.58, 11_000);

        town.SearchNames.Should().Be("|grand baie|grand bay|");
        town.CountryCode.Should().Be("MU");
        town.Names.Should().Equal("grand baie", "grand bay");
    }

    [Fact]
    public void A_code_is_not_a_name_a_member_types()
    {
        var town = WorldTown.Create(2, "Port Louis", ["PLU", "MRU", "Port-Louis", "Port Lewis"], "MU", -20.16, 57.50, 150_000);

        town.Names.Should().Equal("port louis", "port lewis");
    }

    [Fact]
    public void A_big_city_keeps_more_of_its_names_than_a_small_town()
    {
        // GeoNames lists other spellings about alphabetically, so "Londres" comes after many others.
        string[] spellings =
        [
            "Lakana", "Landan", "Landen", "Ljondan", "Llundain", "Lodoni", "Londain", "Londan", "Londar",
            "Londe", "Londen", "Londinium", "Londino", "Londis", "Londona", "Londonas", "Londoni", "Londra", "Londres"
        ];

        var city = WorldTown.Create(3, "London", spellings, "GB", 51.5, -0.13, 8_900_000);
        var town = WorldTown.Create(4, "Londontown", spellings, "US", 38.9, -76.5, 8_000);

        city.Names.Should().Contain("londres");
        town.Names.Should().HaveCount(8);
    }

    [Fact]
    public void A_town_s_names_fit_the_column_that_holds_them()
    {
        var longNames = Enumerable.Range(0, 40).Select(index => $"{new string('a', 50)} {index:D2}");

        var city = WorldTown.Create(5, "Bigtown", longNames, "XX", 0, 0, 5_000_000);

        city.SearchNames.Length.Should().BeLessThanOrEqualTo(TownNames.MaxSearchNamesLength);
        city.SearchNames.Should().StartWith("|bigtown|").And.EndWith("|");
    }
}
