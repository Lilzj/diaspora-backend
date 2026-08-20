using System.Reflection;
using Diaspora.Contracts.Authorization;
using Diaspora.Contracts.Trust.V1.Submission;
using Diaspora.Modules.Providers;
using Diaspora.Modules.Providers.Api;
using Diaspora.Modules.Providers.Application.Services;
using Diaspora.Modules.Providers.Authorization;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Extensibility.Availability;
using Diaspora.Modules.Providers.Extensibility.Categories;
using Diaspora.Modules.Providers.Extensibility.Pricing;
using Diaspora.Modules.Providers.Extensibility.Ranking;
using Diaspora.Modules.Providers.Extensibility.Recommendations;
using Diaspora.Modules.Providers.Extensibility.Search;
using Diaspora.Modules.Providers.Extensibility.Types;
using Diaspora.Modules.Providers.Extensibility.Verification;
using Diaspora.Modules.Providers.Integration;
using Diaspora.Modules.Providers.Observability;
using Diaspora.Modules.Providers.Persistence;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Contributors;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The boundaries the Providers module must not cross, enforced rather than documented.
/// <para>
/// Two matter more than the rest. The first is that <b>no business vertical appears in this
/// assembly</b>: the entire promise of B7 — that Marketplace, Jobs, Housing, Events and
/// Community Life consume it rather than each modelling a provider — depends on B7 not knowing
/// any of them exist. The day one appears here, the reusable foundation has become a bottleneck
/// every business module has to queue behind.
/// </para>
/// <para>
/// The second is that <b>B7 reaches Identity and Trust only through kernel and published
/// contracts</b>. It consumes Location, Media and Community directly, because all three publish
/// consumption contracts for verticals and their own freezes say so; it must never reach into
/// Identity's or Trust's aggregates, which is the boundary ADR-002 draws.
/// </para>
/// </summary>
public class ProviderArchitectureTests
{
    private static readonly Assembly Module = typeof(ProvidersModule).Assembly;

    // ── Module boundaries ───────────────────────────────────────────────────────────────────

    [Fact]
    public void The_module_names_no_vertical_that_will_consume_it()
    {
        var result = Types.InAssembly(Module)
            .Should()
            .NotHaveDependencyOnAny(
                "Diaspora.Modules.Marketplace",
                "Diaspora.Modules.Jobs",
                "Diaspora.Modules.Housing",
                "Diaspora.Modules.Events",
                "Diaspora.Modules.CommunityLife",
                "Diaspora.Modules.Guides",
                "Diaspora.Modules.Search",
                "Diaspora.Modules.Notifications",
                "Diaspora.Modules.Ai")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "a reusable provider foundation cannot depend on the modules that reuse it");
    }

    /// <summary>
    /// B3.0.2. B7 now really submits to Trust rather than returning a stub, and the point of
    /// this test is that doing so cost it no coupling: the claim goes out through the published
    /// <c>ITrustVerificationSubmission</c> in <c>Diaspora.Contracts</c>, so
    /// <see cref="The_module_does_not_reach_into_identity_or_trust"/> below still passes.
    /// </summary>
    [Fact]
    public void The_module_submits_verifications_through_the_published_contract()
    {
        var trustProvider = Module.GetType(
            "Diaspora.Modules.Providers.Infrastructure.Trust.TrustProviderVerificationProvider");

        trustProvider.Should().NotBeNull();

        var dependencies = trustProvider!
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToList();

        dependencies.Should().Contain(typeof(ITrustVerificationSubmission),
            "the claim goes out through the published contract, not a stub and not an HTTP call");

        dependencies.Should().NotContain(
            type => type.Assembly.GetName().Name == "Diaspora.Modules.Trust",
            "consuming the contract must not mean referencing the module");
    }

    [Fact]
    public void The_module_does_not_reach_into_identity_or_trust()
    {
        // Trust is consumed through the kernel's ITrustService and through Trust's published
        // integration events in Diaspora.Contracts — never through its aggregates. Identity
        // likewise, through ICurrentUser.
        var result = Types.InAssembly(Module)
            .Should()
            .NotHaveDependencyOnAny("Diaspora.Modules.Trust", "Diaspora.Domain.Identity")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "ADR-002 draws the line at Identity's and Trust's aggregates");
    }

    [Fact]
    public void The_domain_layer_depends_on_no_framework()
    {
        var result = Types.InAssembly(Module)
            .That().ResideInNamespaceStartingWith("Diaspora.Modules.Providers.Domain")
            .Should()
            .NotHaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "MediatR",
                "FluentValidation")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the domain must be testable without a database, a web host or a mediator");
    }

    [Fact]
    public void The_domain_layer_does_not_depend_on_any_other_module()
    {
        // The aggregate holds Media identifiers and Location's copied path as primitives, not as
        // another module's types. That is what keeps it unit-testable and what would make an
        // extraction of B7 a project reference change rather than a rewrite.
        var result = Types.InAssembly(Module)
            .That().ResideInNamespaceStartingWith("Diaspora.Modules.Providers.Domain")
            .Should()
            .NotHaveDependencyOnAny(
                "Diaspora.Modules.Location",
                "Diaspora.Modules.Media",
                "Diaspora.Modules.Community")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "cross-module consumption belongs in Application and Infrastructure, not the domain");
    }

    // ── Contributor registration ────────────────────────────────────────────────────────────

    [Fact]
    public void The_module_registers_through_every_contributor_the_platform_offers()
    {
        typeof(IPlatformModule).IsAssignableFrom(typeof(ProvidersModule)).Should().BeTrue();

        Implements<IPermissionContributor>(typeof(ProviderPermissionContributor));
        Implements<IPolicyContributor>(typeof(ProviderPolicyContributor));
        Implements<IFeatureContributor>(typeof(ProviderFeatureContributor));
        Implements<IEndpointContributor>(typeof(ProviderEndpointContributor));
        Implements<IObservabilityContributor>(typeof(ProviderObservabilityContributor));

        typeof(Diaspora.Persistence.Modules.IDatabaseContributor)
            .IsAssignableFrom(typeof(ProvidersDatabaseContributor))
            .Should().BeTrue();
    }

    [Fact]
    public void Every_policy_names_a_permission_this_module_declares()
    {
        // The kernel makes this a startup failure. Asserting it here turns a failed boot into a
        // failed test, which is a much cheaper place to find a typo.
        var permissions = new ProviderPermissionContributor()
            .GetPermissions()
            .Select(permission => permission.Key)
            .ToHashSet(StringComparer.Ordinal);

        var policies = new ProviderPolicyContributor().GetPolicies().ToList();

        policies.Should().NotBeEmpty();

        foreach (var policy in policies.Where(policy => policy.Permission is not null))
        {
            permissions.Should().Contain(
                policy.Permission!,
                "policy '{0}' names a permission that must exist",
                policy.Name);
        }
    }

    [Fact]
    public void Every_permission_and_feature_is_namespaced_to_this_module()
    {
        foreach (var permission in new ProviderPermissionContributor().GetPermissions())
        {
            permission.Key.Should().StartWith("providers:");
            permission.Module.Should().Be(ProvidersModuleMetadata.Name);
        }

        foreach (var feature in new ProviderFeatureContributor().GetFeatures())
        {
            feature.Name.Should().StartWith("providers.");
            feature.Module.Should().Be(ProvidersModuleMetadata.Name);
        }
    }

    [Fact]
    public void Registering_a_provider_is_gated_at_the_verified_tier()
    {
        // An unverified account publishing a money-transfer business into a diaspora community
        // is the single worst thing this module can be used for.
        var register = new ProviderPermissionContributor()
            .GetPermissions()
            .Single(permission => permission.Key == ProviderPermissions.Register);

        register.MinimumTier.Should().Be(AccessTier.Verified);
    }

    [Fact]
    public void Reading_survives_restriction_so_a_restricted_member_still_sees_a_directory()
    {
        var read = new ProviderPermissionContributor()
            .GetPermissions()
            .Single(permission => permission.Key == ProviderPermissions.Read);

        read.GrantedWhenRestricted.Should().BeTrue(
            "a directory that 403s for a restricted member looks like an outage");
    }

    // ── Extension seams ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Every_contributor_seam_the_brief_names_exists_and_is_an_interface()
    {
        var seams = new[]
        {
            typeof(IServiceCategoryContributor),
            typeof(IProviderVerificationProvider),
            typeof(IProviderAvailabilityProvider),
            typeof(IProviderPricingFormatter),
            typeof(IProviderRankingStrategy),
            typeof(IProviderSearchProjectionContributor),
            typeof(IProviderRecommendationProvider),
            typeof(IProviderTypeContributor)
        };

        foreach (var seam in seams)
        {
            seam.IsInterface.Should().BeTrue("{0} must be a contributor seam", seam.Name);
            seam.IsPublic.Should().BeTrue("{0} must be reachable from another assembly", seam.Name);
        }
    }

    [Fact]
    public void The_integration_surface_is_flat_and_carries_no_domain_types()
    {
        // A consuming vertical must be able to read a ProviderSummary without referencing B7's
        // domain. The moment one of these carries a strongly typed identifier or a value object,
        // every consumer is coupled to B7's internals rather than to its contract.
        var result = Types.InAssembly(Module)
            .That().ResideInNamespace("Diaspora.Modules.Providers.Integration")
            .And().AreClasses()
            .Should()
            .NotHaveDependencyOn("Diaspora.Modules.Providers.Domain.ValueObjects")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the published integration records must be flat primitives");
    }

    // ── Media, geography and search discipline ──────────────────────────────────────────────

    [Fact]
    public void No_domain_type_stores_a_url()
    {
        // ADR-005. The frontend stores raw URLs in thirty-one fields across seventeen types;
        // this test is what stops that pattern reaching the server.
        var offenders = Module.GetTypes()
            .Where(type => type.Namespace?.StartsWith(
                "Diaspora.Modules.Providers.Domain", StringComparison.Ordinal) == true)
            .SelectMany(type => type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(property =>
                property.Name.EndsWith("Url", StringComparison.Ordinal)
                || property.Name.EndsWith("Uri", StringComparison.Ordinal))

            // A contact method's website genuinely is a URL — it is what the member typed and
            // what the client dials. Media is the thing that must never be one.
            .Where(property => property.DeclaringType?.Name != "ProviderContactMethod")
            .Select(property => $"{property.DeclaringType?.Name}.{property.Name}")
            .ToList();

        offenders.Should().BeEmpty(
            "media is stored as a reference, never as a URL (ADR-005)");
    }

    [Fact]
    public void The_module_computes_no_distance_of_its_own()
    {
        // Every distance comes from ILocationDirectory. A haversine here would be a second,
        // subtly different distance in the same response as B4's.
        var offenders = Module.GetTypes()
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
                | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(method =>
                method.Name.Contains("Haversine", StringComparison.OrdinalIgnoreCase)
                || method.Name.Contains("DistanceBetween", StringComparison.OrdinalIgnoreCase))
            .Select(method => $"{method.DeclaringType?.Name}.{method.Name}")
            .ToList();

        offenders.Should().BeEmpty("geography belongs to B4, not to B7");
    }

    // ── Observability ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_declared_telemetry_sources_have_real_instruments_behind_them()
    {
        // Declaring a source name with nothing behind it subscribes the tracer to silence, which
        // is the mistake the module extension guide names and which B2 actually made.
        var contributor = new ProviderObservabilityContributor();

        contributor.ActivitySources.Should().Contain(ProviderTelemetry.SourceName);
        contributor.Meters.Should().Contain(ProviderTelemetry.SourceName);

        ProviderTelemetry.Activity.Name.Should().Be(ProviderTelemetry.SourceName);
        ProviderTelemetry.Meter.Name.Should().Be(ProviderTelemetry.SourceName);
    }

    // ── Wire contract stability ─────────────────────────────────────────────────────────────

    [Fact]
    public void Provider_roles_serialise_lower_case_to_match_the_client()
    {
        // The client compares 'owner' | 'manager' | 'moderator' | 'contributor' as string
        // literals and keys its role-tab filter on them. Emitting "Owner" breaks a render
        // silently.
        ProviderWireNames.ToWire(ProviderRole.Owner)
            .Should().Be("owner");

        ProviderWireNames.ToWire(ProviderRole.Manager)
            .Should().Be("manager");

        ProviderWireNames.ToWire(ProviderRole.Moderator)
            .Should().Be("moderator");

        ProviderWireNames.ToWire(ProviderRole.Contributor)
            .Should().Be("contributor");
    }

    [Fact]
    public void The_clients_existing_sort_values_are_still_accepted()
    {
        // discover.tsx sends 'default', 'distance' and 'rating'. All three must keep working
        // while the client migrates to the contract names.
        ProviderWireNames.ParseSort("default").Value.Should().Be(ProviderSortOrder.Recommended);
        ProviderWireNames.ParseSort("distance").Value.Should().Be(ProviderSortOrder.Distance);
        ProviderWireNames.ParseSort("rating").Value.Should().Be(ProviderSortOrder.Rating);
        ProviderWireNames.ParseSort(null).Value.Should().Be(ProviderSortOrder.Recommended);
    }

    [Fact]
    public void The_module_owns_exactly_one_route_prefix_and_one_schema()
    {
        ProvidersModuleMetadata.RoutePrefix.Should().Be("/api/v1/providers");
        ProvidersModuleMetadata.Schema.Should().Be("providers");
        new ProvidersDatabaseContributor().Schema.Should().Be(ProvidersModuleMetadata.Schema);
    }

    private static void Implements<TContributor>(Type candidate) =>
        typeof(TContributor).IsAssignableFrom(candidate).Should().BeTrue(
            "{0} must implement {1}", candidate.Name, typeof(TContributor).Name);
}
