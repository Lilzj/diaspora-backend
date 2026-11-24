using Diaspora.Modules.Creator.Domain.ContentHealth;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Creator;

/// <summary>
/// The Content Health rubric marks an item down only for what its own vertical can have (BG-08).
/// <para>
/// The tags criterion applied to every type, and events and homes cannot carry tags: no request
/// sets one, no record holds one and no list card sends one. Every event and every home was told
/// to "Add a tag or two" and could not. Version 2 limits it to the types whose list cards carry
/// <c>tags</c>.
/// </para>
/// </summary>
public sealed class ContentHealthRubricTests
{
    private static ContentHealthRubric.Criterion Criterion(string key) =>
        ContentHealthRubric.Criteria.Single(criterion => criterion.Key == key);

    [Theory]
    [InlineData("event")]
    [InlineData("housing")]
    public void Tags_do_not_apply_to_a_vertical_that_cannot_have_them(string contentType)
    {
        var tags = Criterion("tags");

        tags.AppliesTo.Should().NotBeEmpty("an empty list applies a criterion to every type");
        tags.AppliesTo.Should().NotContain(contentType);
    }

    [Theory]
    [InlineData("marketplace")]
    [InlineData("guide")]
    [InlineData("provider")]
    [InlineData("job")]
    public void Tags_still_apply_where_the_list_card_carries_them(string contentType)
    {
        Criterion("tags").AppliesTo.Should().Contain(contentType);
    }

    [Fact]
    public void Narrowing_the_tags_criterion_moved_the_version()
    {
        // A score is only comparable to another from the same version, and version 1 marked
        // every event and every home down for tags.
        ContentHealthRubric.Version.Should().BeGreaterThanOrEqualTo(2);
    }
}
