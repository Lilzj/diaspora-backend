using Diaspora.Domain.Identity.AccessStates;
using Diaspora.Domain.Identity.Events;
using Diaspora.Domain.Identity.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Diaspora.UnitTests.Identity.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Identity.Domain;

public class UserAuthenticationTests
{
    private static readonly LockoutPolicy Policy = LockoutPolicy.Create(3, TimeSpan.FromMinutes(15));

    [Fact]
    public void A_verified_member_may_authenticate()
    {
        var user = IdentityTestData.LimitedAccessUser();

        user.EnsureCanAuthenticate(IdentityTestData.Now).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Failed_sign_ins_accumulate()
    {
        var user = IdentityTestData.LimitedAccessUser();

        user.RecordFailedLogin(IdentityTestData.Now, Policy);
        user.RecordFailedLogin(IdentityTestData.Now, Policy);

        user.Security.FailedLoginAttempts.Should().Be(2);
        user.Security.IsLockedOut(IdentityTestData.Now).Should().BeFalse();
    }

    [Fact]
    public void The_attempt_budget_triggers_a_temporary_lockout()
    {
        var user = IdentityTestData.LimitedAccessUser();

        for (var i = 0; i < 3; i++)
        {
            user.RecordFailedLogin(IdentityTestData.Now, Policy);
        }

        user.Security.IsLockedOut(IdentityTestData.Now).Should().BeTrue();
        user.EnsureCanAuthenticate(IdentityTestData.Now).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void A_temporary_lockout_does_not_move_the_member_off_the_access_ladder()
    {
        // The distinction that keeps a forgotten password from looking like a security
        // incident: the counter locks, the access state does not change.
        var user = IdentityTestData.LimitedAccessUser();

        for (var i = 0; i < 5; i++)
        {
            user.RecordFailedLogin(IdentityTestData.Now, Policy);
        }

        user.AccessState.Should().Be(AccessState.LimitedAccess);
        user.AccountStatus.Should().Be(AccountStatus.Active);
    }

    [Fact]
    public void A_temporary_lockout_heals_on_its_own()
    {
        var user = IdentityTestData.LimitedAccessUser();

        for (var i = 0; i < 3; i++)
        {
            user.RecordFailedLogin(IdentityTestData.Now, Policy);
        }

        var afterLockout = IdentityTestData.Now.AddMinutes(16);

        user.Security.IsLockedOut(afterLockout).Should().BeFalse();
        user.EnsureCanAuthenticate(afterLockout).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void A_lockout_reports_how_long_is_left()
    {
        var user = IdentityTestData.LimitedAccessUser();

        for (var i = 0; i < 3; i++)
        {
            user.RecordFailedLogin(IdentityTestData.Now, Policy);
        }

        var result = user.EnsureCanAuthenticate(IdentityTestData.Now.AddMinutes(5));

        result.Error.Type.Should().Be(ErrorType.RateLimited);
        result.Error.Description.Should().Contain("600");
    }

    [Fact]
    public void A_successful_sign_in_clears_the_failure_counters()
    {
        var user = IdentityTestData.LimitedAccessUser();
        user.RecordFailedLogin(IdentityTestData.Now, Policy);
        user.RecordFailedLogin(IdentityTestData.Now, Policy);

        user.RecordSuccessfulLogin(SessionId.New(), IdentityTestData.Now);

        user.Security.FailedLoginAttempts.Should().Be(0);
        user.Security.LockoutEndsAt.Should().BeNull();
        user.LastLoginAt.Should().Be(IdentityTestData.Now);
    }

    [Fact]
    public void A_successful_sign_in_raises_the_logged_in_event()
    {
        var user = IdentityTestData.LimitedAccessUser();

        user.RecordSuccessfulLogin(SessionId.New(), IdentityTestData.Now);

        user.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<UserLoggedInDomainEvent>();
    }

    [Fact]
    public void A_locked_account_cannot_authenticate()
    {
        var user = IdentityTestData.LimitedAccessUser();
        user.Lock("Suspected compromise", IdentityTestData.Now);

        var result = user.EnsureCanAuthenticate(IdentityTestData.Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.User.AccountLocked");
    }

    [Fact]
    public void A_disabled_account_cannot_authenticate()
    {
        var user = IdentityTestData.LimitedAccessUser();
        user.Disable("Closed on request", IdentityTestData.Now);

        var result = user.EnsureCanAuthenticate(IdentityTestData.Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.User.AccountDisabled");
    }

    [Fact]
    public void A_suspended_account_can_still_reach_the_suspension_screen()
    {
        var user = IdentityTestData.LimitedAccessUser();
        user.Suspend("Under review", null, IdentityTestData.Now);

        user.EnsureCanAuthenticate(IdentityTestData.Now).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void A_soft_deleted_account_cannot_authenticate()
    {
        var user = IdentityTestData.LimitedAccessUser();
        user.MarkDeleted(IdentityTestData.Now, "admin");

        user.EnsureCanAuthenticate(IdentityTestData.Now).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Soft_deletion_rotates_the_security_stamp_so_live_tokens_stop_working()
    {
        var user = IdentityTestData.LimitedAccessUser();
        var before = user.Security.SecurityStamp;

        user.MarkDeleted(IdentityTestData.Now, "admin");

        user.Security.SecurityStamp.Should().BeGreaterThan(before);
    }
}
