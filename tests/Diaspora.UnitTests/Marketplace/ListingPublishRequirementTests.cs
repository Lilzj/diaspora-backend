using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Listings;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Marketplace;

/// <summary>
/// DEFECT-004. A publication refusal must name the prerequisites that are actually unmet.
///
/// <para>
/// <b>The failure this prevents is a dead end with a misleading sign on it.</b> The bar has
/// seven conditions; the refusal named five and omitted fulfilment and the photograph — the two
/// most likely to be unmet, because the other five are supplied when the listing is created. A
/// seller who added exactly what the message asked for was refused again by the same sentence,
/// with nothing to act on and no way to discover that a photograph was required.
/// </para>
///
/// <para>
/// These tests assert the wording tracks the state rather than reciting the rule, so the two
/// cannot drift apart again. They deliberately do not lower the bar: <c>IsPublishable</c> is
/// unchanged and every condition still holds.
/// </para>
/// </summary>
public sealed class ListingPublishRequirementTests
{
    [Fact]
    public void The_message_names_only_the_unmet_prerequisites()
    {
        var error = MarketplaceErrors.ListingNotPublishableBecause(
        [
            ListingPublishRequirement.Fulfillment,
            ListingPublishRequirement.Photograph,
        ]);

        error.Description.Should().Contain("delivery option");
        error.Description.Should().Contain("photograph");

        // The satisfied ones must not appear. Reciting them is what made the old message
        // unusable: a seller could not tell what was wrong from what was merely required.
        error.Description.Should().NotContain("a title");
        error.Description.Should().NotContain("a category");
        error.Description.Should().NotContain("a country");
    }

    [Fact]
    public void The_message_narrows_as_prerequisites_are_satisfied()
    {
        var both = MarketplaceErrors.ListingNotPublishableBecause(
            [ListingPublishRequirement.Fulfillment, ListingPublishRequirement.Photograph]);

        var one = MarketplaceErrors.ListingNotPublishableBecause(
            [ListingPublishRequirement.Photograph]);

        both.Description.Should().NotBe(one.Description);
        one.Description.Should().NotContain("delivery option");
        one.Description.Should().Contain("photograph");
    }

    [Fact]
    public void The_message_carries_machine_readable_tokens()
    {
        // `Error` has no metadata slot, and adding one would change a primitive every module
        // depends on. The tokens let a client render a checklist without parsing prose.
        var error = MarketplaceErrors.ListingNotPublishableBecause(
            [ListingPublishRequirement.Fulfillment, ListingPublishRequirement.Photograph]);

        error.Description.Should().Contain("[missing: fulfillment,photograph]");
    }

    [Fact]
    public void The_code_is_unchanged_so_existing_clients_still_branch_on_it()
    {
        MarketplaceErrors
            .ListingNotPublishableBecause([ListingPublishRequirement.Photograph])
            .Code
            .Should()
            .Be(MarketplaceErrors.ListingNotPublishable.Code);
    }

    [Fact]
    public void An_empty_unmet_set_falls_back_to_the_general_statement()
    {
        MarketplaceErrors.ListingNotPublishableBecause([])
            .Should().Be(MarketplaceErrors.ListingNotPublishable);
    }

    [Theory]
    [InlineData(ListingPublishRequirement.Title)]
    [InlineData(ListingPublishRequirement.Description)]
    [InlineData(ListingPublishRequirement.Category)]
    [InlineData(ListingPublishRequirement.Country)]
    [InlineData(ListingPublishRequirement.Price)]
    [InlineData(ListingPublishRequirement.Fulfillment)]
    [InlineData(ListingPublishRequirement.Photograph)]
    public void Every_requirement_has_seller_facing_wording_and_a_token(
        ListingPublishRequirement requirement)
    {
        // A new requirement added without wording would silently render as its enum name.
        ListingPublishRequirementText.Describe(requirement)
            .Should().NotBeNullOrWhiteSpace().And.NotBe(requirement.ToString());

        ListingPublishRequirementText.Token(requirement)
            .Should().NotBeNullOrWhiteSpace().And.MatchRegex("^[a-z]+$");
    }
}
