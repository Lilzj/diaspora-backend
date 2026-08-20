using Diaspora.Application.Identity.Authorization;
using Diaspora.Domain.Identity.AccessStates;
using Diaspora.Domain.Identity.Users;
using Diaspora.UnitTests.Identity.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Identity.Access;

/// <summary>
/// The permission resolver is now an adapter over the composed permission catalogue.
/// These tests build a catalogue from the real Identity contributor plus a stand-in
/// contributor module (see <see cref="PlatformCatalogFixture"/>), which is exactly how the
/// resolver behaves at runtime once every module has declared its permissions.
/// </summary>
public class PermissionResolverTests
{
    private static readonly PermissionResolver Resolver = PlatformCatalogFixture.PermissionResolver;

    [Fact]
    public void A_limited_access_member_can_manage_their_own_account()
    {
        var permissions = Resolver.Resolve(UserRole.CommunityUser, AccessState.LimitedAccess);

        permissions.Should().Contain(IdentityPermissions.ReadOwnProfile);
        permissions.Should().Contain(IdentityPermissions.UpdateOwnProfile);
        permissions.Should().Contain(IdentityPermissions.ChangeOwnPassword);
        permissions.Should().Contain(IdentityPermissions.RevokeOwnSessions);
        permissions.Should().Contain(IdentityPermissions.SubmitVerification);
    }

    [Fact]
    public void A_limited_access_member_holds_no_contributor_permissions()
    {
        var permissions = Resolver.Resolve(UserRole.CommunityUser, AccessState.LimitedAccess);

        permissions.Should().NotContain(PlatformCatalogFixture.PublishContent);
        permissions.Should().NotContain(PlatformCatalogFixture.SendMessages);
        permissions.Should().NotContain(PlatformCatalogFixture.CreateOrganizations);
    }

    [Fact]
    public void Verification_grants_the_contributor_permissions()
    {
        var permissions = Resolver.Resolve(UserRole.CommunityUser, AccessState.Verified);

        permissions.Should().Contain(PlatformCatalogFixture.PublishContent);
        permissions.Should().Contain(PlatformCatalogFixture.SendMessages);
        permissions.Should().Contain(PlatformCatalogFixture.ApplyForJobs);
        permissions.Should().Contain(PlatformCatalogFixture.CreateOrganizations);
    }

    [Fact]
    public void A_moderator_who_has_not_verified_still_moderates_but_does_not_contribute()
    {
        // Role and access state are independent axes; collapsing them would either
        // over-grant or under-grant here.
        var permissions = Resolver.Resolve(UserRole.Moderator, AccessState.LimitedAccess);

        permissions.Should().Contain(IdentityPermissions.SuspendUser);
        permissions.Should().NotContain(PlatformCatalogFixture.PublishContent);
    }

    [Fact]
    public void Administrators_hold_the_administration_permissions()
    {
        var permissions = Resolver.Resolve(UserRole.Admin, AccessState.Verified);

        permissions.Should().Contain(IdentityPermissions.DisableUser);
        permissions.Should().Contain(IdentityPermissions.AssignRoles);
        permissions.Should().Contain(IdentityPermissions.RevokeAnySession);
    }

    [Fact]
    public void Moderators_are_not_administrators()
    {
        var permissions = Resolver.Resolve(UserRole.Moderator, AccessState.Verified);

        permissions.Should().NotContain(IdentityPermissions.DisableUser);
        permissions.Should().NotContain(IdentityPermissions.AssignRoles);
    }

    [Fact]
    public void No_human_role_can_apply_a_trust_decision()
    {
        // The mechanical guarantee behind the B2/B3 split: the trust permission is declared
        // with a Service audience, so no tier and no role resolves to it.
        foreach (var role in UserRole.All)
        {
            foreach (var state in AccessState.All)
            {
                Resolver.Resolve(role, state)
                    .Should().NotContain(IdentityPermissions.ApplyTrustDecision,
                        $"{role.Name} in {state.Name} must not be able to grant verification");
            }
        }
    }

    [Fact]
    public void A_restricted_account_keeps_only_the_ability_to_read_its_own_profile()
    {
        foreach (var state in new[] { AccessState.Suspended, AccessState.Locked, AccessState.Disabled })
        {
            var permissions = Resolver.Resolve(UserRole.SuperAdmin, state);

            permissions.Should().ContainSingle().Which.Should().Be(IdentityPermissions.ReadOwnProfile);
        }
    }

    [Fact]
    public void An_unverified_phone_grants_almost_nothing()
    {
        var permissions = Resolver.Resolve(UserRole.CommunityUser, AccessState.PhoneVerificationPending);

        permissions.Should().ContainSingle().Which.Should().Be(IdentityPermissions.ReadOwnProfile);
    }
}
