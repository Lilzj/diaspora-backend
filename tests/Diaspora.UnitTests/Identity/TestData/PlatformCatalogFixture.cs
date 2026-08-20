using Diaspora.Application.Identity.Access;
using Diaspora.Application.Identity.Authorization;
using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.UnitTests.Identity.TestData;

/// <summary>
/// Composes the platform catalogues the way the kernel does at startup, for tests that
/// exercise the resolvers and the Access Gateway.
/// <para>
/// The resolvers are now adapters over the composed catalogues, so a resolver test has to
/// compose one. It uses the real Identity contributors plus <see cref="VerifiedTierModule"/>,
/// a stand-in for the future feature modules (Marketplace, Messaging, Jobs) that supplies
/// the verified-tier permissions and gates Identity no longer owns. Testing against a
/// stand-in rather than the host's provisional contributor keeps the unit tests free of a
/// dependency on the Api project.
/// </para>
/// </summary>
public static class PlatformCatalogFixture
{
    // Verified-tier permissions a contributor module would own. Named here so the tests can
    // assert on them without reaching into Identity, which no longer declares them.
    public const string PublishContent = "content:publish";
    public const string SendMessages = "messaging:send";
    public const string ApplyForJobs = "jobs:apply";
    public const string CreateOrganizations = "organizations:create";

    public const string BrowseGuides = "guides.browse";
    public const string PublishGuides = "guides.publish";
    public const string CreateMarketplaceListings = "marketplace.listings.create";
    public const string SendMessagesFeature = "messaging.send";
    public const string ApplyForJobsFeature = "jobs.apply";
    public const string CreateOrganizationsFeature = "organizations.create";

    public static IPermissionCatalog PermissionCatalog { get; } =
        new PermissionCatalog(
        [
            new IdentityPermissionContributor(),
            new VerifiedTierModule()
        ]);

    public static IFeatureCatalog FeatureCatalog { get; } =
        new FeatureCatalog(
        [
            new IdentityFeatureContributor(),
            new VerifiedTierModule()
        ]);

    public static PermissionResolver PermissionResolver { get; } = new(PermissionCatalog);

    public static FeatureAccessResolver FeatureResolver { get; } = new(FeatureCatalog);

    public static AccessGateway AccessGateway { get; } = new(FeatureResolver, PermissionResolver);

    /// <summary>
    /// Stands in for the contributor modules. Declares the verified-tier permissions and
    /// gates that used to live in Identity, plus one browsable gate, so the resolver tests
    /// have something at each tier to assert on.
    /// </summary>
    private sealed class VerifiedTierModule : IPermissionContributor, IFeatureContributor
    {
        public string Module => "TestContributorModule";

        public IEnumerable<PermissionDefinition> GetPermissions()
        {
            yield return Verified(PublishContent, "Publish content.");
            yield return Verified(SendMessages, "Send messages.");
            yield return Verified(ApplyForJobs, "Apply for jobs.");
            yield return Verified(CreateOrganizations, "Create organizations.");
        }

        public IEnumerable<FeatureDefinition> GetFeatures()
        {
            yield return new FeatureDefinition(BrowseGuides, Module, "Browse guides.");
            yield return VerifiedFeature(PublishGuides, "Publish a guide.");
            yield return VerifiedFeature(CreateMarketplaceListings, "List an item.");
            yield return VerifiedFeature(SendMessagesFeature, "Send messages.");
            yield return VerifiedFeature(ApplyForJobsFeature, "Apply for a job.");
            yield return VerifiedFeature(CreateOrganizationsFeature, "Create an organization.");
        }

        private PermissionDefinition Verified(string key, string description) =>
            new(key, Module, description) { MinimumTier = AccessTier.Verified };

        private FeatureDefinition VerifiedFeature(string name, string description) =>
            new(name, Module, description) { MinimumTier = AccessTier.Verified };
    }
}
