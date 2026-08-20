using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Authorization;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Platform;

/// <summary>
/// The permission catalogue is the heart of B2.6's extensibility: it composes what every
/// module declares and resolves a subject against the whole. These tests pin the
/// composition rules and the tier/role/restriction/audience logic.
/// </summary>
public class PermissionCatalogTests
{
    private static PermissionDefinition Permission(
        string key,
        AccessTier? minimumTier = AccessTier.Limited,
        AccessTier? maximumTier = null,
        bool grantedWhenRestricted = false,
        PermissionAudience audience = PermissionAudience.Member,
        params string[] roles) =>
        new(key, "TestModule", key)
        {
            MinimumTier = minimumTier,
            MaximumTier = maximumTier,
            GrantedWhenRestricted = grantedWhenRestricted,
            Audience = audience,
            Roles = new HashSet<string>(roles, StringComparer.Ordinal)
        };

    private static AuthorizationSubject Subject(
        AccessTier tier = AccessTier.Limited,
        string role = "CommunityUser",
        bool isStaff = false,
        bool isRestricted = false) =>
        new(tier, role, isStaff, isRestricted);

    [Fact]
    public void It_composes_permissions_from_every_contributor()
    {
        var catalog = new PermissionCatalog(
        [
            new TestPermissionContributor("A", Permission("a:read")),
            new TestPermissionContributor("B", Permission("b:read"))
        ]);

        catalog.All.Select(p => p.Key).Should().BeEquivalentTo("a:read", "b:read");
    }

    [Fact]
    public void Two_modules_declaring_the_same_permission_fail_the_composition()
    {
        var act = () => new PermissionCatalog(
        [
            new TestPermissionContributor("A", Permission("shared:key")),
            new TestPermissionContributor("B", Permission("shared:key"))
        ]);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*shared:key*A*B*");
    }

    [Fact]
    public void A_permission_is_granted_only_at_or_above_its_minimum_tier()
    {
        var catalog = new PermissionCatalog(
            [new TestPermissionContributor("M", Permission("m:contribute", minimumTier: AccessTier.Verified))]);

        catalog.IsGranted("m:contribute", Subject(AccessTier.Limited)).Should().BeFalse();
        catalog.IsGranted("m:contribute", Subject(AccessTier.Verified)).Should().BeTrue();
    }

    [Fact]
    public void A_maximum_tier_closes_a_permission_as_the_member_progresses()
    {
        var catalog = new PermissionCatalog(
        [
            new TestPermissionContributor("M",
                Permission("m:submit", minimumTier: AccessTier.Limited, maximumTier: AccessTier.Limited))
        ]);

        catalog.IsGranted("m:submit", Subject(AccessTier.Limited)).Should().BeTrue();
        catalog.IsGranted("m:submit", Subject(AccessTier.PendingVerification)).Should().BeFalse();
    }

    [Fact]
    public void Role_scoped_permissions_are_held_only_by_those_roles()
    {
        var catalog = new PermissionCatalog(
            [new TestPermissionContributor("M", Permission("m:moderate", roles: "Moderator"))]);

        catalog.IsGranted("m:moderate", Subject(role: "Moderator")).Should().BeTrue();
        catalog.IsGranted("m:moderate", Subject(role: "CommunityUser")).Should().BeFalse();
    }

    [Fact]
    public void A_restricted_subject_holds_only_permissions_marked_to_survive_restriction()
    {
        var catalog = new PermissionCatalog(
        [
            new TestPermissionContributor("M",
                Permission("m:read-self", grantedWhenRestricted: true),
                Permission("m:do-things"))
        ]);

        var restricted = Subject(isRestricted: true);

        catalog.Resolve(restricted).Should().ContainSingle().Which.Should().Be("m:read-self");
    }

    [Fact]
    public void A_service_permission_is_never_resolved_from_a_tier_or_role()
    {
        // The mechanical Identity/Trust separation: no member subject, at any tier or role,
        // resolves a Service-audience permission.
        var catalog = new PermissionCatalog(
        [
            new TestPermissionContributor("Trust",
                Permission("trust:apply", minimumTier: null, audience: PermissionAudience.Service))
        ]);

        foreach (var tier in Enum.GetValues<AccessTier>())
        {
            catalog.IsGranted("trust:apply", Subject(tier, "SuperAdmin", isStaff: true)).Should().BeFalse();
        }

        catalog.Resolve(Subject(AccessTier.Verified, "SuperAdmin", isStaff: true))
            .Should().NotContain("trust:apply");
    }

    [Fact]
    public void An_anonymous_subject_holds_nothing()
    {
        var catalog = new PermissionCatalog(
            [new TestPermissionContributor("M", Permission("m:read", minimumTier: AccessTier.Limited))]);

        catalog.Resolve(AuthorizationSubject.Anonymous).Should().BeEmpty();
    }

    [Fact]
    public void An_undeclared_permission_is_never_granted()
    {
        var catalog = new PermissionCatalog([]);

        catalog.IsGranted("nobody:declares:this", Subject(AccessTier.Verified)).Should().BeFalse();
        catalog.Find("nobody:declares:this").Should().BeNull();
    }
}
