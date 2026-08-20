namespace Diaspora.IntegrationTests.Common;

/// <summary>
/// Feature-gate names the integration suite asserts on that are <em>not</em> owned by
/// Identity.
/// <para>
/// In B2 these were constants on Identity's <c>IdentityFeatures</c>. B2.6 moved them out —
/// Identity has no business owning a messaging or marketplace gate — so they now reach the
/// composed catalogue through the host's provisional contributor (and, in production, through
/// the modules that will replace it). The suite exercises the real host, so the gates are
/// genuinely present at runtime; the test just needs their names without borrowing Identity's
/// vocabulary. Naming them here keeps that intent explicit.
/// </para>
/// </summary>
internal static class PlatformFeatureNames
{
    public const string SendMessages = "messaging.send";
    public const string PublishGuides = "guides.publish";
    public const string CreateMarketplaceListings = "marketplace.listings.create";
    public const string ApplyForJobs = "jobs.apply";
    public const string CreateOrganizations = "organizations.create";
}
