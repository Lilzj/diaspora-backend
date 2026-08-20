using Diaspora.Domain.Identity.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Identity.Domain;

public class IdentityValueObjectTests
{
    [Theory]
    [InlineData("Amara Okafor", "Amara Okafor")]
    [InlineData("  Amara   Okafor  ", "Amara Okafor")]
    [InlineData("Amara\tOkafor", "Amara Okafor")]
    public void Full_names_are_normalised(string input, string expected)
    {
        FullName.Create(input).Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")]
    public void Invalid_full_names_are_rejected(string input)
    {
        FullName.TryCreate(input, out _).Should().BeFalse();
    }

    [Fact]
    public void Full_names_produce_initials_for_avatar_placeholders()
    {
        FullName.Create("Amara Okafor").Initials.Should().Be("AO");
        FullName.Create("Chidi").Initials.Should().Be("C");
        FullName.Create("Ngozi Adaeze Okonkwo").Initials.Should().Be("NO");
    }

    [Theory]
    [InlineData("ng", "NG")]
    [InlineData("Ca", "CA")]
    [InlineData(" gb ", "GB")]
    public void Country_codes_are_normalised_to_uppercase(string input, string expected)
    {
        CountryCode.Create(input).Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("NGA")]
    [InlineData("N")]
    [InlineData("12")]
    [InlineData("")]
    public void Invalid_country_codes_are_rejected(string input)
    {
        CountryCode.TryCreate(input, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("EN", "en")]
    [InlineData("pt-br", "pt-BR")]
    [InlineData("pt_BR", "pt-BR")]
    public void Language_tags_are_normalised(string input, string expected)
    {
        LanguageCode.Create(input).Value.Should().Be(expected);
    }

    [Fact]
    public void Language_tags_expose_their_primary_subtag()
    {
        LanguageCode.Create("pt-BR").PrimarySubtag.Should().Be("pt");
    }

    [Theory]
    [InlineData("1")]
    [InlineData("english!")]
    [InlineData("")]
    public void Invalid_language_tags_are_rejected(string input)
    {
        LanguageCode.TryCreate(input, out _).Should().BeFalse();
    }

    [Fact]
    public void Known_time_zones_are_accepted()
    {
        TimeZoneIdentifier.TryCreate("UTC", out _).Should().BeTrue();
    }

    [Fact]
    public void Unknown_time_zones_are_rejected()
    {
        TimeZoneIdentifier.TryCreate("Middle/Earth", out _).Should().BeFalse();
    }

    [Fact]
    public void Secret_hashes_compare_in_fixed_time()
    {
        var hash = SecretHash.Create("abcdef");

        hash.Matches(SecretHash.Create("abcdef")).Should().BeTrue();
        hash.Matches(SecretHash.Create("abcdeg")).Should().BeFalse();
        hash.Matches(SecretHash.Create("abcde")).Should().BeFalse();
    }

    [Fact]
    public void Secret_hashes_never_render_their_value()
    {
        SecretHash.Create("abcdef").ToString().Should().Be("***");
    }

    [Fact]
    public void Value_objects_compare_structurally()
    {
        CountryCode.Create("NG").Should().Be(CountryCode.Create("ng"));
        FullName.Create("Amara Okafor").Should().Be(FullName.Create("  Amara Okafor "));
        LanguageCode.Create("en").Should().NotBe(LanguageCode.Create("fr"));
    }
}
