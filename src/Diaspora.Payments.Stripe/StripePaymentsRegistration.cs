using Diaspora.Modules.Financial.Extensibility.Payouts;
using Diaspora.Modules.Financial.Extensibility.Settlement;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Payments.Stripe;

/// <summary>
/// Registers the Stripe adapter behind the Financial Platform's payout seams.
/// <para>
/// <b>It is a no-op unless a secret key is configured, and that is load-bearing rather than
/// tidy.</b> <c>PayoutProviderRouter</c> builds its set of usable providers from the registered
/// <c>ISettlementProvider</c> instances, so registering an uncredentialed adapter would tell the
/// router that Stripe is available and produce a 401 on somebody's withdrawal instead of the
/// refusal <c>financial.payout.provider_not_registered</c>, which names the real problem.
/// </para>
/// <para>
/// <b>Registering the adapter is not the same as enabling a route.</b> Nothing here adds a row to
/// the capability matrix: which countries, rails and currencies this Stripe account may actually
/// serve is a fact about that account and its jurisdiction, and this repository is not entitled to
/// assert it. Configure <c>Financial:PayoutRouting</c> with rows confirmed against Stripe's own
/// documentation, and leave anything unconfirmed as <c>Unknown</c>.
/// </para>
/// </summary>
public static class StripePaymentsRegistration
{
    public static IServiceCollection AddStripePayments(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(StripeOptions.SectionName);
        var options = section.Get<StripeOptions>() ?? new StripeOptions();

        if (!options.IsConfigured)
        {
            return services;
        }

        services.AddOptions<StripeOptions>()
            .Bind(section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // The deployment supplies the member-to-connected-account mapping. Without one every payee
        // reads as not onboarded, which refuses rather than pretends.
        services.TryAddSingleton<IStripeAccountResolver, UnmappedStripeAccountResolver>();

        services.AddHttpClient(StripeSettlementProvider.HttpClientName, (provider, client) =>
            client.Timeout = TransportTimeout(provider));

        services.AddHttpClient(StripeConnectVerificationSource.HttpClientName, (provider, client) =>
            client.Timeout = TransportTimeout(provider));

        services.AddHttpClient(StripeSettlementStatusProvider.HttpClientName, (provider, client) =>
            client.Timeout = TransportTimeout(provider));

        services.AddSingleton<ISettlementProvider>(provider => new StripeSettlementProvider(
            provider.GetRequiredService<IOptionsMonitor<StripeOptions>>(),
            provider.GetRequiredService<IHttpClientFactory>()
                .CreateClient(StripeSettlementProvider.HttpClientName),
            provider.GetRequiredService<ILogger<StripeSettlementProvider>>()));

        // Without this, an unknown outcome stays unknown and the member's money stays held until an
        // operator intervenes. Registering it is what makes the hold temporary.
        services.AddSingleton<ISettlementStatusProvider>(provider => new StripeSettlementStatusProvider(
            provider.GetRequiredService<IOptionsMonitor<StripeOptions>>(),
            provider.GetRequiredService<IHttpClientFactory>()
                .CreateClient(StripeSettlementStatusProvider.HttpClientName),
            provider.GetRequiredService<ILogger<StripeSettlementStatusProvider>>()));

        // Verified callbacks are the fast half of settlement; the resolution sweep is the slow half
        // that cannot be blocked from outside. Neither is allowed to be the only route.
        services.AddSingleton<IPayoutWebhookProcessor>(provider => new StripePayoutWebhookProcessor(
            provider.GetRequiredService<IOptionsMonitor<StripeOptions>>(),
            provider.GetRequiredService<IClock>(),
            provider.GetRequiredService<ILogger<StripePayoutWebhookProcessor>>()));

        services.AddSingleton<IPayoutProviderVerificationSource>(provider =>
            new StripeConnectVerificationSource(
                provider.GetRequiredService<IOptionsMonitor<StripeOptions>>(),
                provider.GetRequiredService<IStripeAccountResolver>(),
                provider.GetRequiredService<IHttpClientFactory>()
                    .CreateClient(StripeConnectVerificationSource.HttpClientName),
                provider.GetRequiredService<ILogger<StripeConnectVerificationSource>>()));

        return services;
    }

    private static TimeSpan TransportTimeout(IServiceProvider provider)
    {
        var current = provider.GetRequiredService<IOptionsMonitor<StripeOptions>>().CurrentValue;

        return TimeSpan.FromSeconds(current.TimeoutSeconds > 0 ? current.TimeoutSeconds : 20);
    }
}
