using Diaspora.Modules.Financial.Extensibility.Gateways;
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
/// Registers the Stripe adapter behind the Financial Platform's payment and payout seams.
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

        // **The member-to-connected-account mapping, read off the member’s own destinations.**
        //
        // This used to be UnmappedStripeAccountResolver, which answers null for everybody. That is
        // the safe direction — it refuses rather than pretends — but nothing ever replaced it, so
        // StripeConnectVerificationSource reported NotStarted for every member on the platform and
        // every payout route requiring onboarding refused. The mapping was being stored the whole
        // time on PayoutDestination.ProviderToken; there was simply no reader.
        //
        // Still TryAdd, so a deployment that keeps its mapping elsewhere can register its own
        // before this runs.
        services.TryAddSingleton<IStripeAccountResolver, StripeDestinationAccountResolver>();

        services.AddHttpClient(StripeSettlementProvider.HttpClientName, (provider, client) =>
            client.Timeout = TransportTimeout(provider));

        services.AddHttpClient(StripeConnectVerificationSource.HttpClientName, (provider, client) =>
            client.Timeout = TransportTimeout(provider));

        services.AddHttpClient(StripePayoutOnboarding.HttpClientName, (provider, client) =>
            client.Timeout = TransportTimeout(provider));

        services.AddHttpClient(StripeSettlementStatusProvider.HttpClientName, (provider, client) =>
            client.Timeout = TransportTimeout(provider));

        services.AddHttpClient(StripePaymentGateway.HttpClientName, (provider, client) =>
            client.Timeout = TransportTimeout(provider));

        // ── The charge half ──────────────────────────────────────────────────────
        //
        // Without this the deployment has no IPaymentGateway at all: the only implementation in the
        // repository is SimulatedPaymentGateway, which FinancialModule excludes outside Development
        // and Testing. The capabilities endpoint reads the registry, so `gateways` and
        // `supportedMethods` came back empty and every deposit surface in the client correctly, and
        // uselessly, reported that the platform could not take payments.
        services.AddSingleton<IPaymentGateway>(provider => new StripePaymentGateway(
            provider.GetRequiredService<IOptionsMonitor<StripeOptions>>(),
            provider.GetRequiredService<IHttpClientFactory>()
                .CreateClient(StripePaymentGateway.HttpClientName),
            provider.GetRequiredService<ILogger<StripePaymentGateway>>()));

        // Authorise and capture are separated by the member confirming the intent in the app, and
        // this callback is what tells the platform that happened. Without it an authorisation never
        // becomes capturable on the platform's side, however well it went at Stripe.
        services.AddSingleton<IPaymentWebhookProcessor>(provider => new StripePaymentWebhookProcessor(
            provider.GetRequiredService<IOptionsMonitor<StripeOptions>>(),
            provider.GetRequiredService<IClock>(),
            provider.GetRequiredService<ILogger<StripePaymentWebhookProcessor>>()));

        // ── The payout half ──────────────────────────────────────────────────────

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

        // Onboarding: the half that stood between registering a destination and verifying one.
        // Without it a member had no way to become payable, and the withdraw screen was a dead end.
        services.AddSingleton<IPayoutOnboarding>(provider => new StripePayoutOnboarding(
            provider.GetRequiredService<IOptionsMonitor<StripeOptions>>(),
            provider.GetRequiredService<IHttpClientFactory>()
                .CreateClient(StripePayoutOnboarding.HttpClientName),
            provider.GetRequiredService<ILogger<StripePayoutOnboarding>>()));

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
