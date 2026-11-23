using Diaspora.Modules.GeographicIntelligence.Domain.Routing;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.Platform.Operations.Resilience;
using Diaspora.Platform.Usage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Maps.Google;

/// <summary>
/// Registers the Google adapters over the routing and place search placeholders.
/// </summary>
/// <remarks>
/// Call after the modules. Last registration wins on <see cref="IRoutingProvider"/> and
/// <see cref="IPlaceSearchProvider"/>, as with the email and payment adapters.
/// </remarks>
public static class GoogleMapsRegistration
{
    public static IServiceCollection AddGoogleMaps(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(GoogleMapsOptions.SectionName);
        var options = section.Get<GoogleMapsOptions>() ?? new GoogleMapsOptions();

        // No key: leave both placeholders answering "not configured". An uncredentialed adapter
        // would report routing as available and then fail every request with 403.
        if (!options.IsConfigured)
        {
            return services;
        }

        services.AddOptions<GoogleMapsOptions>()
            .Bind(section)
            .ValidateDataAnnotations()
            .Validate(
                settings => TimeZoneInfo.TryFindSystemTimeZoneById(settings.BillingTimeZone, out _),
                "Maps:Google:BillingTimeZone is not a time zone this host knows.")
            .ValidateOnStart();

        // Both clients are governed, and they are governed separately.
        //
        // The audit recorded these as a backpressure exception because every billable call takes a
        // unit from a monthly allowance first. A quota limits spend, not concurrency: it does not
        // return the connection a slow call is holding, and both of these are read on a member's
        // request -- PlanRouteQuery and the place-search queries are query handlers, so a Routes
        // outage holds a request thread per member looking at a map.
        //
        // Routes and Places are separate dependencies because they are separate Google APIs with
        // separate quotas and separate outages; sharing a name would let one break the other.
        services.AddHttpClient(GoogleRoutingProvider.HttpClientName, (provider, client) =>
        {
            var current = provider.GetRequiredService<IOptionsMonitor<GoogleMapsOptions>>().CurrentValue;
            client.BaseAddress = new Uri(current.RoutesBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(current.TimeoutSeconds);
        })
            .WithPlatformResilience(GoogleRoutingProvider.HttpClientName);

        services.AddHttpClient(GooglePlaceSearchProvider.HttpClientName, (provider, client) =>
        {
            var current = provider.GetRequiredService<IOptionsMonitor<GoogleMapsOptions>>().CurrentValue;
            client.BaseAddress = new Uri(current.PlacesBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(current.TimeoutSeconds);
        })
            .WithPlatformResilience(GooglePlaceSearchProvider.HttpClientName);

        // Scoped, because the meter writes through the request's DbContext.
        services.AddScoped<IRoutingProvider>(provider => new GoogleRoutingProvider(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient(GoogleRoutingProvider.HttpClientName),
            provider.GetRequiredService<IProviderUsageMeter>(),
            provider.GetRequiredService<IOptionsMonitor<GoogleMapsOptions>>(),
            provider.GetRequiredService<ILogger<GoogleRoutingProvider>>()));

        services.AddScoped<IPlaceSearchProvider>(provider => new GooglePlaceSearchProvider(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient(GooglePlaceSearchProvider.HttpClientName),
            provider.GetRequiredService<IProviderUsageMeter>(),
            provider.GetRequiredService<IOptionsMonitor<GoogleMapsOptions>>(),
            provider.GetRequiredService<ILogger<GooglePlaceSearchProvider>>()));

        return services;
    }
}
