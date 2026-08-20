using System.Reflection;
using Diaspora.Contracts.Authorization;
using Diaspora.Modules.Location;
using Diaspora.Modules.Location.Api;
using Diaspora.Modules.Location.Authorization;
using Diaspora.Modules.Location.Domain.Spatial;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.Modules.Location.Observability;
using Diaspora.Modules.Location.Persistence;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Contributors;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The boundaries the Location module must not cross, enforced rather than documented.
/// <para>
/// Two of these matter more than the rest. The first is that Location does not reference
/// Trust: the residency projection is fed by a published event contract, and if that ever
/// becomes a direct call the two modules stop being separable. The second is that no vertical
/// appears in this assembly — the whole design rests on Location being consumable by
/// Marketplace, Housing and the rest without knowing any of them exist.
/// </para>
/// </summary>
public sealed class LocationArchitectureTests
{
    private static Assembly LocationAssembly => typeof(LocationModule).Assembly;

    // ── Module boundaries ────────────────────────────────────────────────

    /// <summary>
    /// The most important rule in the module. Trust's contribution reaches Location through
    /// <c>Diaspora.Contracts.Trust.V1.Events</c> — a published contract — and through nothing
    /// else. A direct reference would mean the projection had become a call, and the two
    /// modules could no longer be deployed apart.
    /// </summary>
    [Fact]
    public void Location_must_not_depend_on_the_trust_module()
    {
        var result = Types.InAssembly(LocationAssembly)
            .ShouldNot()
            .HaveDependencyOn("Diaspora.Modules.Trust")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Trust reaches this module through published integration events, never directly: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Location_must_not_depend_on_the_identity_domain()
    {
        var result = Types.InAssembly(LocationAssembly)
            .ShouldNot()
            .HaveDependencyOn("Diaspora.Domain.Identity")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Location reaches the caller through the kernel's ICurrentUser, never through " +
            "Identity's domain: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Location_must_not_depend_on_identity_application_services()
    {
        var result = Types.InAssembly(LocationAssembly)
            .ShouldNot()
            .HaveDependencyOn("Diaspora.Application.Identity")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Location_must_not_depend_on_the_api_host()
    {
        var result = Types.InAssembly(LocationAssembly)
            .ShouldNot()
            .HaveDependencyOn("Diaspora.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "a module the host must reference back is not a module: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Location_must_not_depend_on_infrastructure()
    {
        var result = Types.InAssembly(LocationAssembly)
            .ShouldNot()
            .HaveDependencyOn("Diaspora.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// The design rests on Location knowing no vertical. If Housing ever appears here, the
    /// seams have been bypassed and the hub has become a bottleneck.
    /// </summary>
    [Theory]
    [InlineData("Diaspora.Modules.Marketplace")]
    [InlineData("Diaspora.Modules.Housing")]
    [InlineData("Diaspora.Modules.Jobs")]
    [InlineData("Diaspora.Modules.Community")]
    [InlineData("Diaspora.Modules.Providers")]
    [InlineData("Diaspora.Modules.Guides")]
    public void Location_must_not_depend_on_any_vertical(string vertical)
    {
        var result = Types.InAssembly(LocationAssembly)
            .ShouldNot()
            .HaveDependencyOn(vertical)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"verticals contribute to Location through INearbyResourceProvider and " +
            $"ILocationResourceCounter, never the other way round: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    // ── The spatial seam ─────────────────────────────────────────────────

    /// <summary>
    /// The brief requires PostGIS, Elasticsearch, Mapbox and the rest to plug in without
    /// changing the domain. That is only true if the domain names none of them.
    /// </summary>
    [Theory]
    [InlineData("NetTopologySuite")]
    [InlineData("Npgsql")]
    [InlineData("Nest")]
    [InlineData("Elasticsearch")]
    public void The_domain_must_not_depend_on_a_gis_or_search_library(string library)
    {
        var result = Types.InAssembly(LocationAssembly)
            .That().ResideInNamespaceStartingWith("Diaspora.Modules.Location.Domain")
            .ShouldNot()
            .HaveDependencyOn(library)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the spatial provider seam exists so the domain never learns which engine " +
            "answered: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// The domain must not reach EF Core either. Persistence configures the domain; the domain
    /// does not know it is persisted.
    /// </summary>
    [Fact]
    public void The_domain_must_not_depend_on_entity_framework()
    {
        var result = Types.InAssembly(LocationAssembly)
            .That().ResideInNamespaceStartingWith("Diaspora.Modules.Location.Domain")
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// The default provider must not claim a capability it cannot deliver. Claiming
    /// nearest-neighbour without a spatial index would make an approximation look exact.
    /// </summary>
    [Fact]
    public void The_default_spatial_provider_declares_only_what_it_can_do()
    {
        var capabilities = new Modules.Location.Persistence.Spatial
            .RelationalGeospatialQueryProvider(null!, null!).Capabilities;

        capabilities.Should().HaveFlag(GeospatialCapability.RadiusSearch);
        capabilities.Should().HaveFlag(GeospatialCapability.BoundingBoxSearch);
        capabilities.Should().HaveFlag(GeospatialCapability.PolygonSearch);
        capabilities.Should().NotHaveFlag(GeospatialCapability.NearestNeighbor);
    }

    // ── Contributor registration ─────────────────────────────────────────

    [Fact]
    public void The_module_registers_through_every_contributor_the_platform_offers()
    {
        var types = LocationAssembly.GetTypes();

        types.Should().Contain(typeof(LocationPermissionContributor));
        types.Should().Contain(typeof(LocationFeatureContributor));
        types.Should().Contain(typeof(LocationPolicyContributor));
        types.Should().Contain(typeof(LocationEndpointContributor));
        types.Should().Contain(typeof(LocationObservabilityContributor));
        types.Should().Contain(typeof(LocationDatabaseContributor));

        typeof(IPlatformModule).IsAssignableFrom(typeof(LocationModule)).Should().BeTrue();
    }

    /// <summary>
    /// Declaring a telemetry source without creating an ActivitySource behind it subscribes
    /// the tracer to silence — the B2 mistake the extension guide names.
    /// </summary>
    [Fact]
    public void The_declared_telemetry_source_has_real_instruments_behind_it()
    {
        var contributor = new LocationObservabilityContributor();

        contributor.ActivitySources.Should().Contain(LocationTelemetry.SourceName);
        contributor.Meters.Should().Contain(LocationTelemetry.SourceName);

        LocationTelemetry.Activity.Name.Should().Be(LocationTelemetry.SourceName);
        LocationTelemetry.Meter.Name.Should().Be(LocationTelemetry.SourceName);
    }

    // ── Frozen keys ──────────────────────────────────────────────────────

    [Fact]
    public void Every_declared_permission_is_owned_by_this_module()
    {
        var permissions = new LocationPermissionContributor().GetPermissions().ToList();

        permissions.Should().OnlyContain(
            permission => permission.Module == LocationModuleMetadata.Name);

        permissions.Should().OnlyContain(permission => permission.Key.StartsWith("location:"));
    }

    /// <summary>
    /// The registration flow asks a member to name their country before they have any tier.
    /// A catalogue readable only from Limited is an onboarding flow that cannot complete.
    /// </summary>
    [Fact]
    public void The_catalogue_is_readable_from_the_onboarding_tier()
    {
        var permissions = new LocationPermissionContributor().GetPermissions().ToList();

        permissions
            .Single(permission => permission.Key == LocationPermissions.ReadCatalog)
            .MinimumTier.Should().Be(AccessTier.Onboarding);

        permissions
            .Single(permission => permission.Key == LocationPermissions.Search)
            .MinimumTier.Should().Be(AccessTier.Onboarding);
    }

    /// <summary>
    /// A restricted member must still see where the platform thinks they are — that is the
    /// screen explaining why their results look the way they do.
    /// </summary>
    [Fact]
    public void Exactly_the_context_permissions_survive_restriction()
    {
        var surviving = new LocationPermissionContributor()
            .GetPermissions()
            .Where(permission => permission.GrantedWhenRestricted)
            .Select(permission => permission.Key)
            .ToList();

        surviving.Should().BeEquivalentTo(
        [
            LocationPermissions.ReadCatalog,
            LocationPermissions.Search,
            LocationPermissions.ReadProfile,
            LocationPermissions.ReadExperience
        ]);
    }

    /// <summary>Editing the catalogue changes what every member can find. Staff only.</summary>
    [Fact]
    public void Managing_the_catalogue_is_restricted_to_staff_roles()
    {
        var manage = new LocationPermissionContributor()
            .GetPermissions()
            .Single(permission => permission.Key == LocationPermissions.ManageCatalog);

        manage.Roles.Should().BeEquivalentTo("Admin", "SuperAdmin");
        manage.MinimumTier.Should().Be(AccessTier.Verified);
    }

    /// <summary>
    /// The kernel validates this at startup too. Asserting it here means the failure surfaces
    /// in a unit run rather than as a boot failure in CI.
    /// </summary>
    [Fact]
    public void Every_policy_names_a_permission_this_module_declares()
    {
        var declared = new LocationPermissionContributor()
            .GetPermissions()
            .Select(permission => permission.Key)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var policy in new LocationPolicyContributor().GetPolicies())
        {
            policy.Module.Should().Be(LocationModuleMetadata.Name);
            policy.Permission.Should().NotBeNull();
            declared.Should().Contain(policy.Permission!);
        }
    }

    [Fact]
    public void Every_feature_gate_is_owned_by_this_module()
    {
        new LocationFeatureContributor()
            .GetFeatures()
            .Should().OnlyContain(feature =>
                feature.Module == LocationModuleMetadata.Name &&
                feature.Name.StartsWith("location."));
    }

    [Fact]
    public void The_module_owns_exactly_one_database_schema()
    {
        var contributor = new LocationDatabaseContributor();

        contributor.Schema.Should().Be("location");
        contributor.Module.Should().Be(LocationModuleMetadata.Name);
    }

    /// <summary>
    /// Everything hangs off one prefix. A module claiming a top-level route is a module that
    /// can collide with another one.
    /// </summary>
    [Fact]
    public void Every_route_hangs_off_the_modules_own_prefix()
    {
        LocationModuleMetadata.RoutePrefix.Should().Be("/api/v1/location");
    }

    /// <summary>
    /// The module runs after Identity, Bookmarks and Trust. It takes over no kernel service —
    /// unlike Trust, which claims ITrustService — so ordering matters only for what it reads.
    /// </summary>
    [Fact]
    public void The_module_orders_after_identity_bookmarks_and_trust()
    {
        new LocationModule().Order.Should().BeGreaterThan(200);
    }

    // ── The extensibility contracts ──────────────────────────────────────

    /// <summary>
    /// The seams are the module's promise to every phase after this one. Renaming or removing
    /// one breaks a contract that verticals will already have implemented.
    /// </summary>
    [Fact]
    public void The_extension_seams_are_public_and_present()
    {
        typeof(INearbyResourceProvider).IsPublic.Should().BeTrue();
        typeof(ILocationResourceCounter).IsPublic.Should().BeTrue();
        typeof(ICountryMetadataContributor).IsPublic.Should().BeTrue();
        typeof(ILocationCatalogSeeder).IsPublic.Should().BeTrue();
        typeof(IGeospatialQueryProvider).IsPublic.Should().BeTrue();
        typeof(IGeocodingProvider).IsPublic.Should().BeTrue();
        typeof(IGeoDistanceCalculator).IsPublic.Should().BeTrue();
    }

    /// <summary>
    /// The consumer contracts other modules bind to. Also public, and also frozen.
    /// </summary>
    [Fact]
    public void The_consumer_contracts_are_public_and_present()
    {
        typeof(Modules.Location.Integration.ILocationDirectory).IsPublic.Should().BeTrue();
        typeof(Modules.Location.Integration.IResidencyDirectory).IsPublic.Should().BeTrue();
        typeof(Modules.Location.Integration.IGeographicScopeResolver).IsPublic.Should().BeTrue();
        typeof(Modules.Location.Integration.GeographicScope).IsPublic.Should().BeTrue();
    }

    /// <summary>
    /// A vertical implementing <c>INearbyResourceProvider</c> must be able to do so without
    /// referencing anything of Location's persistence or API — otherwise the seam has leaked.
    /// </summary>
    [Fact]
    public void The_extensibility_namespace_does_not_reach_into_persistence_or_api()
    {
        var result = Types.InAssembly(LocationAssembly)
            .That().ResideInNamespace("Diaspora.Modules.Location.Extensibility")
            .ShouldNot()
            .HaveDependencyOnAny(
                "Diaspora.Modules.Location.Persistence",
                "Diaspora.Modules.Location.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    // ── Naming ───────────────────────────────────────────────────────────

    /// <summary>
    /// Every contributor is sealed, matching the platform's convention and the reference
    /// module's shape. An open contributor invites an override the kernel will not call.
    /// </summary>
    [Fact]
    public void Every_contributor_implementation_is_sealed()
    {
        var contributorTypes = LocationAssembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type =>
                typeof(IPermissionContributor).IsAssignableFrom(type) ||
                typeof(IFeatureContributor).IsAssignableFrom(type) ||
                typeof(IPolicyContributor).IsAssignableFrom(type) ||
                typeof(IEndpointContributor).IsAssignableFrom(type) ||
                typeof(IObservabilityContributor).IsAssignableFrom(type))
            .ToList();

        contributorTypes.Should().NotBeEmpty();
        contributorTypes.Should().OnlyContain(type => type.IsSealed);
    }
}
