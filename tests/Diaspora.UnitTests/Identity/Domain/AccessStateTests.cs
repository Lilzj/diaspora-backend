using Diaspora.Domain.Identity.AccessStates;
using Diaspora.SharedKernel.Primitives;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Identity.Domain;

public class AccessStateTests
{
    [Fact]
    public void Every_state_maps_to_an_account_status()
    {
        foreach (var state in AccessState.All)
        {
            var status = state.ToAccountStatus();
            Enum.IsDefined(status).Should().BeTrue($"{state.Name} must map to a defined AccountStatus");
        }
    }

    [Theory]
    [InlineData(nameof(AccessState.RegistrationStarted), false)]
    [InlineData(nameof(AccessState.PhoneVerificationPending), true)]
    [InlineData(nameof(AccessState.LimitedAccess), true)]
    [InlineData(nameof(AccessState.VerificationPending), true)]
    [InlineData(nameof(AccessState.VerificationUnderReview), true)]
    [InlineData(nameof(AccessState.Verified), true)]
    [InlineData(nameof(AccessState.Suspended), true)]
    [InlineData(nameof(AccessState.Locked), false)]
    [InlineData(nameof(AccessState.Disabled), false)]
    public void Authentication_is_allowed_only_where_intended(string stateName, bool expected)
    {
        var state = Enumeration.FromName<AccessState>(stateName);
        state.AllowsAuthentication.Should().Be(expected);
    }

    [Fact]
    public void Suspended_members_may_still_sign_in_so_they_can_appeal()
    {
        AccessState.Suspended.AllowsAuthentication.Should().BeTrue();
    }

    [Fact]
    public void Locked_and_disabled_members_cannot_sign_in()
    {
        AccessState.Locked.AllowsAuthentication.Should().BeFalse();
        AccessState.Disabled.AllowsAuthentication.Should().BeFalse();
    }

    [Fact]
    public void A_state_cannot_transition_to_itself()
    {
        foreach (var state in AccessState.All)
        {
            state.CanTransitionTo(state).Should().BeFalse($"{state.Name} should not self-transition");
        }
    }

    [Fact]
    public void Phone_verification_is_the_only_route_into_limited_access_from_onboarding()
    {
        AccessState.RegistrationStarted.CanTransitionTo(AccessState.LimitedAccess).Should().BeFalse();
        AccessState.PhoneVerificationPending.CanTransitionTo(AccessState.LimitedAccess).Should().BeTrue();
    }

    [Fact]
    public void Verified_is_reachable_only_from_the_verification_states()
    {
        var sources = AccessState.All
            .Where(state => state.CanTransitionTo(AccessState.Verified))
            .Select(state => state.Name)
            .ToArray();

        sources.Should().BeEquivalentTo(
            nameof(AccessState.VerificationUnderReview),
            nameof(AccessState.Suspended),
            nameof(AccessState.Locked));
    }

    [Fact]
    public void Limited_access_cannot_jump_straight_to_verified()
    {
        // The Trust Engine owns this promotion; skipping the request and review steps
        // would let Identity grant verification on its own.
        AccessState.LimitedAccess.CanTransitionTo(AccessState.Verified).Should().BeFalse();
    }

    [Fact]
    public void Disabled_accounts_reopen_at_limited_access_only()
    {
        AccessState.Disabled.AllowedTransitions.Should().ContainSingle()
            .Which.Should().Be(AccessState.LimitedAccess);
    }

    [Fact]
    public void Restricted_states_are_classified_consistently()
    {
        AccessState.Suspended.IsRestricted.Should().BeTrue();
        AccessState.Locked.IsRestricted.Should().BeTrue();
        AccessState.Disabled.IsRestricted.Should().BeTrue();
        AccessState.LimitedAccess.IsRestricted.Should().BeFalse();
        AccessState.Verified.IsRestricted.Should().BeFalse();
    }

    [Fact]
    public void Onboarded_covers_exactly_the_usable_states()
    {
        AccessState.All.Where(state => state.IsOnboarded).Select(state => state.Name)
            .Should().BeEquivalentTo(
                nameof(AccessState.LimitedAccess),
                nameof(AccessState.VerificationPending),
                nameof(AccessState.VerificationUnderReview),
                nameof(AccessState.Verified));
    }

    [Fact]
    public void Every_state_except_disabled_can_still_move_somewhere()
    {
        foreach (var state in AccessState.All)
        {
            state.AllowedTransitions.Should().NotBeEmpty(
                $"{state.Name} would otherwise be an inescapable dead end");
        }
    }
}
