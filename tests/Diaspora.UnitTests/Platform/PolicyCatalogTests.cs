using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Authorization;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Platform;

public class PolicyCatalogTests
{
    private static PermissionCatalog PermissionsWith(params string[] keys)
    {
        var definitions = keys.Select(key => new PermissionDefinition(key, "M", key)).ToArray();
        return new PermissionCatalog([new TestPermissionContributor("M", definitions)]);
    }

    [Fact]
    public void It_composes_and_validates_policies_against_the_permission_catalogue()
    {
        var policies = new PolicyCatalog(
            [new TestPolicyContributor("M", PolicyDefinition.ForPermission("m.read", "M", "m:read"))],
            PermissionsWith("m:read"));

        policies.Find("m.read").Should().NotBeNull();
        policies.Find("m.read")!.Permission.Should().Be("m:read");
    }

    [Fact]
    public void A_policy_naming_an_undeclared_permission_fails_the_composition()
    {
        // This is the valuable check: a typo'd permission would otherwise become a
        // permanently-denied endpoint discovered only in production. Fail the boot instead.
        var act = () => new PolicyCatalog(
            [new TestPolicyContributor("M", PolicyDefinition.ForPermission("m.read", "M", "m:typo"))],
            PermissionsWith("m:read"));

        act.Should().Throw<InvalidOperationException>().WithMessage("*m:typo*no module declares*");
    }

    [Fact]
    public void A_policy_with_neither_permission_nor_tier_fails_the_composition()
    {
        var act = () => new PolicyCatalog(
            [new TestPolicyContributor("M", new PolicyDefinition("m.open", "M"))],
            PermissionsWith());

        act.Should().Throw<InvalidOperationException>().WithMessage("*neither a permission nor a tier*");
    }

    [Fact]
    public void Duplicate_policy_names_fail_the_composition()
    {
        var act = () => new PolicyCatalog(
            [
                new TestPolicyContributor("A", PolicyDefinition.ForTier("shared.policy", "A", AccessTier.Verified)),
                new TestPolicyContributor("B", PolicyDefinition.ForTier("shared.policy", "B", AccessTier.Verified))
            ],
            PermissionsWith());

        act.Should().Throw<InvalidOperationException>().WithMessage("*shared.policy*");
    }

    [Fact]
    public void A_tier_backed_policy_needs_no_permission()
    {
        var policies = new PolicyCatalog(
            [new TestPolicyContributor("M", PolicyDefinition.ForTier("m.verified", "M", AccessTier.Verified))],
            PermissionsWith());

        policies.Find("m.verified")!.MinimumTier.Should().Be(AccessTier.Verified);
    }

    // ── Aliases: the mechanism B1.4.1 renamed B13's and B14's policies with ──────────────

    /// <summary>
    /// <b>Both names, one definition.</b> That is the whole property an alias has to have: whatever
    /// the requirements become, the old name gets them in the same edit, because there is nothing
    /// separate to update.
    /// </summary>
    [Fact]
    public void A_legacy_name_resolves_to_the_policy_that_replaced_it()
    {
        var policies = new PolicyCatalog(
            [
                new TestPolicyContributor(
                    "M",
                    PolicyDefinition.ForPermission("m.read", "M", "m:read").WithAliases("M.Read"))
            ],
            PermissionsWith("m:read"));

        policies.Find("M.Read").Should().BeSameAs(policies.Find("m.read"));
        policies.Find("M.Read")!.Permission.Should().Be("m:read");
    }

    /// <summary>
    /// An alias is a second name, never a second policy. A catalogue that counted them would report
    /// a platform with more policies than any module declared, and every consumer that walks
    /// <c>All</c> — the ASP.NET registration, the startup validator, the manifest — would double up.
    /// </summary>
    [Fact]
    public void An_alias_is_not_a_second_entry_in_the_catalogue()
    {
        var policies = new PolicyCatalog(
            [
                new TestPolicyContributor(
                    "M",
                    PolicyDefinition.ForPermission("m.read", "M", "m:read")
                        .WithAliases("M.Read", "M.Reader"))
            ],
            PermissionsWith("m:read"));

        policies.All.Should().ContainSingle().Which.Aliases.Should().HaveCount(2);
    }

    /// <summary>
    /// <b>The dangerous collision, refused at composition.</b> An alias that is also a live policy
    /// would give one module's endpoints another module's requirements — a privilege change wearing
    /// a rename's clothes, and completely silent if the two happen to be compatible.
    /// </summary>
    [Fact]
    public void An_alias_that_shadows_a_live_policy_fails_the_composition()
    {
        var act = () => new PolicyCatalog(
            [
                new TestPolicyContributor("A", PolicyDefinition.ForPermission("a.read", "A", "m:read")),
                new TestPolicyContributor(
                    "B",
                    PolicyDefinition.ForPermission("b.read", "B", "m:read").WithAliases("a.read"))
            ],
            PermissionsWith("m:read"));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*a.read*already a policy in its own right*");
    }

    [Fact]
    public void Two_policies_claiming_one_legacy_name_fail_the_composition()
    {
        var act = () => new PolicyCatalog(
            [
                new TestPolicyContributor(
                    "A", PolicyDefinition.ForPermission("a.read", "A", "m:read").WithAliases("Old.Read")),
                new TestPolicyContributor(
                    "B", PolicyDefinition.ForPermission("b.read", "B", "m:read").WithAliases("Old.Read"))
            ],
            PermissionsWith("m:read"));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Old.Read*declared by both*");
    }

    [Fact]
    public void A_blank_alias_fails_the_composition()
    {
        var act = () => new PolicyCatalog(
            [
                new TestPolicyContributor(
                    "M", PolicyDefinition.ForPermission("m.read", "M", "m:read").WithAliases("  "))
            ],
            PermissionsWith("m:read"));

        act.Should().Throw<InvalidOperationException>().WithMessage("*blank alias*");
    }

    /// <summary>
    /// <c>WithAliases</c> accumulates rather than replaces, so a policy renamed twice keeps both of
    /// the names it used to have rather than only the most recent one.
    /// </summary>
    [Fact]
    public void Renaming_twice_keeps_both_old_names()
    {
        var policy = PolicyDefinition.ForPermission("m.read", "M", "m:read")
            .WithAliases("M.Read")
            .WithAliases("m.legacy.read");

        policy.Aliases.Should().Equal("M.Read", "m.legacy.read");
    }
}
