using System.Reflection;
using Diaspora.Modules.Guides.Domain.Categories;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Policies;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Guides;

/// <summary>
/// The guide catalogue, whose keys are the contract.
/// <para>
/// Guides reference a category by key rather than by identifier, because the key is what a
/// contributor seed declares, what a URL carries and what a frontend switches on — and it
/// survives a reseed where a generated identifier would not. Everything in this suite follows
/// from that: the key is normalised once and never changes afterwards, and a category is
/// retired rather than deleted, because thousands of guides hold the key and a delete would
/// strand every one of them.
/// </para>
/// </summary>
public class GuideCategoryTests
{
    private static readonly DateTimeOffset Now = GuideLifecycleTests.Now;

    [Fact]
    public void A_created_category_is_active_and_carries_its_normalised_key()
    {
        var category = Create("  Banking-And-Money  ", "Banking and money");

        category.IsSuccess.Should().BeTrue();
        category.Value.Key.Should().Be("banking-and-money");
        category.Value.Name.Should().Be("Banking and money");
        category.Value.IsActive.Should().BeTrue();
        category.Value.ParentKey.Should().BeNull();
        category.Value.CreatedAt.Should().Be(Now);
    }

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
    public void A_key_that_could_not_appear_in_a_url_is_refused(string? key)
    {
        var category = Create(key);

        category.IsFailure.Should().BeTrue();
        category.Error.Should().Be(GuideErrors.CategoryKeyInvalid);
    }

    [Fact]
    public void A_key_is_accepted_at_the_ceiling_and_refused_one_past_it()
    {
        Create(new string('k', GuideLimits.MaxCategoryKey)).IsSuccess.Should().BeTrue();

        Create(new string('k', GuideLimits.MaxCategoryKey + 1))
            .Error.Should().Be(GuideErrors.CategoryKeyInvalid);
    }

    [Fact]
    public void A_category_needs_a_name_a_person_can_read()
    {
        Create("banking", name: null).Error.Should().Be(GuideErrors.CategoryKeyInvalid);
        Create("banking", name: "   ").Error.Should().Be(GuideErrors.CategoryKeyInvalid);

        Create("banking", name: new string('n', GuideLimits.MaxCategoryName + 1))
            .Error.Should().Be(GuideErrors.CategoryKeyInvalid);
    }

    [Fact]
    public void A_parent_key_is_normalised_the_same_way_the_key_is()
    {
        var category = Create("banking", parentKey: "  Money-Matters  ");

        category.Value.ParentKey.Should().Be("money-matters");
    }

    [Fact]
    public void A_description_and_icon_are_truncated_rather_than_refused()
    {
        var category = Create(
            "banking",
            description: new string('d', GuideLimits.MaxCategoryDescription + 40),
            icon: new string('i', GuideLimits.MaxCategoryName + 40));

        category.Value.Description!.Length.Should().Be(GuideLimits.MaxCategoryDescription);
        category.Value.Icon!.Length.Should().Be(GuideLimits.MaxCategoryName);
    }

    // ── Updating ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void An_update_can_change_everything_except_the_key()
    {
        var category = Create("banking", "Banking").Value;

        category.Update("Banking and money", "Accounts, transfers and cards.", "bank", 3, "money-matters")
            .IsSuccess.Should().BeTrue();

        category.Key.Should().Be("banking",
            "the key is what URLs, seeds and every existing guide already hold");

        category.Name.Should().Be("Banking and money");
        category.Description.Should().Be("Accounts, transfers and cards.");
        category.Icon.Should().Be("bank");
        category.Order.Should().Be(3);
        category.ParentKey.Should().Be("money-matters");
    }

    [Fact]
    public void An_update_to_a_nameless_category_is_refused()
    {
        var category = Create("banking", "Banking").Value;

        category.Update("   ", null, null, 0, null)
            .Error.Should().Be(GuideErrors.CategoryKeyInvalid);

        category.Name.Should().Be("Banking");
    }

    [Fact]
    public void A_category_cannot_be_made_its_own_parent()
    {
        var category = Create("banking", "Banking").Value;

        var cycle = category.Update("Banking", null, null, 0, "BANKING");

        cycle.IsFailure.Should().BeTrue();
        cycle.Error.Should().Be(GuideErrors.CategoryKeyInvalid,
            "the one cycle a two-level hierarchy can create");
    }

    [Fact]
    public void An_unusable_parent_key_is_refused_rather_than_silently_dropped()
    {
        var category = Create("banking", "Banking").Value;

        category.Update("Banking", null, null, 0, "not a key")
            .Error.Should().Be(GuideErrors.CategoryKeyInvalid);
    }

    [Fact]
    public void Clearing_the_parent_moves_the_category_to_the_root()
    {
        var category = Create("banking", "Banking", parentKey: "money-matters").Value;

        category.Update("Banking", null, null, 0, null).IsSuccess.Should().BeTrue();

        category.ParentKey.Should().BeNull();
    }

    // ── Retirement, not deletion ────────────────────────────────────────────────────────────

    [Fact]
    public void A_retired_category_leaves_the_picker_and_keeps_its_key()
    {
        var category = Create("banking", "Banking").Value;

        category.Deactivate().IsSuccess.Should().BeTrue();

        category.IsActive.Should().BeFalse();
        category.Key.Should().Be("banking",
            "existing guides still hold the key and must not be stranded");
    }

    [Fact]
    public void A_retired_category_can_be_brought_back()
    {
        var category = Create("banking", "Banking").Value;

        category.Deactivate();
        category.Reactivate().IsSuccess.Should().BeTrue();

        category.IsActive.Should().BeTrue();
    }

    [Fact]
    public void The_aggregate_offers_no_way_to_delete_a_category_at_all()
    {
        // Deactivation is the design, so the absence of a delete is asserted against the type:
        // a Delete added tomorrow strands every guide holding the key.
        var methods = typeof(GuideCategory)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
                | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .ToList();

        methods.Should().NotContain(name =>
            name.Contains("Delete", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Remove", StringComparison.OrdinalIgnoreCase));

        typeof(Diaspora.Domain.Abstractions.ISoftDeletable)
            .IsAssignableFrom(typeof(GuideCategory))
            .Should().BeFalse("a category is retired, never removed");
    }

    private static Diaspora.SharedKernel.Results.Result<GuideCategory> Create(
        string? key = "banking",
        string? name = "Banking",
        string? description = null,
        string? icon = null,
        string? parentKey = null) =>
        GuideCategory.Create(
            GuideCategoryId.New(), key, name, Now, description, icon, order: 0, parentKey);
}
