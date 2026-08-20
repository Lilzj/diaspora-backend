using System.Net;
using Diaspora.Application.Identity.Authorization;
using Diaspora.Contracts.Identity.V1.Requests;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Identity;

/// <summary>
/// The progressive access ladder and the B2/B3 trust boundary, exercised over HTTP.
/// </summary>
public class ProgressiveAccessTests : IdentityTestBase
{
    public ProgressiveAccessTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_limited_access_member_sees_explore_and_the_verification_center()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        Api.Authenticate(auth.Tokens.AccessToken);
        var state = await IdentityApiClient.ReadDataAsync<AccessStateResponse>(
            await Api.GetAccessStateAsync());

        state.State.Should().Be("LimitedAccess");
        state.Decision.Destination.Should().Be("ExploreLimited");
        state.Decision.GrantedFeatures.Should().Contain(IdentityFeatures.AccessVerificationCenter);
    }

    [Fact]
    public async Task A_limited_access_member_cannot_reach_the_contributor_features()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        Api.Authenticate(auth.Tokens.AccessToken);
        var features = await IdentityApiClient.ReadDataAsync<FeatureAccessResponse>(
            await Api.GetFeaturesAsync());

        var blocked = features.Features.Where(f => !f.IsEnabled).Select(f => f.Name).ToArray();

        blocked.Should().Contain(PlatformFeatureNames.SendMessages);
        blocked.Should().Contain(PlatformFeatureNames.PublishGuides);
        blocked.Should().Contain(PlatformFeatureNames.CreateMarketplaceListings);
        blocked.Should().Contain(PlatformFeatureNames.ApplyForJobs);
        blocked.Should().Contain(PlatformFeatureNames.CreateOrganizations);
    }

    [Fact]
    public async Task Blocked_features_explain_what_would_unlock_them()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        Api.Authenticate(auth.Tokens.AccessToken);
        var features = await IdentityApiClient.ReadDataAsync<FeatureAccessResponse>(
            await Api.GetFeaturesAsync());

        var messaging = features.Features.Single(f => f.Name == PlatformFeatureNames.SendMessages);

        messaging.IsEnabled.Should().BeFalse();
        messaging.UnlockedAt.Should().Be("Verified");
        messaging.BlockedReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Requesting_verification_moves_the_member_to_pending()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        Api.Authenticate(auth.Tokens.AccessToken);
        var response = await Api.SubmitVerificationAsync("Please review my residency documents.");

        var state = await IdentityApiClient.ReadDataAsync<AccessStateResponse>(response);

        state.State.Should().Be("VerificationPending");
        state.Decision.Destination.Should().Be("VerificationDashboard");
    }

    [Fact]
    public async Task Verification_cannot_be_requested_twice()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        Api.Authenticate(auth.Tokens.AccessToken);
        await Api.SubmitVerificationAsync();

        var second = await Api.SubmitVerificationAsync();

        // 409, not 403 — and that distinction is the point. The access token still carries
        // the permission snapshot from when the member held limited access, so the
        // authorisation layer lets the request through; the aggregate refuses it. Token
        // claims are a fast path, never the last word, and this is what defence in depth
        // buys: a stale snapshot cannot produce an illegal state transition.
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_refreshed_token_reflects_the_new_access_state()
    {
        // The other half of the story above: once the client rotates its token, the
        // snapshot catches up and the authorisation layer refuses the request outright.
        var (_, auth) = await RegisterAndVerifyAsync();

        Api.Authenticate(auth.Tokens.AccessToken);
        await Api.SubmitVerificationAsync();
        Api.ClearAuthentication();

        var refreshed = await IdentityApiClient.ReadDataAsync<AuthenticationResponse>(
            await Api.RefreshAsync(auth.Tokens.RefreshToken));

        refreshed.User.AccessState.Should().Be("VerificationPending");

        Api.Authenticate(refreshed.Tokens.AccessToken);
        var second = await Api.SubmitVerificationAsync();

        second.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Identity_cannot_verify_a_member_on_its_own()
    {
        // The architectural boundary, end to end: there is no member-reachable endpoint
        // that produces the Verified state.
        var (_, auth) = await RegisterAndVerifyAsync();

        Api.Authenticate(auth.Tokens.AccessToken);
        await Api.SubmitVerificationAsync();

        var attempt = await Api.ApplyTrustDecisionAsync(new ApplyTrustDecisionRequest
        {
            UserId = auth.User.Id,
            Outcome = "Granted"
        });

        attempt.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var state = await IdentityApiClient.ReadDataAsync<AccessStateResponse>(
            await Api.GetAccessStateAsync());

        state.State.Should().Be("VerificationPending");
    }

    [Fact]
    public async Task A_service_token_without_the_trust_permission_is_refused()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        using var client = Factory.CreateClient();
        var api = new IdentityApiClient(client);
        api.Authenticate(TrustEngineTokenFactory.CreateWithoutPermissions());

        var response = await api.ApplyTrustDecisionAsync(new ApplyTrustDecisionRequest
        {
            UserId = auth.User.Id,
            Outcome = "Granted"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_trust_engine_can_grant_verification()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        Api.Authenticate(auth.Tokens.AccessToken);
        await Api.SubmitVerificationAsync();

        (await ApplyTrustDecisionAsync(auth.User.Id, "UnderReview")).IsSuccessStatusCode.Should().BeTrue();

        var granted = await ApplyTrustDecisionAsync(auth.User.Id, "Granted");
        var state = await IdentityApiClient.ReadDataAsync<AccessStateResponse>(granted);

        state.State.Should().Be("Verified");
        state.Decision.Destination.Should().Be("FullApplication");
    }

    [Fact]
    public async Task Verification_unlocks_the_contributor_features()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();

        Api.Authenticate(auth.Tokens.AccessToken);
        var features = await IdentityApiClient.ReadDataAsync<FeatureAccessResponse>(
            await Api.GetFeaturesAsync());

        var granted = features.Features.Where(f => f.IsEnabled).Select(f => f.Name).ToArray();

        granted.Should().Contain(PlatformFeatureNames.SendMessages);
        granted.Should().Contain(PlatformFeatureNames.PublishGuides);
        granted.Should().Contain(PlatformFeatureNames.CreateMarketplaceListings);
        granted.Should().Contain(PlatformFeatureNames.ApplyForJobs);
        granted.Should().Contain(PlatformFeatureNames.CreateOrganizations);
    }

    [Fact]
    public async Task A_rejected_verification_returns_the_member_to_limited_access()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        Api.Authenticate(auth.Tokens.AccessToken);
        await Api.SubmitVerificationAsync();

        var rejected = await ApplyTrustDecisionAsync(auth.User.Id, "Rejected", "Documents unreadable");
        var state = await IdentityApiClient.ReadDataAsync<AccessStateResponse>(rejected);

        state.State.Should().Be("LimitedAccess");
    }

    [Fact]
    public async Task The_trust_engine_can_revoke_verification()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();

        var revoked = await ApplyTrustDecisionAsync(auth.User.Id, "Revoked", "Residency lapsed");
        var state = await IdentityApiClient.ReadDataAsync<AccessStateResponse>(revoked);

        state.State.Should().Be("LimitedAccess");
    }

    [Fact]
    public async Task An_unrecognised_trust_outcome_is_refused()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        var response = await ApplyTrustDecisionAsync(auth.User.Id, "DefinitelyNotAnOutcome");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
