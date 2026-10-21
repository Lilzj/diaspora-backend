using Diaspora.Modules.Location.Application.EventHandlers;
using Diaspora.Modules.Location.Application.Services;
using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.Modules.Location.Domain.Experience;
using Diaspora.Modules.Location.Domain.Profiles;
using Diaspora.Modules.Location.Domain.Residency;
using Diaspora.Modules.Location.Domain.Places;
using Diaspora.Modules.Location.Domain.Spatial;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.Modules.Location.Infrastructure.Places;
using Diaspora.Modules.Location.Integration;
using Diaspora.Modules.Location.Persistence.Repositories;
using Diaspora.Modules.Location.Persistence.Seeding;
using Diaspora.Modules.Location.Persistence.Spatial;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Seeding;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace Diaspora.Modules.Location;

/// <summary>
/// The Location &amp; Hyperlocal Intelligence Engine.
/// <para>
/// Registers entirely through the B2.6 contributor model. The host references this assembly so
/// it lands in the build output and names it nowhere else; permissions, feature gates,
/// policies, endpoints, telemetry and database configuration are all found by the kernel's
/// contributor scan. <c>Program.cs</c>, the Platform Kernel, the Identity module and the Trust
/// module are untouched.
/// </para>
/// <para>
/// <b>Order 300.</b> After Identity (1), Bookmarks (100) and Trust (200). The ordering matters
/// in one direction only: this module's endpoints authorise against tiers Identity's subject
/// accessor supplies, and its residency projection consumes events Trust publishes. It takes
/// over no kernel service and replaces nothing — unlike Trust, which claims
/// <c>ITrustService</c>, Location introduces its own contracts rather than fulfilling a
/// placeholder.
/// </para>
/// </summary>
public sealed class LocationModule : IPlatformModule
{
    public string Name => LocationModuleMetadata.Name;

    public string Version => LocationModuleMetadata.Version;

    public int Order => 300;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var assembly = typeof(LocationModule).Assembly;

        // Handlers and validators from this assembly. The Application layer's pipeline
        // behaviors are open generics registered once, so validation, telemetry, transactions
        // and authorisation apply to these handlers for free.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddOptions<LocationSpatialOptions>()
            .Bind(configuration.GetSection(LocationSpatialOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<LocationPlacesOptions>()
            .Bind(configuration.GetSection(LocationPlacesOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        RegisterRepositories(services);
        RegisterSpatial(services);
        RegisterExtensibility(services, assembly);
        RegisterServices(services);
        RegisterSeeding(services);
    }

    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddScoped<ICountryRepository, CountryRepository>();
        services.AddScoped<IAdministrativeAreaRepository, AdministrativeAreaRepository>();
        services.AddScoped<ILocationProfileRepository, LocationProfileRepository>();

        // Privacy. The module that owns a table is the only one that can erase it, and a saved
        // place is a home address — so this is registered beside the repository that writes one.
        services.AddScoped<Platform.Operations.Privacy.IDataErasureProvider,
            Infrastructure.Privacy.LocationDataErasureProvider>();
        services.AddScoped<Platform.Operations.Privacy.IDataExportProvider,
            Infrastructure.Privacy.LocationDataExportProvider>();
        services.AddScoped<ICountryExperienceRepository, CountryExperienceRepository>();
        services.AddScoped<IVerifiedResidencyRepository, VerifiedResidencyRepository>();
        services.AddScoped<
            Domain.Presence.IMemberPresenceRepository, MemberPresenceRepository>();
    }

    /// <summary>
    /// Registers the spatial stack: the earth model, the query provider and the geocoder.
    /// <para>
    /// The distance calculator is a singleton because it is stateless arithmetic. The
    /// providers are scoped because they reach the database through scoped repositories — and
    /// so is the registry that selects among them, which is why it validates its uniqueness
    /// rules per scope rather than once. That is a few dictionary inserts per request against
    /// a handful of providers, and it buys the ability to register a provider that needs a
    /// <c>DbContext</c> without the captive-dependency trap.
    /// </para>
    /// </summary>
    private static void RegisterSpatial(IServiceCollection services)
    {
        services.AddSingleton<IGeoDistanceCalculator, HaversineDistanceCalculator>();

        services.AddScoped<IGeospatialQueryProvider, RelationalGeospatialQueryProvider>();
        services.AddScoped<GeospatialProviderRegistry>();

        services.AddScoped<IGeocodingProvider, CatalogGeocodingProvider>();

        // Street addresses. "Not configured" until a vendor adapter registers after this module;
        // TryAdd so an adapter registered earlier also wins.
        services.TryAddScoped<IPlaceSearchProvider, NotConfiguredPlaceSearchProvider>();
    }

    /// <summary>
    /// Discovers the module's extension points across every loaded <c>Diaspora.*</c> assembly.
    /// <para>
    /// Scanning rather than listing is what makes "a vertical appears in
    /// <c>/api/v1/location/nearby</c> the moment its assembly ships" literally true. A Housing
    /// module registering an <see cref="INearbyResourceProvider"/> is found here without this
    /// file changing — which is the whole reason the seam exists.
    /// </para>
    /// <para>
    /// The registries are scoped rather than singleton because the providers they hold reach
    /// the database. Each validates its own uniqueness at construction, so two modules
    /// claiming <c>housing</c> fails the first request rather than silently resolving by
    /// assembly load order.
    /// </para>
    /// </summary>
    private static void RegisterExtensibility(IServiceCollection services, Assembly assembly)
    {
        var assemblies = ModuleAssemblyScanner.FindPlatformAssemblies().ToArray();

        RegisterImplementations<INearbyResourceProvider>(services, assemblies, ServiceLifetime.Scoped);
        RegisterImplementations<ILocationResourceCounter>(services, assemblies, ServiceLifetime.Scoped);

        // Counts by a row's own coordinates within a circle — the area around a point (Phase 2A of
        // retiring the place catalogue, 14 Oct 2026).
        RegisterImplementations<ICircleResourceCounter>(services, assemblies, ServiceLifetime.Scoped);
        RegisterImplementations<ILocationCatalogSeeder>(services, assemblies, ServiceLifetime.Scoped);
        RegisterImplementations<IAreaFilingSource>(services, assemblies, ServiceLifetime.Scoped);

        // Metadata contributors are the one seam whose contract forbids I/O — they answer from
        // a table in memory — so they and the catalogue composing them are singletons. The
        // lifetime is not a detail: a scoped contributor consumed by a singleton catalogue is a
        // captive dependency, and the host's service-provider validation refuses to start on it.
        RegisterImplementations<ICountryMetadataContributor>(
            services, assemblies, ServiceLifetime.Singleton);

        services.AddScoped<NearbyResourceProviderRegistry>();
        services.AddScoped<LocationResourceCounterRegistry>();
        services.AddSingleton<CountryMetadataCatalog>();
    }

    private static void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<LocationIntegrationEventRelay>();
        services.AddScoped<CountryNameResolver>();
        services.AddScoped<LocationProfileProvisioner>();
        services.AddScoped<LocationSnapshotFactory>();
        services.AddScoped<ResidencyProjector>();
        services.AddScoped<NearbyAggregator>();
        services.AddScoped<ResourceCountAggregator>();

        // The viewport aggregation, extracted so the map-cluster contract and B21's density layer
        // share one implementation of the cell cap and the suppression floor rather than two.
        services.AddScoped<MapCellAggregator>();

        // The contracts other modules consume. Registered against their interfaces so a
        // consumer depends on the seam rather than on the implementation.
        services.AddScoped<ILocationDirectory, LocationDirectory>();
        services.AddScoped<IResidencyDirectory, ResidencyDirectory>();
        services.AddScoped<IGeographicScopeResolver, GeographicScopeResolver>();

        // Filing a row under the area it is in. Kept registered for the catalogue's own remaining
        // uses until Phase 3 removes it.
        //
        // ── 14 Oct 2026: AreaFilingSweep is no longer hosted ─────────────────────────────────
        //
        // The owner retired the place catalogue for discovery: every vertical is filed and found
        // by its coordinates, its country and the town its owner typed. Nothing writes an area id
        // or path on create or update any more, and the sweep that back-filed them would only
        // keep writing data nothing reads. The class stays until Phase 3 deletes it.
        services.AddScoped<IAreaLocator, AreaLocator>();

        // 21 Oct 2026. The "when the app is open" presence report's area rules.
        services.AddScoped<Application.Presence.PresenceAreas>();
    }

    private static void RegisterSeeding(IServiceCollection services)
    {
        services.AddScoped<LocationCatalogWriter>();
        services.AddScoped<ILocationCatalogWriter>(
            provider => provider.GetRequiredService<LocationCatalogWriter>());

        // A hosted service the module registers itself. This is how the catalogue gets seeded
        // without an edit to Program.cs — the contributor model working as intended rather
        // than a hole in it.
        services.AddReferenceDataSeeder<LocationCatalogSeedRunner>();
    }

    /// <summary>
    /// Registers every implementation of a contract found across the platform's assemblies.
    /// <para>
    /// The lifetime is per-seam rather than uniform. A vertical's nearby provider or counter
    /// will almost certainly need its own repository, so those are scoped — a singleton
    /// registration would work until the first provider that touches a <c>DbContext</c>, and
    /// then fail in a way that looks like a concurrency bug. Metadata contributors are
    /// singletons because their contract forbids I/O and the catalogue over them is composed
    /// once.
    /// </para>
    /// </summary>
    private static void RegisterImplementations<TContract>(
        IServiceCollection services, Assembly[] assemblies, ServiceLifetime lifetime)
        where TContract : class
    {
        foreach (var type in ModuleAssemblyScanner.FindImplementations<TContract>(assemblies))
        {
            services.Add(new ServiceDescriptor(typeof(TContract), type, lifetime));
        }
    }
}
