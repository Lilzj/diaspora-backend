using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Bookmarks.Authorization;

/// <summary>
/// Declares the permissions the Bookmarks module owns.
/// <para>
/// Both are granted from the Limited tier: saving things is exactly the kind of low-stakes
/// engagement a member should have before verifying. That decision lives here, in the
/// module, because the module is the only thing that knows what a bookmark is worth — which
/// is the entire argument against B2's arrangement, where Identity held permissions for
/// features it could not reason about.
/// </para>
/// </summary>
public sealed class BookmarksPermissionContributor : IPermissionContributor
{
    public string Module => BookmarksModuleMetadata.Name;

    public IEnumerable<PermissionDefinition> GetPermissions()
    {
        yield return new PermissionDefinition(
            BookmarkPermissions.Read, Module, "Read your own bookmarks.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new PermissionDefinition(
            BookmarkPermissions.Manage, Module, "Create and remove your own bookmarks.")
        {
            MinimumTier = AccessTier.Limited
        };
    }
}

/// <summary>Declares the Bookmarks feature gate.</summary>
public sealed class BookmarksFeatureContributor : IFeatureContributor
{
    public string Module => BookmarksModuleMetadata.Name;

    public IEnumerable<FeatureDefinition> GetFeatures()
    {
        yield return new FeatureDefinition(
            BookmarkFeatures.Use, Module, "Save listings, guides and jobs for later.")
        {
            MinimumTier = AccessTier.Limited
        };
    }
}

/// <summary>Declares the policies the module's endpoints reference.</summary>
public sealed class BookmarksPolicyContributor : IPolicyContributor
{
    public string Module => BookmarksModuleMetadata.Name;

    public IEnumerable<PolicyDefinition> GetPolicies()
    {
        yield return PolicyDefinition.ForPermission(
            BookmarkPolicies.Read, Module, BookmarkPermissions.Read);

        yield return PolicyDefinition.ForPermission(
            BookmarkPolicies.Manage, Module, BookmarkPermissions.Manage);
    }
}

/// <summary>Subscribes the host's tracer and metrics to the module's telemetry source.</summary>
public sealed class BookmarksObservabilityContributor : IObservabilityContributor
{
    public string Module => BookmarksModuleMetadata.Name;

    public IEnumerable<string> ActivitySources => [BookmarkTelemetry.SourceName];

    public IEnumerable<string> Meters => [BookmarkTelemetry.SourceName];
}
