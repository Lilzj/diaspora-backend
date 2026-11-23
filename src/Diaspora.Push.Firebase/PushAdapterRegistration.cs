using Diaspora.Modules.Notifications.Extensibility.Channels;
using Diaspora.Modules.Notifications.Options;
using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Diaspora.Push.Firebase;

/// <summary>
/// Registers the Cloud Messaging adapter behind <see cref="IPushNotificationSender"/>.
///
/// <para>
/// <b>Call this after the platform kernel.</b> The Notifications module registers a placeholder
/// sender during its own composition, and the seam is last-registration-wins — the same
/// substitution every other port in that module uses. Registering earlier would leave the
/// placeholder in front and every push would be recorded unsupported while the configuration
/// claimed a provider.
/// </para>
///
/// <para>
/// <b>It is a no-op unless the provider says Firebase.</b> A deployment that has not configured
/// push, or has configured a different one, is left exactly as it was — so this call can sit
/// unconditionally in <c>Program.cs</c> and the configuration remains the only thing that decides.
/// </para>
/// </summary>
public static class PushAdapterRegistration
{
    /// <summary>The <c>Notifications:Push:Provider</c> value this adapter answers to.</summary>
    public const string ProviderName = "Firebase";

    public static IServiceCollection AddFirebasePushAdapter(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration.GetSection(PushProviderOptions.SectionName).Get<PushProviderOptions>()
            ?? new PushProviderOptions();

        if (!string.Equals(options.Provider, ProviderName, StringComparison.OrdinalIgnoreCase))
        {
            return services;
        }

        // Resolved once at startup rather than per send. A missing or malformed credential should
        // stop the host with a message naming the file, not fail the first notification of the day
        // with a stack trace in a delivery row.
        services.AddSingleton(provider =>
        {
            var current = provider.GetRequiredService<IOptions<PushProviderOptions>>().Value;
            var json = ReadCredential(current);
            var key = ServiceAccountKey.Parse(json);

            // The project id in the key is authoritative. If configuration names a different one,
            // that is a mismatched credential rather than a preference, and sending to the wrong
            // project fails per-device with an error that looks like a dead token.
            if (!string.IsNullOrWhiteSpace(current.ProjectId)
                && !string.Equals(current.ProjectId, key.ProjectId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"'{PushProviderOptions.SectionName}:ProjectId' is '{current.ProjectId}' but the "
                    + $"credential belongs to project '{key.ProjectId}'. One of them is from a different "
                    + "Firebase project.");
            }

            return new ServiceAccountCredential(
                key, provider.GetRequiredService<IHttpClientFactory>().CreateClient(TokenClientName));
        });

        // Both clients are governed, under one dependency name.
        //
        // These two were the platform's last outbound calls with nothing but a transport timeout,
        // and they were invisible as well as ungoverned: the rule that was supposed to notice read
        // the registration's first argument and matched on the name of the constant, so it saw
        // every client called HttpClientName and neither of these. A scan that recognises its
        // population by naming convention finds whatever already follows the convention.
        //
        // One name, not two: a send cannot succeed without a token, so breaking them apart would
        // leave the send breaker counting failures of a call that had already been refused
        // upstream. The token exchange is also low-volume -- the credential caches the token -- so
        // a breaker of its own would rarely reach the throughput it needs to trip.
        services.AddHttpClient(TokenClientName)
            .WithPlatformResilience(OperationalPolicyNames.CircuitBreaker.FirebasePush);

        services.AddHttpClient(SendClientName, (provider, client) =>
        {
            var current = provider.GetRequiredService<IOptions<PushProviderOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(current.TimeoutSeconds > 0 ? current.TimeoutSeconds : 10);
        })
            .WithPlatformResilience(OperationalPolicyNames.CircuitBreaker.FirebasePush);

        // Scoped, matching the placeholder it replaces, so the channel's lifetime expectations are
        // unchanged. The credential above is the singleton, which is what holds the cached token.
        services.AddScoped<IPushNotificationSender>(provider => new CloudMessagingPushSender(
            provider.GetRequiredService<IOptionsMonitor<PushProviderOptions>>(),
            provider.GetRequiredService<ServiceAccountCredential>(),
            provider.GetRequiredService<IHttpClientFactory>().CreateClient(SendClientName),
            provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CloudMessagingPushSender>>()));

        return services;
    }

    private const string TokenClientName = "push-token-exchange";
    private const string SendClientName = "push-send";

    /// <summary>
    /// The credential, from a mounted file or from a secret-store variable.
    /// <para>
    /// Both being set is refused rather than resolved by precedence: which one wins would then
    /// depend on read order, and that is how a deployment ends up authenticating as last year's
    /// service account.
    /// </para>
    /// </summary>
    private static string ReadCredential(PushProviderOptions options)
    {
        var hasPath = !string.IsNullOrWhiteSpace(options.CredentialsPath);
        var hasInline = !string.IsNullOrWhiteSpace(options.CredentialsJson);

        if (hasPath && hasInline)
        {
            throw new InvalidOperationException(
                $"Both '{PushProviderOptions.SectionName}:CredentialsPath' and ':CredentialsJson' are set. "
                + "Supply exactly one.");
        }

        if (hasInline)
        {
            return options.CredentialsJson!;
        }

        if (!hasPath)
        {
            throw new InvalidOperationException(
                $"Push provider '{ProviderName}' is configured but neither "
                + $"'{PushProviderOptions.SectionName}:CredentialsPath' nor ':CredentialsJson' is set.");
        }

        if (!File.Exists(options.CredentialsPath))
        {
            throw new InvalidOperationException(
                $"The push credential file was not found at '{options.CredentialsPath}'. "
                + "The orchestrator mounts this path; check the secret is attached to this workload.");
        }

        return File.ReadAllText(options.CredentialsPath);
    }
}
