using Diaspora.Application.Identity.Authorization;
using Diaspora.Domain.Identity.AccessStates;
using Diaspora.Domain.Identity.Users;
using Diaspora.UnitTests.Identity.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Identity.Access;

/// <summary>
/// The feature resolver is now an adapter over the composed feature catalogue. These tests
/// build one from the Identity contributor plus a stand-in contributor module (see
/// <see cref="PlatformCatalogFixture"/>) that supplies the verified-tier gates Identity no
/// longer owns.
/// </summary>
public class FeatureAccessResolverTests
{
    private static readonly FeatureAccessResolver Resolver = PlatformCatalogFixture.FeatureResolver;

    private static readonly int TotalFeatureCount = PlatformCatalogFixture.FeatureCatalog.All.Count;

    [Theory]
    [InlineData(PlatformCatalogFixture.SendMessagesFeature)]
    [InlineData(PlatformCatalogFixture.PublishGuides)]
    [InlineData(PlatformCatalogFixture.CreateMarketplaceListings)]
    [InlineData(PlatformCatalogFixture.ApplyForJobsFeature)]
    [InlineData(PlatformCatalogFixture.CreateOrganizationsFeature)]
    public void The_contributor_gates_stay_shut_at_limited_access(string feature)
    {
        Resolver.IsEnabled(feature, AccessState.LimitedAccess, UserRole.CommunityUser)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(PlatformCatalogFixture.SendMessagesFeature)]
    [InlineData(PlatformCatalogFixture.PublishGuides)]
    [InlineData(PlatformCatalogFixture.CreateMarketplaceListings)]
    [InlineData(PlatformCatalogFixture.ApplyForJobsFeature)]
    [InlineData(PlatformCatalogFixture.CreateOrganizationsFeature)]
    public void Verification_opens_the_contributor_gates(string feature)
    {
        Resolver.IsEnabled(feature, AccessState.Verified, UserRole.CommunityUser)
            .Should().BeTrue();
    }

    [Fact]
    public void Browsing_is_available_from_limited_access_onward()
    {
        Resolver.IsEnabled(PlatformCatalogFixture.BrowseGuides, AccessState.LimitedAccess, UserRole.CommunityUser)
            .Should().BeTrue();
    }

    [Fact]
    public void Nothing_is_granted_before_phone_verification()
    {
        var snapshot = Resolver.Resolve(AccessState.PhoneVerificationPending, UserRole.CommunityUser);

        snapshot.Granted.Should().BeEmpty();
        snapshot.Blocked.Should().HaveCount(TotalFeatureCount);
    }

    [Fact]
    public void A_suspended_member_holds_nothing()
    {
        var snapshot = Resolver.Resolve(AccessState.Suspended, UserRole.Admin);

        snapshot.Granted.Should().BeEmpty();
    }

    [Fact]
    public void Staff_features_follow_the_role_not_the_access_state()
    {
        Resolver.IsEnabled(IdentityFeatures.ModerateContent, AccessState.LimitedAccess, UserRole.Moderator)
            .Should().BeTrue();

        Resolver.IsEnabled(IdentityFeatures.ModerateContent, AccessState.Verified, UserRole.CommunityUser)
            .Should().BeFalse();
    }

    [Fact]
    public void Submitting_verification_closes_once_a_request_is_in_flight()
    {
        Resolver.IsEnabled(IdentityFeatures.SubmitVerification, AccessState.LimitedAccess, UserRole.CommunityUser)
            .Should().BeTrue();

        Resolver.IsEnabled(IdentityFeatures.SubmitVerification, AccessState.VerificationPending, UserRole.CommunityUser)
            .Should().BeFalse();
    }

    [Fact]
    public void An_unknown_feature_is_never_enabled()
    {
        Resolver.IsEnabled("not.a.real.feature", AccessState.Verified, UserRole.SuperAdmin)
            .Should().BeFalse();
    }

    [Fact]
    public void Every_feature_is_accounted_for_in_every_state()
    {
        var allFeatures = PlatformCatalogFixture.FeatureCatalog.All.Select(feature => feature.Name).ToArray();

        foreach (var state in AccessState.All)
        {
            var snapshot = Resolver.Resolve(state, UserRole.CommunityUser);
            var covered = snapshot.Granted.Concat(snapshot.Blocked.Select(b => b.Feature)).Distinct();

            covered.Should().BeEquivalentTo(allFeatures,
                $"{state.Name} must give an answer for every gate");
        }
    }
}
