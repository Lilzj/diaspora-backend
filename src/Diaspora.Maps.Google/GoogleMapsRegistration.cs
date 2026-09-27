using Diaspora.Modules.GeographicIntelligence.Domain.Routing;
using Diaspora.Modules.Location.Domain.Places;
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

        services.AddHttpClient(GoogleRoutingProvider.HttpClientName, (provider, client) =>
        {
            var current = provider.GetRequiredService<IOptionsMonitor<GoogleMapsOptions>>().CurrentValue;
            client.BaseAddress = new Uri(current.RoutesBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(current.TimeoutSeconds);
        });

        services.AddHttpClient(GooglePlaceSearchProvider.HttpClientName, (provider, client) =>
        {
            var current = provider.GetRequiredService<IOptionsMonitor<GoogleMapsOptions>>().CurrentValue;
            client.BaseAddress = new Uri(current.PlacesBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(current.TimeoutSeconds);
        });

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
