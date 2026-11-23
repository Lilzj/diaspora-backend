using Diaspora.Modules.Financial.Extensibility.Payouts;
using Diaspora.Modules.Financial.Extensibility.Settlement;
using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Payments.Flutterwave;

/// <summary>
/// Registers the Flutterwave adapter behind the Financial Platform's payout seams.
/// <para>
/// <b>A no-op unless a secret key is configured</b>, for the reason the Stripe registration gives:
/// the payout router treats every registered <c>ISettlementProvider</c> as usable, so an
/// uncredentialed adapter would advertise a rail that answers 401.
/// </para>
/// <para>
/// <b>Registering the adapter enables no corridor.</b> Which countries, rails and currencies this
/// account may serve is <c>Financial:PayoutRouting</c>, and Flutterwave's coverage differs sharply
/// by country — a rail available in one is frequently absent in the next. Nothing in this repository
/// asserts what a given account can do; rows stay <c>Unknown</c> and refuse until somebody confirms
/// them against Flutterwave's own documentation.
/// </para>
/// </summary>
public static class FlutterwavePaymentsRegistration
{
    public static IServiceCollection AddFlutterwavePayments(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(FlutterwaveOptions.SectionName);
        var options = section.Get<FlutterwaveOptions>() ?? new FlutterwaveOptions();

        if (!options.IsConfigured)
        {
            return services;
        }

        services.AddOptions<FlutterwaveOptions>()
            .Bind(section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Every client below is routed through Flutterwave's own breaker, tuned identically to the
        // other two payment adapters' and keyed separately from them. The executor keys breaker
        // state by dependency name, so one shared name would mean another provider's outage
        // refusing a Flutterwave payment -- and IPaymentGatewayRegistry.Resolve picks a gateway by
        // currency and country, so the three are alternatives rather than a chain.
        //
        // The retry policy for this dependency is MaxAttempts = 0 and must stay that way; the
        // handler refuses to be constructed otherwise.
        services.AddHttpClient(FlutterwaveSettlementProvider.HttpClientName, (provider, client) =>
            client.Timeout = TransportTimeout(provider))
            .WithPlatformResilience(
                OperationalPolicyNames.CircuitBreaker.FlutterwavePayments);

        services.AddHttpClient(FlutterwaveSettlementStatusProvider.HttpClientName, (provider, client) =>
            client.Timeout = TransportTimeout(provider))
            .WithPlatformResilience(
                OperationalPolicyNames.CircuitBreaker.FlutterwavePayments);

        services.AddHttpClient(
            FlutterwavePayoutReconciliationProvider.HttpClientName,
            (provider, client) => client.Timeout = TransportTimeout(provider))
            .WithPlatformResilience(
                OperationalPolicyNames.CircuitBreaker.FlutterwavePayments);

        services.AddSingleton<ISettlementProvider>(provider => new FlutterwaveSettlementProvider(
            provider.GetRequiredService<IOptionsMonitor<FlutterwaveOptions>>(),
            provider.GetRequiredService<IHttpClientFactory>()
                .CreateClient(FlutterwaveSettlementProvider.HttpClientName),
            provider.GetRequiredService<ILogger<FlutterwaveSettlementProvider>>()));

        // The only thing that can find a transfer Flutterwave made that no withdrawal accounts for.
        // Without it PayoutReconciliationSweep runs and finds nothing.
        services.AddSingleton<IPayoutReconciliationProvider>(provider =>
            new FlutterwavePayoutReconciliationProvider(
                provider.GetRequiredService<IOptionsMonitor<FlutterwaveOptions>>(),
                provider.GetRequiredService<IHttpClientFactory>()
                    .CreateClient(FlutterwavePayoutReconciliationProvider.HttpClientName),
                provider.GetRequiredService<ILogger<FlutterwavePayoutReconciliationProvider>>()));

        services.AddSingleton<IPayoutWebhookProcessor>(provider =>
            new FlutterwavePayoutWebhookProcessor(
                provider.GetRequiredService<IOptionsMonitor<FlutterwaveOptions>>(),
                provider.GetRequiredService<ILogger<FlutterwavePayoutWebhookProcessor>>()));

        // Flutterwave can be asked by the platform's own reference, so an unknown outcome here
        // resolves without an operator. PayPal cannot, and has no equivalent registration.
        services.AddSingleton<ISettlementStatusProvider>(provider =>
            new FlutterwaveSettlementStatusProvider(
                provider.GetRequiredService<IOptionsMonitor<FlutterwaveOptions>>(),
                provider.GetRequiredService<IHttpClientFactory>()
                    .CreateClient(FlutterwaveSettlementStatusProvider.HttpClientName),
                provider.GetRequiredService<ILogger<FlutterwaveSettlementStatusProvider>>()));

        return services;
    }

    private static TimeSpan TransportTimeout(IServiceProvider provider)
    {
        var current = provider.GetRequiredService<IOptionsMonitor<FlutterwaveOptions>>().CurrentValue;

        return TimeSpan.FromSeconds(current.TimeoutSeconds > 0 ? current.TimeoutSeconds : 30);
    }
}
