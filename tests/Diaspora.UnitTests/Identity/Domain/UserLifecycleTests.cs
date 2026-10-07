using Diaspora.Domain.Identity.AccessStates;
using Diaspora.Domain.Identity.Events;
using Diaspora.Domain.Identity.Trust;
using Diaspora.Domain.Identity.ValueObjects;
using Diaspora.UnitTests.Identity.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Identity.Domain;

public class UserLifecycleTests
{
    [Fact]
    public void A_limited_access_member_can_request_verification()
    {
        var user = IdentityTestData.LimitedAccessUser();

        var result = user.RequestVerification(IdentityTestData.Now);

        result.IsSuccess.Should().BeTrue();
        user.AccessState.Should().Be(AccessState.VerificationPending);
        user.DomainEvents.Should().Contain(e => e is UserVerificationRequestedDomainEvent);
    }

    [Fact]
    public void Verification_cannot_be_requested_twice()
    {
        var user = IdentityTestData.LimitedAccessUser();
        user.RequestVerification(IdentityTestData.Now);

        var second = user.RequestVerification(IdentityTestData.Now);

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("Identity.User.VerificationAlreadyRequested");
    }

    [Fact]
    public void Identity_cannot_grant_verification_without_the_trust_engine()
    {
        // The architectural boundary in one assertion: there is no path from limited
        // access to Verified that does not pass through a trust decision.
        var user = IdentityTestData.LimitedAccessUser();

        var result = user.ApplyTrustDecision(
            TrustDecision.Create(TrustDecisionOutcome.Granted, IdentityTestData.Now),
            IdentityTestData.Now);

        result.IsFailure.Should().BeTrue();
        user.AccessState.Should().Be(AccessState.LimitedAccess);
    }

    [Fact]
    public void A_granted_trust_decision_promotes_a_reviewed_member_to_verified()
    {
        var user = IdentityTestData.LimitedAccessUser();
        user.RequestVerification(IdentityTestData.Now);
        user.ApplyTrustDecision(
            TrustDecision.Create(TrustDecisionOutcome.UnderReview, IdentityTestData.Now), IdentityTestData.Now);

        var result = user.ApplyTrustDecision(
            TrustDecision.Create(TrustDecisionOutcome.Granted, IdentityTestData.Now), IdentityTestData.Now);

        result.IsSuccess.Should().BeTrue();
        user.AccessState.Should().Be(AccessState.Verified);
    }

    [Fact]
    public void A_grant_straight_from_the_queue_still_verifies_the_member()
    {
        // The console approves without assigning first, so no UnderReview decision is
        // ever sent. Before 7 Oct 2026 this grant was refused and the member stayed on
        // VerificationPending with an approved verification.
        var user = IdentityTestData.LimitedAccessUser();
        user.RequestVerification(IdentityTestData.Now);

        var result = user.ApplyTrustDecision(
            TrustDecision.Create(TrustDecisionOutcome.Granted, IdentityTestData.Now), IdentityTestData.Now);

        result.IsSuccess.Should().BeTrue();
        user.AccessState.Should().Be(AccessState.Verified);
    }

    [Fact]
    public void A_repeated_grant_is_accepted_and_changes_nothing()
    {
        var user = IdentityTestData.LimitedAccessUser();
        user.RequestVerification(IdentityTestData.Now);
        user.ApplyTrustDecision(
            TrustDecision.Create(TrustDecisionOutcome.Granted, IdentityTestData.Now), IdentityTestData.Now);
        user.ClearDomainEvents();

        var again = user.ApplyTrustDecision(
            TrustDecision.Create(TrustDecisionOutcome.Granted, IdentityTestData.Now), IdentityTestData.Now);

        again.IsSuccess.Should().BeTrue();
        user.AccessState.Should().Be(AccessState.Verified);
        user.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void A_rejected_trust_decision_returns_the_member_to_limited_access()
    {
        var user = IdentityTestData.LimitedAccessUser();
        user.RequestVerification(IdentityTestData.Now);

        var result = user.ApplyTrustDecision(
            TrustDecision.Create(TrustDecisionOutcome.Rejected, IdentityTestData.Now, "Documents unreadable"),
            IdentityTestData.Now);

        result.IsSuccess.Should().BeTrue();
        user.AccessState.Should().Be(AccessState.LimitedAccess);
        user.StatusReason.Should().Be("Documents unreadable");
    }

    [Fact]
    public void The_trust_engine_can_revoke_verification()
    {
        var user = IdentityTestData.VerifiedUser();

        var result = user.ApplyTrustDecision(
            TrustDecision.Create(TrustDecisionOutcome.Revoked, IdentityTestData.Now), IdentityTestData.Now);

        result.IsSuccess.Should().BeTrue();
        user.AccessState.Should().Be(AccessState.LimitedAccess);
    }

    [Fact]
    public void Suspension_records_the_reason_and_the_end_date()
    {
        var user = IdentityTestData.LimitedAccessUser();
        var until = IdentityTestData.Now.AddDays(7);

        var result = user.Suspend("Repeated marketplace violations", until, IdentityTestData.Now);

        result.IsSuccess.Should().BeTrue();
        user.AccessState.Should().Be(AccessState.Suspended);
        user.AccountStatus.Should().Be(AccountStatus.Suspended);
        user.SuspendedUntil.Should().Be(until);
        user.StatusReason.Should().Be("Repeated marketplace violations");
    }

    [Fact]
    public void Reinstatement_restores_the_rung_the_member_was_on()
    {
        // A verified member who is suspended and reinstated must come back verified,
        // not demoted to limited access.
        var user = IdentityTestData.VerifiedUser();
        user.Suspend("Under review", null, IdentityTestData.Now);

        var result = user.Reinstate(IdentityTestData.Now, AccessStateChangeReason.ModerationReinstatement);

        result.IsSuccess.Should().BeTrue();
        user.AccessState.Should().Be(AccessState.Verified);
        user.SuspendedUntil.Should().BeNull();
        user.StatusReason.Should().BeNull();
    }

    [Fact]
    public void Chained_restrictions_do_not_overwrite_the_original_rung()
    {
        var user = IdentityTestData.VerifiedUser();
        user.Suspend("First", null, IdentityTestData.Now);
        user.Lock("Then locked", IdentityTestData.Now);

        user.Reinstate(IdentityTestData.Now, AccessStateChangeReason.AccountRecovery);

        user.AccessState.Should().Be(AccessState.Verified,
            "the remembered state should still be the pre-restriction one");
    }

    [Fact]
    public void A_hard_lock_rotates_the_security_stamp()
    {
        var user = IdentityTestData.LimitedAccessUser();
        var before = user.Security.SecurityStamp;

        user.Lock("Suspected credential theft", IdentityTestData.Now);

        user.AccessState.Should().Be(AccessState.Locked);
        user.Security.SecurityStamp.Should().BeGreaterThan(before);
        user.DomainEvents.Should().Contain(e => e is UserAccountLockedDomainEvent);
    }

    [Fact]
    public void Disabling_an_account_rotates_the_security_stamp()
    {
        var user = IdentityTestData.LimitedAccessUser();
        var before = user.Security.SecurityStamp;

        user.Disable("Closed on request", IdentityTestData.Now);

        user.AccessState.Should().Be(AccessState.Disabled);
        user.AccountStatus.Should().Be(AccountStatus.Disabled);
        user.Security.SecurityStamp.Should().BeGreaterThan(before);
    }

    [Fact]
    public void Reactivation_re_enters_at_limited_access()
    {
        // Trust has to be re-earned; a reopened account does not resume as verified.
        var user = IdentityTestData.VerifiedUser();
        user.Disable("Closed on request", IdentityTestData.Now);

        var result = user.Reactivate(IdentityTestData.Now);

        result.IsSuccess.Should().BeTrue();
        user.AccessState.Should().Be(AccessState.LimitedAccess);
    }

    [Fact]
    public void Reinstating_an_unrestricted_account_is_refused()
    {
        var user = IdentityTestData.LimitedAccessUser();

        var result = user.Reinstate(IdentityTestData.Now, AccessStateChangeReason.ModerationReinstatement);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Updating_the_profile_reports_only_the_fields_that_changed()
    {
        var user = IdentityTestData.LimitedAccessUser();

        user.UpdateProfile(
            FullName.Create("Amara N. Okafor"),
            user.Nationality,
            user.CountryOfResidence,
            user.PreferredLanguage,
            user.PreferredTimeZone,
            IdentityTestData.Now);

        var updated = user.DomainEvents.OfType<UserProfileUpdatedDomainEvent>().Single();
        updated.ChangedFields.Should().ContainSingle().Which.Should().Be("FullName");
    }

    [Fact]
    public void An_unchanged_profile_raises_no_event()
    {
        var user = IdentityTestData.LimitedAccessUser();

        user.UpdateProfile(
            user.FullName,
            user.Nationality,
            user.CountryOfResidence,
            user.PreferredLanguage,
            user.PreferredTimeZone,
            IdentityTestData.Now);

        user.DomainEvents.Should().BeEmpty();
    }
}
