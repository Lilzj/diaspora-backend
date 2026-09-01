using System.Net;
using Diaspora.Application.Identity.Authorization;
using Diaspora.Contracts.Authorization;
using Diaspora.Contracts.Identity.V1.Requests;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.IntegrationTests.Common;
using Diaspora.Platform.Authorization;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Identity;

/// <summary>
/// What each access tier actually receives, end to end, through the running host.
/// <para>
/// The unit tests assert the specification and the architecture tests assert that every module's
/// declarations conform to it. This suite closes the loop: it walks a real member up the ladder
/// and checks that the composed catalogue hands them <b>exactly</b> the capabilities the
/// specification says, at every rung.
/// </para>
/// <para>
/// Every expectation is computed from the catalogue and the semantics rather than hardcoded, so
/// a module adding a gate does not need this file edited — but a module adding a gate at the
/// <em>wrong tier</em> fails here as well as in the architecture suite.
/// </para>
/// </summary>
public sealed class AccessTierCapabilityTests : IdentityTestBase
{
    public AccessTierCapabilityTests(CustomWebApplicationFactory factory) : base(factory) { }

    private IReadOnlyList<FeatureDefinition> AllFeatures =>
        Factory.Services.GetRequiredService<IFeatureCatalog>().All;

    // ── Anonymous ────────────────────────────────────────────────────────

    [Fact]
    public async Task An_anonymous_caller_reaches_nothing_that_needs_a_subject()
    {
        var response = await Api.GetFeaturesAsync();

        response.StatusCode.Should().Be(
            HttpStatusCode.Unauthorized,
            "the anonymous tier admits no capability of any category");
    }

    // ── Onboarding ───────────────────────────────────────────────────────

    [Fact]
    public async Task An_onboarding_member_receives_exactly_the_onboarding_capabilities()
    {
        var auth = await SignInMidOnboardingAsync();

        AssertExactlyTheCapabilitiesFor(AccessTier.Onboarding, auth.Access.GrantedFeatures);
    }

    [Fact]
    public async Task An_onboarding_member_can_still_finish_onboarding()
    {
        // The loop-with-no-entrance test. If this list ever empties, a member cannot add a
        // profile photograph or submit verification evidence, and the reputation model scores
        // both.
        var auth = await SignInMidOnboardingAsync();

        auth.Access.GrantedFeatures.Should().NotBeEmpty();

        auth.Access.GrantedFeatures.Should().OnlyContain(
            name => CapabilityClassifier.Classify(Feature(name)) == CapabilityCategory.Onboarding);
    }

    [Fact]
    public async Task An_onboarding_member_holds_no_membership_privilege()
    {
        var auth = await SignInMidOnboardingAsync();

        auth.Access.GrantedFeatures.Should().NotContain(
            name => CapabilityClassifier.ReadsAsMembershipPrivilege(name));

        auth.Access.BlockedFeatures.Should().Contain(
            name => name.StartsWith("community.", StringComparison.Ordinal),
            "community participation begins at Limited and not before");
    }

    // ── Limited ──────────────────────────────────────────────────────────

    [Fact]
    public async Task A_limited_member_receives_exactly_the_member_capabilities()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        AssertExactlyTheCapabilitiesFor(AccessTier.Limited, auth.Access.GrantedFeatures);
    }

    [Fact]
    public async Task A_limited_member_keeps_everything_an_onboarding_member_had()
    {
        // Progression is cumulative. Verifying a phone number does not take away the ability to
        // change a profile photograph.
        var onboarding = await SignInMidOnboardingAsync();
        var (_, limited) = await RegisterAndVerifyAsync();

        limited.Access.GrantedFeatures.Should().Contain(onboarding.Access.GrantedFeatures);
    }

    [Fact]
    public async Task A_limited_member_can_participate_but_cannot_broadcast()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        var granted = auth.Access.GrantedFeatures;

        granted.Should().Contain("community.feed");
        granted.Should().Contain("community.commenting");
        granted.Should().Contain("community.reactions");

        auth.Access.BlockedFeatures.Should().Contain("community.posting");
        auth.Access.BlockedFeatures.Should().Contain("media.upload.video");
    }

    // ── PendingVerification ──────────────────────────────────────────────

    [Fact]
    public async Task A_member_awaiting_a_trust_decision_receives_the_member_capabilities()
    {
        var auth = await SubmitVerificationAndSignInAgainAsync();

        AssertExactlyTheCapabilitiesFor(
            AccessTier.PendingVerification, auth.Access.GrantedFeatures);
    }

    [Fact]
    public async Task Waiting_on_the_trust_engine_is_neither_a_promotion_nor_a_demotion()
    {
        var (_, limited) = await RegisterAndVerifyAsync();
        var pending = await SubmitVerificationAndSignInAgainAsync();

        // Identical but for the capabilities that stop applying once a request is in flight,
        // which say so with MaximumTier rather than by being a different tier's business.
        var closedOnSubmission = AllFeatures
            .Where(feature => feature.MaximumTier == AccessTier.Limited)
            .Select(feature => feature.Name)
            .ToList();

        closedOnSubmission.Should().NotBeEmpty(
            "the verification submission gate is the worked example of MaximumTier");

        pending.Access.GrantedFeatures
            .Should().BeEquivalentTo(
                limited.Access.GrantedFeatures.Except(closedOnSubmission, StringComparer.Ordinal));
    }

    // ── Verified ─────────────────────────────────────────────────────────

    [Fact]
    public async Task A_verified_member_receives_exactly_the_trusted_capabilities()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();

        AssertExactlyTheCapabilitiesFor(AccessTier.Verified, auth.Access.GrantedFeatures);
    }

    [Fact]
    public async Task A_verified_member_can_broadcast()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();

        auth.Access.GrantedFeatures.Should().Contain("community.posting");
        auth.Access.GrantedFeatures.Should().Contain("media.upload.video");
    }

    [Fact]
    public async Task A_verified_member_still_holds_no_staff_capability()
    {
        // The ladder is progression. Administration is a role, and no amount of verification
        // confers it — which is why there is no "Administrator" tier.
        var (_, auth) = await RegisterVerifiedMemberAsync();

        auth.Access.GrantedFeatures.Should().NotContain(
            name => Feature(name).StaffOnly);

        auth.Access.BlockedFeatures.Should().Contain(
            name => Feature(name).StaffOnly,
            "staff gates are declared and blocked, not absent");
    }

    [Fact]
    public async Task A_verified_member_is_refused_a_staff_route_over_http()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync("/api/v1/community/moderation/queue");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── The ladder as a whole ────────────────────────────────────────────

    [Fact]
    public async Task Each_rung_keeps_everything_below_it_except_what_declares_a_ceiling()
    {
        var onboarding = (await SignInMidOnboardingAsync()).Access.GrantedFeatures;
        var limited = (await RegisterAndVerifyAsync()).Auth.Access.GrantedFeatures;
        var verified = (await RegisterVerifiedMemberAsync()).Auth.Access.GrantedFeatures;

        limited.Should().Contain(onboarding);

        // Progression is cumulative with exactly one sanctioned exception: a capability that
        // stops applying as a member advances says so with MaximumTier. Submitting a
        // verification request is the worked example — once Trust has granted it, submitting
        // again is not a thing the member can do, and closing it is correct rather than a
        // regression.
        var ceilinged = AllFeatures
            .Where(feature => feature.MaximumTier is not null)
            .Select(feature => feature.Name)
            .ToList();

        ceilinged.Should().NotBeEmpty();

        verified.Should().Contain(limited.Except(ceilinged, StringComparer.Ordinal));

        verified.Should().NotContain(
            ceilinged,
            "a ceilinged capability closes above its ceiling — that is what MaximumTier is for");
    }

    /// <summary>
    /// The assertion every tier test shares: the granted set is <b>exactly</b> what the
    /// catalogue and the semantics jointly imply — no more, and no less.
    /// </summary>
    private void AssertExactlyTheCapabilitiesFor(
        AccessTier tier, IReadOnlyList<string> granted)
    {
        var expected = AllFeatures
            .Where(feature => !feature.StaffOnly)
            .Where(feature => tier >= feature.MinimumTier)
            .Where(feature => feature.MaximumTier is null || tier <= feature.MaximumTier)
            .Select(feature => feature.Name)
            .ToList();

        granted.Should().BeEquivalentTo(
            expected,
            $"{tier} must receive exactly what the composed catalogue declares for it");

        granted.Should().OnlyContain(
            name => AccessTierSemantics.Admits(tier, CapabilityClassifier.Classify(Feature(name))),
            $"every capability {tier} holds must belong to a category {tier} admits");
    }

    private FeatureDefinition Feature(string name) =>
        AllFeatures.Single(feature => feature.Name == name);

    /// <summary>Registers and signs in without verifying the phone number.</summary>
    private async Task<AuthenticationResponse> SignInMidOnboardingAsync()
    {
        var phone = await RegisterAsync();

        return await IdentityApiClient.ReadDataAsync<AuthenticationResponse>(
            await Api.LoginAsync(new LoginRequest
            {
                Email = EmailFor(phone),
                Password = ValidPassword
            }));
    }

    /// <summary>Reaches PendingVerification, then re-authenticates so the token carries it.</summary>
    private async Task<AuthenticationResponse> SubmitVerificationAndSignInAgainAsync()
    {
        var (phone, auth) = await RegisterAndVerifyAsync();

        Api.Authenticate(auth.Tokens.AccessToken);
        await Api.SubmitVerificationAsync();
        Api.ClearAuthentication();

        return await IdentityApiClient.ReadDataAsync<AuthenticationResponse>(
            await Api.LoginAsync(new LoginRequest
            {
                Email = EmailFor(phone),
                Password = ValidPassword
            }));
    }
}
