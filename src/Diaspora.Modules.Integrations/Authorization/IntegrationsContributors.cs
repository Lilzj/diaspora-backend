using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Integrations.Authorization;

/// <summary>
/// What a caller must hold, and at which tier.
/// <para>
/// <b>Everything here is <see cref="AccessTier.Verified"/> except reading, and the split is
/// B2.0.1's rule applied rather than a guess.</b> A tier gates privilege over other members, not
/// progression. Connecting an external account is not privilege over anybody — it is a member
/// attaching their own Google account to their own record — but it is the act that puts a
/// third-party credential into this platform's custody, and a half-registered account is not
/// somebody this deployment should be holding a refresh token for.
/// </para>
/// <para>
/// Reading is <see cref="AccessTier.Limited"/> because the connection centre must be able to say
/// "you have connected nothing, and here is what this deployment supports" to a member who has not
/// finished verifying — otherwise the screen that explains the feature is behind the feature.
/// </para>
/// </summary>
public sealed class IntegrationsPermissionContributor : IPermissionContributor
{
    public string Module => IntegrationsModuleMetadata.Name;

    public IEnumerable<PermissionDefinition> GetPermissions()
    {
        yield return new PermissionDefinition(
            IntegrationsPermissions.ConnectionsRead,
            Module,
            "Read one's own integration connections, identity links, calendar preferences and the "
            + "deployment's integration capability report.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new PermissionDefinition(
            IntegrationsPermissions.ConnectionsWrite,
            Module,
            "Connect, re-authorize, revoke or disconnect one's own external account.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            IntegrationsPermissions.CalendarRead,
            Module,
            "List the calendars a connected account exposes.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            IntegrationsPermissions.CalendarWrite,
            Module,
            "Write an entry into a connected calendar.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            IntegrationsPermissions.CalendarSync,
            Module,
            "Start a calendar synchronisation and read its state.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            IntegrationsPermissions.SettingsWrite,
            Module,
            "Change one's own integration preferences.")
        {
            MinimumTier = AccessTier.Limited
        };
    }
}

/// <summary>
/// The module's feature gates.
/// <para>
/// Two, and the split is operational. A deployment turns connections on when it has an OAuth adapter
/// and an allow-listed redirect; it turns the calendar on when it has a calendar adapter. One gate
/// for both would mean a deployment with identity linking and no calendar either hides the
/// connection centre it can serve or advertises a calendar it cannot write to.
/// </para>
/// </summary>
public sealed class IntegrationsFeatureContributor : IFeatureContributor
{
    public string Module => IntegrationsModuleMetadata.Name;

    public IEnumerable<FeatureDefinition> GetFeatures()
    {
        yield return new FeatureDefinition(
            IntegrationsFeatures.Connections,
            Module,
            "The integration connection centre, the OAuth exchange and identity linking.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            IntegrationsFeatures.Calendar,
            Module,
            "Proxied calendar listing, writing and synchronisation.")
        {
            MinimumTier = AccessTier.Verified
        };
    }
}

public sealed class IntegrationsPolicyContributor : IPolicyContributor
{
    public string Module => IntegrationsModuleMetadata.Name;

    public IEnumerable<PolicyDefinition> GetPolicies()
    {
        yield return PolicyDefinition.ForPermission(
            IntegrationsPolicies.ConnectionsRead, Module, IntegrationsPermissions.ConnectionsRead);

        yield return PolicyDefinition.ForPermission(
            IntegrationsPolicies.ConnectionsWrite, Module, IntegrationsPermissions.ConnectionsWrite);

        yield return PolicyDefinition.ForPermission(
            IntegrationsPolicies.CalendarRead, Module, IntegrationsPermissions.CalendarRead);

        yield return PolicyDefinition.ForPermission(
            IntegrationsPolicies.CalendarWrite, Module, IntegrationsPermissions.CalendarWrite);

        yield return PolicyDefinition.ForPermission(
            IntegrationsPolicies.CalendarSync, Module, IntegrationsPermissions.CalendarSync);

        yield return PolicyDefinition.ForPermission(
            IntegrationsPolicies.SettingsWrite, Module, IntegrationsPermissions.SettingsWrite);
    }
}
