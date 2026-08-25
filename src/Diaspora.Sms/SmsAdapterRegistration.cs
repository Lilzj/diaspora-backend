using Diaspora.Application.Identity.Abstractions;
using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.Resilience;
using Diaspora.Sms.Gateways;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Sms;

/// <summary>
/// Registers the SMS adapter behind <see cref="IOtpDeliveryService"/>.
///
/// <para>
/// <b>Call this after the Identity infrastructure.</b> That registration puts a logging
/// placeholder behind the port, and the seam is last-registration-wins — the same substitution
/// the push adapter uses. Registering earlier would leave the placeholder in front, and the
/// placeholder's failure mode is silence: it reports success and sends nothing.
/// </para>
///
/// <para>
/// <b>It is a no-op unless a provider is configured.</b> A deployment with no <c>Sms</c> section
/// keeps the placeholder, which is the right answer for a development host where codes are read
/// from the log. So this call can sit unconditionally in <c>Program.cs</c>.
/// </para>
/// </summary>
public static class SmsAdapterRegistration
{
    public static IServiceCollection AddSmsAdapter(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(SmsOptions.SectionName);
        var options = section.Get<SmsOptions>() ?? new SmsOptions();

        // Nothing named and nothing credentialed: leave the placeholder alone.
        if (string.IsNullOrWhiteSpace(options.DefaultProvider)
            && options.Routes.Count == 0
            && !options.Twilio.IsConfigured
            && !options.AfricasTalking.IsConfigured)
        {
            return services;
        }

        services.AddOptions<SmsOptions>()
            .Bind(section)
            .ValidateDataAnnotations();

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<SmsOptions>, SmsOptionsValidator>());

        // Declared here rather than in the platform's own contributor: the numbers are about what
        // an SMS costs and how long a member will wait, and neither is the platform's business.
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IOperationalPolicyContributor, SmsResiliencePolicyContributor>());

        // One classifier per provider dependency. Without these the executor's default applies,
        // which treats an unrecognised exception as permanent — so a missing registration would
        // silently switch retry off rather than fail visibly.
        //
        // ── Add, not TryAddEnumerable ────────────────────────────────────────────────────
        //
        // `TryAddEnumerable` de-duplicates on the descriptor's *implementation type*, and a
        // factory descriptor has none — the implementation type reads as `IFailureClassifier`
        // itself, which it refuses as "indistinguishable from other services registered for" the
        // same interface. It threw during registration, so the host aborted before binding a port
        // and the machine restarted ten times and stopped.
        //
        // Two instances of one type is also exactly what is wanted here, and is the case
        // `TryAddEnumerable` exists to prevent. The two differ by constructor argument, not by
        // type, so de-duplicating by type would have collapsed them into one and left whichever
        // provider lost running under the platform's default classifier — retry silently off.
        services.AddSingleton<IFailureClassifier>(
            _ => new SmsFailureClassifier(SmsDependencies.Twilio));

        services.AddSingleton<IFailureClassifier>(
            _ => new SmsFailureClassifier(SmsDependencies.AfricasTalking));

        // The per-attempt timeout is the policy's; this one is the transport's last resort, set
        // above it so a client-level abort cannot pre-empt the executor's own accounting.
        services.AddHttpClient(TwilioSmsGateway.HttpClientName, (provider, client) =>
            client.Timeout = TransportTimeout(provider));

        services.AddHttpClient(AfricasTalkingSmsGateway.HttpClientName, (provider, client) =>
            client.Timeout = TransportTimeout(provider));

        services.AddSingleton<ISmsGateway>(provider => new TwilioSmsGateway(
            provider.GetRequiredService<IOptionsMonitor<SmsOptions>>(),
            provider.GetRequiredService<IHttpClientFactory>().CreateClient(TwilioSmsGateway.HttpClientName),
            provider.GetRequiredService<ILogger<TwilioSmsGateway>>()));

        services.AddSingleton<ISmsGateway>(provider => new AfricasTalkingSmsGateway(
            provider.GetRequiredService<IOptionsMonitor<SmsOptions>>(),
            provider.GetRequiredService<IHttpClientFactory>().CreateClient(AfricasTalkingSmsGateway.HttpClientName),
            provider.GetRequiredService<ILogger<AfricasTalkingSmsGateway>>()));

        services.AddSingleton<SmsGatewayRouter>();

        // Scoped, matching the placeholder it replaces: it resolves `ILocalizedMessageProvider`,
        // which reads the current request's language.
        services.AddScoped<IOtpDeliveryService, RoutedSmsOtpDeliveryService>();

        return services;
    }

    private static TimeSpan TransportTimeout(IServiceProvider provider)
    {
        var current = provider.GetRequiredService<IOptionsMonitor<SmsOptions>>().CurrentValue;

        return TimeSpan.FromSeconds(current.TimeoutSeconds > 0 ? current.TimeoutSeconds : 10);
    }
}
