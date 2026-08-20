using Diaspora.Modules.Bookmarks.Domain;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Bookmarks;

/// <summary>
/// DEFECT-005. A bookmark target type must be something a module claims.
///
/// <para>
/// <b>The invariant is deliberately narrower than "the target exists".</b> <c>TargetType</c> and
/// <c>TargetId</c> are strings precisely so any module's identifier shape fits without Bookmarks
/// knowing that module exists; checking existence would turn a module that depends on nothing
/// into one that depends on all of them. A bookmark can therefore outlive the thing it points
/// at, exactly as a browser bookmark outlives a page, and that is accepted and documented.
/// </para>
///
/// <para>
/// What was wrong was the absence of any check at all:
/// <c>{"targetType":"NotAThing"}</c> was stored as <c>notathing</c>, so a typo in a client became
/// a permanent row nothing could ever render. These tests hold the line at the type while
/// keeping the extensibility that made the free string the right choice.
/// </para>
/// </summary>
public sealed class BookmarkTargetCatalogTests
{
    [Fact]
    public void An_unknown_target_type_is_rejected()
    {
        new BookmarkTargetCatalog().IsKnown("notathing").Should().BeFalse();
    }

    [Theory]
    [InlineData("marketplace-listing")]
    [InlineData("housing-listing")]
    [InlineData("guide")]
    [InlineData("job")]
    [InlineData("community-post")]
    public void The_platform_baseline_is_accepted(string targetType)
    {
        new BookmarkTargetCatalog().IsKnown(targetType).Should().BeTrue();
    }

    [Fact]
    public void Matching_ignores_case_and_surrounding_space()
    {
        // The create handler lower-cases before storing, so the catalogue must agree with it
        // rather than reject a spelling the handler would have normalised anyway.
        var catalog = new BookmarkTargetCatalog();

        catalog.IsKnown("Marketplace-Listing").Should().BeTrue();
        catalog.IsKnown("  guide  ").Should().BeTrue();
    }

    [Theory]
    [InlineData("CommunityPost")]
    [InlineData("MarketplaceListing")]
    [InlineData("HousingListing")]
    public void A_pascal_case_spelling_of_a_known_type_is_accepted(string targetType)
    {
        // The baseline keys were chosen here rather than derived from a caller — the module has
        // no client — so refusing PascalCase would be refusing a spelling of the same word for
        // no reason but which form happened to be written down first.
        new BookmarkTargetCatalog().IsKnown(targetType).Should().BeTrue();
    }

    [Fact]
    public void Normalisation_does_not_invent_types()
    {
        new BookmarkTargetCatalog().IsKnown("NotAThing").Should().BeFalse();
    }

    [Fact]
    public void Empty_and_null_are_not_target_types()
    {
        var catalog = new BookmarkTargetCatalog();

        catalog.IsKnown("").Should().BeFalse();
        catalog.IsKnown("   ").Should().BeFalse();
        catalog.IsKnown(null!).Should().BeFalse();
    }

    [Fact]
    public void A_module_can_add_its_own_target_type_without_editing_the_catalogue()
    {
        // This is the property that keeps the fix from becoming the thing it replaced. If a new
        // saveable thing required an edit inside Bookmarks, the module would have acquired
        // knowledge of every module that owns one.
        var catalog = new BookmarkTargetCatalog([new StubContributor("field-report")]);

        catalog.IsKnown("field-report").Should().BeTrue();
        catalog.IsKnown("marketplace-listing").Should().BeTrue("the baseline must survive");
    }

    [Fact]
    public void A_contributed_type_is_normalised_like_every_other()
    {
        new BookmarkTargetCatalog([new StubContributor("  Field-Report  ")])
            .IsKnown("field-report").Should().BeTrue();
    }

    [Fact]
    public void The_known_set_is_reportable_so_the_error_can_name_it()
    {
        var known = new BookmarkTargetCatalog().Known;

        known.Should().NotBeEmpty();
        known.Should().BeInAscendingOrder("an unordered list makes a poor error message");
    }

    private sealed class StubContributor : IBookmarkTargetContributor
    {
        private readonly string[] _types;
        public StubContributor(params string[] types) => _types = types;
        public IEnumerable<string> TargetTypes => _types;
    }
}
