using Diaspora.Domain.Identity.AccessStates;
using Diaspora.Domain.Identity.Events;
using Diaspora.UnitTests.Identity.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Identity.Domain;

public class UserRegistrationTests
{
    [Fact]
    public void Registration_starts_below_phone_verification()
    {
        var user = IdentityTestData.RegisterUser();

        user.AccessState.Should().Be(AccessState.RegistrationStarted);
        user.AccountStatus.Should().Be(AccountStatus.Provisioning);
        user.IsPhoneVerified.Should().BeFalse();
    }

    [Fact]
    public void Registration_raises_the_registered_event()
    {
        var user = IdentityTestData.RegisterUser();

        user.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<UserRegisteredDomainEvent>();
    }

    [Fact]
    public void A_new_account_grants_no_access_until_the_phone_is_confirmed()
    {
        var user = IdentityTestData.RegisterUser();

        user.EnsureCanAuthenticate(IdentityTestData.Now).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Beginning_phone_verification_moves_to_the_pending_state()
    {
        var user = IdentityTestData.RegisterUser();

        var result = user.BeginPhoneVerification(IdentityTestData.Now);

        result.IsSuccess.Should().BeTrue();
        user.AccessState.Should().Be(AccessState.PhoneVerificationPending);
    }

    [Fact]
    public void Confirming_the_phone_promotes_to_limited_access()
    {
        var user = IdentityTestData.RegisterUser();
        user.BeginPhoneVerification(IdentityTestData.Now);

        var result = user.ConfirmPhoneVerified(IdentityTestData.Now);

        result.IsSuccess.Should().BeTrue();
        user.AccessState.Should().Be(AccessState.LimitedAccess);
        user.AccountStatus.Should().Be(AccountStatus.Active);
        user.IsPhoneVerified.Should().BeTrue();
        user.Security.PhoneVerifiedAt.Should().Be(IdentityTestData.Now);
    }

    [Fact]
    public void Confirming_the_phone_raises_both_verification_and_state_events()
    {
        var user = IdentityTestData.RegisterUser();
        user.BeginPhoneVerification(IdentityTestData.Now);
        user.ClearDomainEvents();

        user.ConfirmPhoneVerified(IdentityTestData.Now);

        user.DomainEvents.Should().Contain(e => e is UserAccessStateChangedDomainEvent);
        user.DomainEvents.Should().Contain(e => e is UserPhoneVerifiedDomainEvent);
    }

    [Fact]
    public void The_phone_cannot_be_verified_twice()
    {
        var user = IdentityTestData.LimitedAccessUser();

        var result = user.ConfirmPhoneVerified(IdentityTestData.Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.User.PhoneAlreadyVerified");
    }

    [Fact]
    public void Confirming_the_phone_out_of_order_is_refused()
    {
        // Straight from RegistrationStarted, without the code ever being dispatched.
        var user = IdentityTestData.RegisterUser();

        var result = user.ConfirmPhoneVerified(IdentityTestData.Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.User.InvalidStateTransition");
    }
}
