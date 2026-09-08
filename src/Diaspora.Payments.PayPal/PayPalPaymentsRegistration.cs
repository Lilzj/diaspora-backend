using Diaspora.Modules.Financial.Extensibility.Payouts;
using Diaspora.Modules.Financial.Extensibility.Settlement;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Payments.PayPal;

/// <summary>
/// Registers the PayPal adapter behind the Financial Platform's settlement seam.
/// <para>
/// <b>A no-op unless both credentials are configured</b>, for the reason the Stripe registration
/// gives: <c>PayoutProviderRouter</c> treats every registered <c>ISettlementProvider</c> as usable,
/// so an uncredentialed adapter would advertise a rail that answers 401 instead of refusing with
/// <c>financial.payout.provider_not_registered</c>.
/// </para>
/// <para>
/// <b>No <c>ISettlementStatusProvider</c> is registered, and that is a real limitation rather than
/// an omission.</b> PayPal identifies a payout by the <c>payout_batch_id</c> it returns, and a
/// submission whose response was lost never returned one — so there is nothing to look it up by.
/// Its <c>sender_batch_id</c> is the platform's own handle and the payouts API offers no lookup on
/// it. An unresolved PayPal payout therefore stays held and visible until an operator reconciles it
/// against PayPal's console, which is the honest behaviour; inventing a resolution would be worse.
/// See <c>docs/financial/paypal-adapter.md</c>.
/// </para>
/// </summary>
public static class PayPalPaymentsRegistration
{
    public static IServiceCollection AddPayPalPayments(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(PayPalOptions.SectionName);
        var options = section.Get<PayPalOptions>() ?? new PayPalOptions();

        if (!options.IsConfigured)
        {
            return services;
        }

        services.AddOptions<PayPalOptions>()
            .Bind(section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient(PayPalAccessTokenCache.HttpClientName, (provider, client) =>
            client.Timeout = TransportTimeout(provider));

        services.AddHttpClient(PayPalSettlementProvider.HttpClientName, (provider, client) =>
            client.Timeout = TransportTimeout(provider));

        services.AddHttpClient(PayPalPayoutWebhookProcessor.HttpClientName, (provider, client) =>
            client.Timeout = TransportTimeout(provider));

        // Singleton because the token it holds is account-wide and refreshing it is single-flight.
        // A scoped cache would mint a token per request, which is the behaviour it exists to avoid.
        services.AddSingleton(provider => new PayPalAccessTokenCache(
            provider.GetRequiredService<IOptionsMonitor<PayPalOptions>>(),
            provider.GetRequiredService<IHttpClientFactory>()
                .CreateClient(PayPalAccessTokenCache.HttpClientName),
            provider.GetRequiredService<ILogger<PayPalAccessTokenCache>>()));

        services.AddSingleton<ISettlementProvider>(provider => new PayPalSettlementProvider(
            provider.GetRequiredService<IOptionsMonitor<PayPalOptions>>(),
            provider.GetRequiredService<PayPalAccessTokenCache>(),
            provider.GetRequiredService<IHttpClientFactory>()
                .CreateClient(PayPalSettlementProvider.HttpClientName),
            provider.GetRequiredService<IClock>(),
            provider.GetRequiredService<ILogger<PayPalSettlementProvider>>()));

        // **The only way a lost-response PayPal payout is ever resolved.**
        //
        // ISettlementStatusProvider cannot serve this adapter: the resolution sweep only sees
        // withdrawals whose settlement reference is null, and PayPal has no lookup by
        // sender_batch_id. Resubmitting the batch to read it back would create the payout if the
        // original never landed. So PayPal settles by being told, and this is the telling.
        services.AddSingleton<IPayoutWebhookProcessor>(provider => new PayPalPayoutWebhookProcessor(
            provider.GetRequiredService<IOptionsMonitor<PayPalOptions>>(),
            provider.GetRequiredService<PayPalAccessTokenCache>(),
            provider.GetRequiredService<IHttpClientFactory>()
                .CreateClient(PayPalPayoutWebhookProcessor.HttpClientName),
            provider.GetRequiredService<ILogger<PayPalPayoutWebhookProcessor>>()));

        return services;
    }

    private static TimeSpan TransportTimeout(IServiceProvider provider)
    {
        var current = provider.GetRequiredService<IOptionsMonitor<PayPalOptions>>().CurrentValue;

        return TimeSpan.FromSeconds(current.TimeoutSeconds > 0 ? current.TimeoutSeconds : 30);
    }
}
