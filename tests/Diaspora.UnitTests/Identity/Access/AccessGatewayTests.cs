using Diaspora.Application.Identity.Access;
using Diaspora.Application.Identity.Authorization;
using Diaspora.Application.Identity.Models;
using Diaspora.Domain.Identity.AccessStates;
using Diaspora.Domain.Identity.Trust;
using Diaspora.Domain.Identity.Users;
using Diaspora.UnitTests.Identity.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Identity.Access;

/// <summary>
/// The Access Gateway is the single place that decides where an authenticated member is
/// sent. These tests pin that contract, including the requirement that it has an answer
/// for every state on the ladder.
/// </summary>
public class AccessGatewayTests
{
    private static readonly AccessGateway Gateway = PlatformCatalogFixture.AccessGateway;

    [Fact]
    public void A_member_awaiting_phone_verification_is_sent_to_verify_their_phone()
    {
        var user = IdentityTestData.RegisterUser();
        user.BeginPhoneVerification(IdentityTestData.Now);

        var decision = Gateway.Resolve(user, IdentityTestData.Now);

        decision.Destination.Should().Be(AccessDestination.PhoneVerification);
        decision.RequiredActions.Should().NotBeEmpty();
        decision.Features.Granted.Should().BeEmpty();
    }

    [Fact]
    public void A_limited_access_member_lands_on_explore_with_the_verification_center()
    {
        var user = IdentityTestData.LimitedAccessUser();

        var decision = Gateway.Resolve(user, IdentityTestData.Now);

        decision.Destination.Should().Be(AccessDestination.ExploreLimited);
        decision.Features.Granted.Should().Contain(PlatformCatalogFixture.BrowseGuides);
        decision.Features.Granted.Should().Contain(IdentityFeatures.AccessVerificationCenter);
    }

    [Fact]
    public void A_limited_access_member_cannot_contribute()
    {
        var user = IdentityTestData.LimitedAccessUser();

        var decision = Gateway.Resolve(user, IdentityTestData.Now);
        var blocked = decision.Features.Blocked.Select(b => b.Feature).ToArray();

        blocked.Should().Contain(PlatformCatalogFixture.SendMessagesFeature);
        blocked.Should().Contain(PlatformCatalogFixture.PublishGuides);
        blocked.Should().Contain(PlatformCatalogFixture.CreateMarketplaceListings);
        blocked.Should().Contain(PlatformCatalogFixture.ApplyForJobsFeature);
        blocked.Should().Contain(PlatformCatalogFixture.CreateOrganizationsFeature);
    }

    [Fact]
    public void Blocked_contributor_features_say_which_state_unlocks_them()
    {
        var user = IdentityTestData.LimitedAccessUser();

        var decision = Gateway.Resolve(user, IdentityTestData.Now);
        var messaging = decision.Features.Blocked.Single(b => b.Feature == PlatformCatalogFixture.SendMessagesFeature);

        messaging.UnlockedAt.Should().Be(nameof(AccessState.Verified));
        messaging.Reason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void A_pending_verification_routes_to_the_dashboard()
    {
        var user = IdentityTestData.LimitedAccessUser();
        user.RequestVerification(IdentityTestData.Now);

        var decision = Gateway.Resolve(user, IdentityTestData.Now);

        decision.Destination.Should().Be(AccessDestination.VerificationDashboard);
    }

    [Fact]
    public void A_review_in_progress_routes_to_the_review_screen()
    {
        var user = IdentityTestData.LimitedAccessUser();
        user.RequestVerification(IdentityTestData.Now);
        user.ApplyTrustDecision(
            TrustDecision.Create(TrustDecisionOutcome.UnderReview, IdentityTestData.Now), IdentityTestData.Now);

        var decision = Gateway.Resolve(user, IdentityTestData.Now);

        decision.Destination.Should().Be(AccessDestination.VerificationUnderReview);
    }

    [Fact]
    public void A_verified_member_reaches_the_full_application()
    {
        var user = IdentityTestData.VerifiedUser();

        var decision = Gateway.Resolve(user, IdentityTestData.Now);

        decision.Destination.Should().Be(AccessDestination.FullApplication);
        decision.RequiredActions.Should().BeEmpty();
        decision.Features.Granted.Should().Contain(PlatformCatalogFixture.SendMessagesFeature);
        decision.Features.Granted.Should().Contain(PlatformCatalogFixture.PublishGuides);
        decision.Features.Granted.Should().Contain(PlatformCatalogFixture.CreateOrganizationsFeature);
    }

    [Fact]
    public void A_suspended_member_is_sent_to_the_suspension_screen_with_nothing_granted()
    {
        var user = IdentityTestData.VerifiedUser();
        var until = IdentityTestData.Now.AddDays(14);
        user.Suspend("Marketplace violation", until, IdentityTestData.Now);

        var decision = Gateway.Resolve(user, IdentityTestData.Now);

        decision.Destination.Should().Be(AccessDestination.SuspensionScreen);
        decision.RestrictedUntil.Should().Be(until);
        decision.Features.Granted.Should().BeEmpty();
        decision.Message.Should().Contain(until.ToString("u"));
    }

    [Fact]
    public void A_locked_member_is_sent_to_account_recovery()
    {
        var user = IdentityTestData.LimitedAccessUser();
        user.Lock("Suspected compromise", IdentityTestData.Now);

        var decision = Gateway.Resolve(user, IdentityTestData.Now);

        decision.Destination.Should().Be(AccessDestination.AccountRecovery);
    }

    [Fact]
    public void A_disabled_member_is_told_the_account_is_closed()
    {
        var user = IdentityTestData.LimitedAccessUser();
        user.Disable("Closed on request", IdentityTestData.Now);

        var decision = Gateway.Resolve(user, IdentityTestData.Now);

        decision.Destination.Should().Be(AccessDestination.AccountDisabled);
    }

    [Fact]
    public void The_gateway_has_a_route_for_every_access_state()
    {
        // If a state is ever added without routing, this fails rather than throwing at
        // runtime on the first member who reaches it.
        var user = IdentityTestData.LimitedAccessUser();
        var routed = new List<string>();

        foreach (var state in AccessState.All)
        {
            var probe = ProbeFor(state);
            var act = () => Gateway.Resolve(probe, IdentityTestData.Now);

            act.Should().NotThrow($"the gateway must route {state.Name}");
            routed.Add(state.Name);
        }

        routed.Should().HaveCount(AccessState.All.Count);
        user.Should().NotBeNull();
    }

    /// <summary>
    /// Walks a fresh aggregate to the requested state through legal transitions only, so
    /// the probe is a state the domain can actually produce.
    /// </summary>
    private static User ProbeFor(AccessState target)
    {
        var now = IdentityTestData.Now;

        if (target == AccessState.RegistrationStarted)
        {
            return IdentityTestData.RegisterUser();
        }

        if (target == AccessState.PhoneVerificationPending)
        {
            var pending = IdentityTestData.RegisterUser();
            pending.BeginPhoneVerification(now);
            return pending;
        }

        var user = IdentityTestData.LimitedAccessUser();

        if (target == AccessState.LimitedAccess) return user;

        if (target == AccessState.Suspended)
        {
            user.Suspend("probe", null, now);
            return user;
        }

        if (target == AccessState.Locked)
        {
            user.Lock("probe", now);
            return user;
        }

        if (target == AccessState.Disabled)
        {
            user.Disable("probe", now);
            return user;
        }

        user.RequestVerification(now);
        if (target == AccessState.VerificationPending) return user;

        user.ApplyTrustDecision(TrustDecision.Create(TrustDecisionOutcome.UnderReview, now), now);
        if (target == AccessState.VerificationUnderReview) return user;

        user.ApplyTrustDecision(TrustDecision.Create(TrustDecisionOutcome.Granted, now), now);
        return user;
    }
}
