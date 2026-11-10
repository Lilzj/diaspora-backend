using Diaspora.Application.Identity.Abstractions;
using Diaspora.Contracts.Authorization;
using Diaspora.Domain.Identity.AccessStates;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Users;
using Diaspora.Infrastructure.Identity.Authorization;
using Diaspora.Platform.Authorization;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Identity.Access;

/// <summary>
/// <c>ClaimsPermissionService.AuthorizeAsync</c>, which resolves a policy name through the
/// catalogue (BSEC-003, 10 Nov 2026).
/// <para>
/// <b>These exist because the method used to be <c>HasPermissionAsync(policy)</c> and could only
/// ever answer false.</b> Policy names are dotted and permission keys are colon-separated, on
/// purpose — so passing one to the other asked whether the caller held a permission that cannot
/// exist. Nothing caught it because the only caller is <c>AuthorizationBehavior</c>'s
/// <c>Policy</c> arm and no request type had ever set that field.
/// </para>
/// <para>
/// The first test is the one that would have failed before: a caller holding exactly the permission
/// the policy maps to is authorised <em>by the policy name</em>. The rest pin the answers a gate
/// has to get right when it cannot resolve the question.
/// </para>
/// </summary>
public class ClaimsPermissionServicePolicyTests
{
    private const string PolicyName = "testing.widgets.read";
    private const string PermissionKey = "testing:widgets:read";
    private const string Module = "Testing";

    [Fact]
    public async Task A_permission_backed_policy_is_satisfied_by_the_permission_behind_it()
    {
        var service = Service(
            permissions: [PermissionKey],
            policies: [PolicyDefinition.ForPermission(PolicyName, Module, PermissionKey)]);

        (await service.AuthorizeAsync(PolicyName)).Should().BeTrue(
            "the policy name and the permission key are different strings for the same decision, and "
            + "the catalogue is what joins them");
    }

    /// <summary>
    /// The precise shape of the old defect: the caller holds the permission, and the policy name
    /// itself is not a permission anybody can hold.
    /// </summary>
    [Fact]
    public async Task The_policy_name_is_not_itself_treated_as_a_permission()
    {
        var service = Service(
            permissions: [PolicyName],
            policies: [PolicyDefinition.ForPermission(PolicyName, Module, PermissionKey)]);

        (await service.AuthorizeAsync(PolicyName)).Should().BeFalse(
            "holding a permission spelled like the policy proves nothing — and before this was "
            + "fixed, that was the only way to pass");
    }

    [Fact]
    public async Task A_policy_nothing_declares_is_refused()
    {
        var service = Service(permissions: [PermissionKey], policies: []);

        (await service.AuthorizeAsync(PolicyName)).Should().BeFalse(
            "a typo, or a module this host does not compose, must not open a gate");
    }

    [Theory]
    [InlineData(AccessTier.Anonymous, false)]
    [InlineData(AccessTier.Onboarding, false)]
    [InlineData(AccessTier.Limited, true)]
    [InlineData(AccessTier.Verified, true)]
    public async Task A_tier_backed_policy_compares_the_callers_tier(AccessTier tier, bool expected)
    {
        var service = Service(
            permissions: [],
            policies: [PolicyDefinition.ForTier(PolicyName, Module, AccessTier.Limited)],
            subject: new AuthorizationSubject(tier, "Member", IsStaff: false, IsRestricted: false));

        (await service.AuthorizeAsync(PolicyName)).Should().Be(expected);
    }

    /// <summary>
    /// A restricted account never satisfies a tier gate, however high the tier its token asserts.
    /// The same exclusion <c>AccessTierAuthorizationHandler</c> applies at the HTTP layer, so the
    /// two cannot disagree about a suspended member.
    /// </summary>
    [Fact]
    public async Task A_restricted_caller_never_satisfies_a_tier_policy()
    {
        var service = Service(
            permissions: [],
            policies: [PolicyDefinition.ForTier(PolicyName, Module, AccessTier.Limited)],
            subject: new AuthorizationSubject(
                AccessTier.Verified, "Member", IsStaff: false, IsRestricted: true));

        (await service.AuthorizeAsync(PolicyName)).Should().BeFalse();
    }

    [Fact]
    public async Task An_empty_policy_name_is_refused()
    {
        var service = Service(permissions: [PermissionKey], policies: []);

        (await service.AuthorizeAsync("  ")).Should().BeFalse();
    }

    private static ClaimsPermissionService Service(
        string[] permissions,
        PolicyDefinition[] policies,
        AuthorizationSubject? subject = null)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.IsAuthenticated.Returns(true);
        currentUser.Permissions.Returns(permissions);

        var currentIdentity = Substitute.For<ICurrentIdentityContext>();
        currentIdentity.UserId.Returns(UserId.From(Guid.NewGuid()));

        var catalog = Substitute.For<IPolicyCatalog>();
        catalog.Find(Arg.Any<string>()).Returns(call =>
            policies.FirstOrDefault(policy => policy.Name == call.Arg<string>()));

        var subjects = Substitute.For<IAuthorizationSubjectAccessor>();
        subjects.Current.Returns(subject ?? AuthorizationSubject.Anonymous);

        return new ClaimsPermissionService(
            currentUser,
            currentIdentity,
            Substitute.For<IPermissionResolver>(),
            Substitute.For<IUserRepository>(),
            catalog,
            subjects);
    }
}
