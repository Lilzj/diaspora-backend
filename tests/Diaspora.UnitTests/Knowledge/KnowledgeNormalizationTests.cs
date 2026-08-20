using Diaspora.Modules.Guides.Domain.Knowledge;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Knowledge;

/// <summary>
/// The four kinds of text the Knowledge Directory treats as identity rather than prose.
/// <para>
/// <b>Every one of these has one implementation, and this suite is why.</b> Five aggregates in
/// B8.1 accept a category key; a key normalised to lower case in four of them and left alone in the
/// fifth produces a directory entry pointing at a category the database says does not exist — with
/// both rows visible, both spellings plausible, and nothing in a stack trace. The locale casing is
/// the same defect wearing different clothes: <c>fr-mu</c> and <c>fr-MU</c> as two rows makes the
/// unique key refuse a curator's second translation while the visible list says otherwise.
/// </para>
/// </summary>
public class KnowledgeNormalizationTests
{
    // ── Keys ────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("  Cost-Of-Living  ", "cost-of-living")]
    [InlineData("IMMIGRATION", "immigration")]
    [InlineData("student-life", "student-life")]
    [InlineData("a1", "a1")]
    public void A_key_is_trimmed_and_lower_cased(string input, string expected) =>
        KnowledgeNormalization.Key(input).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-leading")]
    [InlineData("trailing-")]
    [InlineData("double--hyphen")]
    [InlineData("has space")]
    [InlineData("has_underscore")]
    [InlineData("has.dot")]
    public void A_key_that_could_not_appear_in_a_url_is_refused(string? key) =>
        KnowledgeNormalization.Key(key).Should().BeNull();

    [Fact]
    public void A_key_longer_than_its_column_is_refused_rather_than_clipped()
    {
        // Clipping would produce a key that is a prefix of a real one — the worst possible
        // collision, because both look right and one silently wins.
        KnowledgeNormalization.Key(new string('a', 65)).Should().BeNull();
    }

    /// <summary>
    /// The grammar deliberately matches B8's guide category keys. The directory draws from those
    /// keys, and two grammars would mean a key legal on one side of the join and not the other.
    /// </summary>
    [Fact]
    public void The_key_grammar_matches_the_guide_category_grammar()
    {
        foreach (var key in new[] { "immigration", "government-services", "cost-of-living" })
        {
            KnowledgeNormalization.Key(key).Should().Be(key);
        }
    }

    /// <summary>
    /// <b>A regression test for a defect that cost an afternoon and produced no error anywhere.</b>
    /// <see cref="KnowledgeNormalization.Key"/> is passed as a method group to <c>Select</c> in half
    /// a dozen places. While it carried an optional second <c>int</c> parameter it also matched
    /// <c>Select(Func&lt;T, int, TResult&gt;)</c> — LINQ's <em>indexed</em> overload — and the
    /// compiler chose it silently. Every key was then validated against its own position in the list
    /// as a length limit, so a twenty-four row country template arranged seven rows and dropped
    /// seventeen: no exception, no log line, and a country page that simply had less on it than
    /// anybody intended.
    /// <para>
    /// The fix was to remove the optional parameter, so this asserts the shape rather than the
    /// symptom: mapping a list of valid keys through the method group must return every one of them,
    /// whatever their position.
    /// </para>
    /// </summary>
    [Fact]
    public void Mapping_keys_through_the_method_group_keeps_every_one_of_them()
    {
        // Ordered longest-first, so an index-as-length-limit bug drops the early ones.
        var keys = new[]
        {
            "government-services",
            "cost-of-living",
            "transportation",
            "getting-started",
            "immigration",
            "healthcare",
            "education",
            "utilities",
            "banking",
            "housing",
            "taxes",
            "legal"
        };

        var normalized = keys.Select(KnowledgeNormalization.Key).OfType<string>().ToList();

        normalized.Should().BeEquivalentTo(
            keys,
            options => options.WithStrictOrdering(),
            "a method group passed to Select must bind to the element overload, not the indexed one");
    }

    [Fact]
    public void A_path_key_may_be_longer_than_a_category_key()
    {
        const string Phrase = "arriving-with-school-age-children-on-a-dependant-visa-and-a-pension";

        KnowledgeNormalization.PathKey(Phrase).Should().Be(Phrase);

        KnowledgeNormalization.Key(Phrase).Should().BeNull(
            "a category key is a taxonomy term and stays short");

        KnowledgeNormalization.PathKey(new string('a', 101)).Should().BeNull(
            "a path key is longer than a category key, not unbounded");
    }

    // ── Countries ───────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("mu", "MU")]
    [InlineData(" ca ", "CA")]
    [InlineData("NG", "NG")]
    public void A_country_code_is_trimmed_and_upper_cased(string input, string expected) =>
        KnowledgeNormalization.Country(input).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("M")]
    [InlineData("MUS")]
    [InlineData("M1")]
    public void A_country_code_that_is_not_two_letters_is_refused(string? code) =>
        KnowledgeNormalization.Country(code).Should().BeNull();

    /// <summary>
    /// Shape only, and never a lookup against B4's catalogue. A deployment serving a territory
    /// Location has not catalogued yet must be able to publish guidance about it rather than being
    /// told its country does not exist.
    /// </summary>
    [Fact]
    public void A_country_code_is_checked_for_shape_and_not_for_existence() =>
        KnowledgeNormalization.Country("zz").Should().Be("ZZ");

    // ── Locales ─────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("FR", "fr")]
    [InlineData("fr-mu", "fr-MU")]
    [InlineData("PT-br", "pt-BR")]
    [InlineData("zh-hans", "zh-Hans")]
    [InlineData("zh-hans-cn", "zh-Hans-CN")]
    public void A_locale_is_normalised_to_conventional_casing(string input, string expected) =>
        KnowledgeNormalization.Locale(input).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("f")]
    [InlineData("fr_MU")]
    [InlineData("fr-MU-x-private-extension-that-is-far-too-long")]
    public void A_locale_that_is_not_a_usable_tag_is_refused(string? locale) =>
        KnowledgeNormalization.Locale(locale).Should().BeNull();

    /// <summary>
    /// The specific collision the casing rule prevents: two spellings of one locale would be two
    /// rows in a collection keyed on it.
    /// </summary>
    [Fact]
    public void Two_spellings_of_one_locale_normalise_to_the_same_tag() =>
        KnowledgeNormalization.Locale("fr-mu")
            .Should().Be(KnowledgeNormalization.Locale("FR-MU"));

    // ── Bounded text ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Prose_is_trimmed_and_clipped_rather_than_refused()
    {
        KnowledgeNormalization.Text("  a summary  ", 100).Should().Be("a summary");
        KnowledgeNormalization.Text(new string('x', 200), 100).Should().HaveLength(100);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_prose_becomes_null_rather_than_an_empty_string(string? value) =>
        KnowledgeNormalization.Text(value, 100).Should().BeNull();
}
