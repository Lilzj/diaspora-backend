using Diaspora.Platform.Operations.Security;

namespace Diaspora.Modules.Integrations.Infrastructure.Security;

/// <summary>
/// The secrets an integration deployment needs, declared so the platform can validate and report on
/// them without being asked.
/// <para>
/// <b>Every one is optional, and that is the design.</b> A deployment with no Google adapter must
/// start; a deployment with a Google adapter and no client secret must start too, and must say so
/// through the capability report rather than crashing at boot. §8: "an integration that is not
/// configured must not cause application startup to fail unless the integration is explicitly
/// mandatory", and none of these is.
/// </para>
/// <para>
/// <b>Declaring them anyway is what makes the absence visible.</b> B17's secret health check reads
/// this list, so an operator sees "the Google client secret has never been set" on a dashboard
/// rather than discovering it when the first member tries to connect a calendar. And the rotation
/// schedule is expressible because the requirement names one.
/// </para>
/// <para>
/// <b>No value appears anywhere in this assembly.</b> These are names. The store resolves them, the
/// store owns their rotation, and rotating one is a store operation rather than a redeploy.
/// </para>
/// </summary>
public sealed class IntegrationSecretRequirementContributor : ISecretRequirementContributor
{
    /// <summary>
    /// The naming convention: <c>integrations.{provider-key}.{purpose}</c>.
    /// <para>
    /// Provider-keyed rather than vendor-keyed, so <c>calendar.google</c> and <c>identity.google</c>
    /// hold different credentials with different scopes — which is the same separation the provider
    /// key itself exists to enforce, carried into the secret store.
    /// </para>
    /// </summary>
    public const string Prefix = "integrations.";

    /// <summary>The client secret for a provider's OAuth application.</summary>
    public static string ClientSecretName(string providerKey) => $"{Prefix}{providerKey}.client-secret";

    /// <summary>The shared secret a provider signs its callbacks with.</summary>
    public static string WebhookSecretName(string providerKey) => $"{Prefix}{providerKey}.webhook-secret";

    public string Module => IntegrationsModuleMetadata.Name;

    public IEnumerable<SecretRequirement> Secrets
    {
        get
        {
            // The four provider keys the frontend's own union names for the two domains this module
            // serves. Declared rather than discovered, because a requirement list assembled from
            // registered adapters would report nothing on the deployment that has none — which is
            // exactly the deployment an operator needs the list for.
            foreach (var provider in new[]
                     {
                         "calendar.google", "calendar.apple", "identity.google", "identity.apple"
                     })
            {
                yield return new SecretRequirement(
                    ClientSecretName(provider),
                    Module,
                    $"The OAuth client secret for {provider}. Absent means this deployment cannot "
                    + "complete an authorization for that provider, which the capability report "
                    + "states explicitly.",
                    Required: false,

                    // A client secret shorter than this is not one any of these providers issues,
                    // so a short value is a truncated paste rather than a policy choice.
                    MinimumLength: 16,

                    // Annually. Providers do not force it; a credential nobody has rotated since it
                    // was issued is the one that turns up in an old deployment manifest.
                    RotateEvery: TimeSpan.FromDays(365));

                yield return new SecretRequirement(
                    WebhookSecretName(provider),
                    Module,
                    $"The shared secret {provider} signs its callbacks with. Absent means callbacks "
                    + "from that provider are refused — deliberately, because a verifier that cannot "
                    + "verify must not accept.",
                    Required: false,
                    MinimumLength: 32,
                    RotateEvery: TimeSpan.FromDays(180));
            }
        }
    }
}
