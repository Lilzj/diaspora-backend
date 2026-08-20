using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Analytics.Authorization;

/// <summary>
/// What a caller must hold, and at which tier.
/// <para>
/// <b>Reading your own analytics is <see cref="AccessTier.Limited"/>, not
/// <see cref="AccessTier.Verified"/>.</b> B2.0.1's rule is that a tier gates <em>privilege over
/// other members</em> rather than progression, and looking at figures about your own work is the
/// least privileged thing on this platform. A member who published a guide on their second day
/// should be able to see whether anybody read it.
/// </para>
/// <para>
/// <b>Submitting telemetry is <see cref="AccessTier.Onboarding"/>, because a member is instrumented
/// from their first screen.</b> The permission exists so a deployment can revoke ingestion from an
/// account without turning the endpoint off, and so the capability appears in the catalogue like
/// every other — not because there is a tier at which measurement becomes acceptable.
/// </para>
/// <para>
/// <b>The two privileged permissions are role-gated rather than tier-gated, and the difference is
/// the whole of §48 and §49.</b> A tier floor of Verified would grant platform analytics to every
/// verified member, because Identity resolves permissions from the tier — "Verified and above"
/// means "everybody who finished verification". Analytics across subjects the caller does not own is
/// an administrative capability and the role is the gate.
/// </para>
/// </summary>
public sealed class AnalyticsPermissionContributor : IPermissionContributor
{
    public string Module => AnalyticsModuleMetadata.Name;

    public IEnumerable<PermissionDefinition> GetPermissions()
    {
        yield return new PermissionDefinition(
            AnalyticsPermissions.CreatorRead,
            Module,
            "Read the caller's own creator analytics.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new PermissionDefinition(
            AnalyticsPermissions.ContentReadOwn,
            Module,
            "Read performance for content the caller owns.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new PermissionDefinition(
            AnalyticsPermissions.TelemetryWrite,
            Module,
            "Submit client telemetry about the caller's own use of the application.")
        {
            MinimumTier = AccessTier.Onboarding
        };

        yield return new PermissionDefinition(
            AnalyticsPermissions.PlatformRead,
            Module,
            "Read analytics for a subject the caller does not own.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = AdministratorRoles()
        };

        yield return new PermissionDefinition(
            AnalyticsPermissions.OperationsRun,
            Module,
            "Run a bounded backfill or rebuild of the analytics rollups.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = AdministratorRoles()
        };
    }

    /// <summary>
    /// Who may read somebody else's figures, or rewrite the rollups.
    /// <para>
    /// Administrators only, and deliberately not moderators. A moderator decides about content; an
    /// analytics read across the platform is a different kind of authority, and a rebuild changes
    /// what every dashboard says.
    /// </para>
    /// </summary>
    private static HashSet<string> AdministratorRoles() =>
        new(StringComparer.Ordinal) { "Admin", "SuperAdmin" };
}

/// <summary>
/// The module's feature gates.
/// <para>
/// Three, and the split is operational: a deployment turns ingestion on first, waits for data to
/// accumulate, and turns the dashboards on when they have something to say. A dashboard published
/// on day one renders a correct zero, which every member reads as a defect.
/// </para>
/// </summary>
public sealed class AnalyticsFeatureContributor : IFeatureContributor
{
    public string Module => AnalyticsModuleMetadata.Name;

    public IEnumerable<FeatureDefinition> GetFeatures()
    {
        yield return new FeatureDefinition(
            AnalyticsFeatures.Creator,
            Module,
            "Creator analytics: the overview, the trends and content performance.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            AnalyticsFeatures.Content,
            Module,
            "Per-item performance for content the caller owns.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            AnalyticsFeatures.Telemetry,
            Module,
            "Client telemetry ingestion.")
        {
            MinimumTier = AccessTier.Onboarding
        };
    }
}

public sealed class AnalyticsPolicyContributor : IPolicyContributor
{
    public string Module => AnalyticsModuleMetadata.Name;

    public IEnumerable<PolicyDefinition> GetPolicies()
    {
        yield return PolicyDefinition.ForPermission(
            AnalyticsPolicies.CreatorRead, Module, AnalyticsPermissions.CreatorRead);

        yield return PolicyDefinition.ForPermission(
            AnalyticsPolicies.ContentRead, Module, AnalyticsPermissions.ContentReadOwn);

        yield return PolicyDefinition.ForPermission(
            AnalyticsPolicies.TelemetryWrite, Module, AnalyticsPermissions.TelemetryWrite);

        yield return PolicyDefinition.ForPermission(
            AnalyticsPolicies.PlatformRead, Module, AnalyticsPermissions.PlatformRead);

        yield return PolicyDefinition.ForPermission(
            AnalyticsPolicies.OperationsRun, Module, AnalyticsPermissions.OperationsRun);
    }
}
