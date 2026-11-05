using Diaspora.SharedKernel.Search;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.SharedKernel;

/// <summary>
/// How a search sentence is read without any AI (owner, 5 Nov 2026): its meaningful words,
/// lower-cased, and nothing for a sentence that only holds itself together.
/// </summary>
public sealed class SearchWordsTests
{
    [Theory]
    [InlineData("where to braid my HAIR", new[] { "braid", "hair" })]
    [InlineData("3-bedroom flat, Grand Baie!", new[] { "bedroom", "flat", "grand", "baie" })]
    [InlineData("Plombier à Curepipe", new[] { "plombier", "curepipe" })]
    [InlineData("hair hair salon", new[] { "hair", "salon" })]
    public void A_sentence_is_read_as_its_meaningful_words(string text, string[] expected)
    {
        SearchWords.From(text).Should().Equal(expected);
    }

    [Theory]
    [InlineData("providers near me")]
    [InlineData("where can I find")]
    [InlineData("   ")]
    [InlineData(null)]
    public void A_sentence_with_nothing_to_match_gives_no_words_rather_than_match_nothing(string? text)
    {
        SearchWords.From(text).Should().BeEmpty();
    }

    [Fact]
    public void A_long_paste_keeps_only_the_first_few_words()
    {
        SearchWords.From("one two three four five six seven eight nine")
            .Should().HaveCount(SearchWords.MaxWords);
    }

    [Theory]
    [InlineData("box braids", true)]
    [InlineData("Hair Braiding", true)]
    [InlineData("childcare", false)]
    [InlineData(null, false)]
    public void A_word_names_a_phrase_when_a_word_of_it_starts_with_it(string? phrase, bool names)
    {
        SearchWords.StartsAWordOf(phrase, ["braid", "car"]).Should().Be(names);
    }

    [Fact]
    public void Any_of_several_conditions_is_one_condition()
    {
        var any = SearchWords.AnyOf<string>([value => value.StartsWith('a'), value => value.EndsWith('z')])!.Compile();

        any("apple").Should().BeTrue();
        any("fizz").Should().BeTrue();
        any("mango").Should().BeFalse();
        SearchWords.AnyOf<string>([]).Should().BeNull();
    }
}
