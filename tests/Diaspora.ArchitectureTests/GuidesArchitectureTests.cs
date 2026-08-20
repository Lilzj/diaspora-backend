using System.Reflection;
using Diaspora.Contracts.Authorization;
using Diaspora.Contracts.Trust.V1.Submission;
using Diaspora.Modules.Guides;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Authorization;
using Diaspora.Modules.Guides.Extensibility.Categories;
using Diaspora.Modules.Guides.Extensibility.Ranking;
using Diaspora.Modules.Guides.Extensibility.Search;
using Diaspora.Modules.Guides.Extensibility.Verification;
using Diaspora.Modules.Guides.Observability;
using Diaspora.Modules.Guides.Persistence;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Contributors;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The boundaries the Guides module must not cross, enforced rather than documented.
/// <para>
/// Two matter more than the rest. The first is that <b>B8 knows no vertical that will consume
/// it</b>. Guides is the last module in the dependency direction and the first knowledge
/// surface: Marketplace, Jobs, Housing, Events and every later module read it through
/// <c>IGuideDirectory</c> and <c>IGuideCatalog</c>. The day one of them appears in this
/// assembly, the reference has inverted and every knowledge-adjacent module has to queue behind
/// B8's release.
/// </para>
/// <para>
/// The second is that <b>B8 reaches Identity and Trust only through the kernel and published
/// contracts</b>. It consumes Location, Media, Community and Providers directly, because all
/// four publish consumption contracts for verticals; it must never reach into Identity's or
/// Trust's aggregates, which is the boundary ADR-002 draws — and which is exactly what would be
/// tempting for a module whose trust marks and author standing are both projections of Trust.
/// </para>
/// </summary>
public class GuidesArchitectureTests
{
    private static readonly Assembly Module = typeof(GuidesModule).Assembly;

    // ── Module boundaries ───────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Diaspora.Modules.Marketplace")]
    [InlineData("Diaspora.Modules.Jobs")]
    [InlineData("Diaspora.Modules.Housing")]
    [InlineData("Diaspora.Modules.Messaging")]
    [InlineData("Diaspora.Modules.Events")]
    [InlineData("Diaspora.Modules.CommunityLife")]
    [InlineData("Diaspora.Modules.Search")]
    [InlineData("Diaspora.Modules.Notifications")]
    [InlineData("Diaspora.Modules.Ai")]
    public void The_module_names_no_vertical_that_will_consume_it(string vertical)
    {
        var result = Types.InAssembly(Module)
            .ShouldNot()
            .HaveDependencyOn(vertical)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "later modules read Guides through IGuideDirectory and IGuideCatalog, never the " +
            "other way round: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void The_module_does_not_reach_into_identity_or_trust()
    {
        // Trust is consumed through the kernel and through Trust's published integration events
        // in Diaspora.Contracts — never through its aggregates. Identity likewise, through
        // ICurrentUser and a UserId value.
        var result = Types.InAssembly(Module)
            .Should()
            .NotHaveDependencyOnAny("Diaspora.Modules.Trust", "Diaspora.Domain.Identity")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "ADR-002 draws the line at Identity's and Trust's aggregates: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// B3.0.2. The guide verification claim really reaches Trust, and the point of this test is
    /// that doing so cost no coupling: it goes out through the published
    /// <c>ITrustVerificationSubmission</c> in <c>Diaspora.Contracts</c>, so
    /// <see cref="The_module_does_not_reach_into_identity_or_trust"/> above still passes.
    /// </summary>
    [Fact]
    public void The_module_submits_verifications_through_the_published_contract()
    {
        var trustProvider = Module.GetType(
            "Diaspora.Modules.Guides.Infrastructure.Trust.TrustGuideVerificationProvider");

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
    public void The_domain_layer_depends_on_no_framework()
    {
        var result = Types.InAssembly(Module)
            .That().ResideInNamespaceStartingWith("Diaspora.Modules.Guides.Domain")
            .Should()
            .NotHaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "MediatR",
                "FluentValidation")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the domain must be testable without a database, a web host or a mediator: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void The_domain_layer_does_not_depend_on_any_other_module()
    {
        // The aggregate holds Media and Provider identifiers, and Location's copied path, as
        // primitives rather than as another module's types. That is what keeps it unit-testable
        // and what would make an extraction of B8 a project reference change rather than a
        // rewrite.
        var result = Types.InAssembly(Module)
            .That().ResideInNamespaceStartingWith("Diaspora.Modules.Guides.Domain")
            .Should()
            .NotHaveDependencyOnAny(
                "Diaspora.Modules.Location",
                "Diaspora.Modules.Media",
                "Diaspora.Modules.Community",
                "Diaspora.Modules.Providers")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "cross-module consumption belongs in Application and Infrastructure, not the domain: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    // ── Contributor registration ────────────────────────────────────────────────────────────

    [Fact]
    public void The_module_registers_through_every_contributor_the_platform_offers()
    {
        typeof(IPlatformModule).IsAssignableFrom(typeof(GuidesModule)).Should().BeTrue();

        Implements<IPermissionContributor>(typeof(GuidePermissionContributor));
        Implements<IPolicyContributor>(typeof(GuidePolicyContributor));
        Implements<IFeatureContributor>(typeof(GuideFeatureContributor));
        Implements<IObservabilityContributor>(typeof(GuideObservabilityContributor));

        typeof(Diaspora.Persistence.Modules.IDatabaseContributor)
            .IsAssignableFrom(typeof(GuidesDatabaseContributor))
            .Should().BeTrue();

        Implements<IEndpointContributor>(EndpointContributor());
    }

    [Fact]
    public void Every_contributor_is_sealed_because_none_of_them_is_an_extension_point()
    {
        // The seams are the interfaces in Extensibility. A contributor is this module's own
        // declaration of what it owns, and a subclass of one would be a second module claiming
        // to be this one.
        var contributors = new[]
        {
            typeof(GuidePermissionContributor),
            typeof(GuidePolicyContributor),
            typeof(GuideFeatureContributor),
            typeof(GuideObservabilityContributor),
            typeof(GuidesDatabaseContributor),
            typeof(GuidesModule),
            EndpointContributor()
        };

        foreach (var contributor in contributors)
        {
            contributor.IsSealed.Should().BeTrue("{0} must be sealed", contributor.Name);
        }
    }

    [Fact]
    public void Every_contributor_is_constructible_before_the_container_exists()
    {
        // The kernel builds these during host construction, so a constructor dependency here is
        // a boot failure rather than a resolution failure.
        var contributors = new[]
        {
            typeof(GuidePermissionContributor),
            typeof(GuidePolicyContributor),
            typeof(GuideFeatureContributor),
            typeof(GuideObservabilityContributor),
            typeof(GuidesDatabaseContributor),
            typeof(GuidesModule)
        };

        foreach (var contributor in contributors)
        {
            contributor.GetConstructor(Type.EmptyTypes).Should().NotBeNull(
                "{0} must have a public parameterless constructor", contributor.Name);
        }
    }

    [Fact]
    public void Every_permission_key_is_namespaced_to_this_module()
    {
        foreach (var permission in new GuidePermissionContributor().GetPermissions())
        {
            permission.Key.Should().StartWith("guides:");
            permission.Module.Should().Be(GuidesModuleMetadata.Name);
        }
    }

    [Fact]
    public void Every_feature_and_policy_name_is_namespaced_to_this_module()
    {
        foreach (var feature in new GuideFeatureContributor().GetFeatures())
        {
            feature.Name.Should().StartWith("guides.");
            feature.Module.Should().Be(GuidesModuleMetadata.Name);
        }

        foreach (var policy in new GuidePolicyContributor().GetPolicies())
        {
            policy.Name.Should().StartWith("guides.");
            policy.Module.Should().Be(GuidesModuleMetadata.Name);
        }
    }

    [Fact]
    public void Every_policy_names_a_permission_this_module_declares()
    {
        // The kernel makes this a startup failure. Asserting it here turns a failed boot into a
        // failed test, which is a much cheaper place to find a typo.
        var permissions = new GuidePermissionContributor()
            .GetPermissions()
            .Select(permission => permission.Key)
            .ToHashSet(StringComparer.Ordinal);

        var policies = new GuidePolicyContributor().GetPolicies().ToList();

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
    public void No_permission_or_feature_sits_below_the_limited_tier()
    {
        // B2.0.1. A gate below Limited is granted to phone-unverified accounts, which is the
        // B6 lesson made policy.
        new GuidePermissionContributor().GetPermissions()
            .Should().OnlyContain(permission => permission.MinimumTier >= AccessTier.Limited,
                "a permission below Limited is granted to phone-unverified accounts");

        new GuideFeatureContributor().GetFeatures()
            .Should().OnlyContain(feature => feature.MinimumTier >= AccessTier.Limited,
                "a gate below Limited tells an unverified account it may do something it may not");
    }

    [Fact]
    public void Authoring_is_limited_and_publishing_is_verified()
    {
        // The product judgement the whole permission band rests on: writing is engagement,
        // publishing is a claim that carries the platform's name to people who will act on it.
        var permissions = new GuidePermissionContributor().GetPermissions().ToList();

        Tier(permissions, GuidePermissions.Create).Should().Be(AccessTier.Limited);
        Tier(permissions, GuidePermissions.Publish).Should().Be(AccessTier.Verified);
        Tier(permissions, GuidePermissions.Schedule).Should().Be(AccessTier.Verified);
        Tier(permissions, GuidePermissions.SubmitVerification).Should().Be(AccessTier.Verified);
    }

    [Fact]
    public void Correcting_your_own_published_advice_survives_a_restriction()
    {
        // Wrong information about a visa deadline hurts readers, not the restricted author.
        var permissions = new GuidePermissionContributor().GetPermissions().ToList();

        permissions.Single(permission => permission.Key == GuidePermissions.EditOwn)
            .GrantedWhenRestricted.Should().BeTrue();

        permissions.Single(permission => permission.Key == GuidePermissions.Report)
            .GrantedWhenRestricted.Should().BeTrue(
                "a restriction that silences the person best placed to flag harmful advice is " +
                "the wrong restriction");
    }

    // ── Extension seams ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Every_contributor_seam_the_brief_names_exists_and_is_a_public_interface()
    {
        var seams = new[]
        {
            typeof(IGuideCategoryContributor),
            typeof(IGuideVerificationProvider),
            typeof(IGuideRankingStrategy),
            typeof(IGuideSearchEngine)
        };

        foreach (var seam in seams)
        {
            seam.IsInterface.Should().BeTrue("{0} must be a contributor seam", seam.Name);
            seam.IsPublic.Should().BeTrue("{0} must be reachable from another assembly", seam.Name);
        }
    }

    [Fact]
    public void The_registries_that_resolve_the_seams_are_public_too()
    {
        typeof(GuideRankingStrategyRegistry).IsPublic.Should().BeTrue();
        typeof(GuideVerificationProviderRegistry).IsPublic.Should().BeTrue();
    }

    [Fact]
    public void The_module_is_discoverable_by_the_kernels_assembly_scan()
    {
        // The host references this assembly and names it nowhere else. If the scan cannot find
        // it, the module is present in the build output and absent from the running platform.
        var modules = ModuleAssemblyScanner.FindImplementations<IPlatformModule>(
            ModuleAssemblyScanner.FindPlatformAssemblies());

        modules.Should().Contain(typeof(GuidesModule));
    }

    [Fact]
    public void The_integration_surface_carries_no_domain_value_objects()
    {
        // A consuming vertical must be able to read a GuideSummary without referencing B8's
        // domain. The moment one of these carries a value object, every consumer is coupled to
        // B8's internals rather than to its contract.
        var result = Types.InAssembly(Module)
            .That().ResideInNamespace("Diaspora.Modules.Guides.Integration")
            .And().AreClasses()
            .Should()
            .NotHaveDependencyOn("Diaspora.Modules.Guides.Domain.ValueObjects")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the published integration records must be flat primitives: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    // ── Media and geography discipline ──────────────────────────────────────────────────────

    /// <summary>
    /// ADR-005, stated precisely: <b>an asset this platform hosts is a reference, never a URL.</b> A
    /// guide outlives storage migrations, CDN changes and signed-URL expiry; a stored URL breaks on
    /// all three while a reference survives them.
    /// <para>
    /// <b>A link to somebody else's page is a different thing, and B8.2 made the distinction
    /// unavoidable.</b> An immigration department's circular, a mission's appointment portal and a
    /// visa application page are all external addresses this platform does not host, cannot migrate
    /// and must not pretend to own — there is no reference form for them, and refusing to store one
    /// would mean the platform cannot say where a notice came from. The exclusions below are those,
    /// named individually rather than by pattern, so a media property called <c>ImageUrl</c> still
    /// fails the build.
    /// </para>
    /// </summary>
    [Fact]
    public void No_domain_type_stores_a_url_for_an_asset_this_platform_hosts()
    {
        // The SEO canonical genuinely is a URL — it is what a crawler is told, and it is validated
        // as absolute https for exactly that reason. The rest are external addresses B8.2 records
        // so a reader can go to the source; every one is validated as absolute http or https and
        // none of them is media.
        var external = new[]
        {
            "CanonicalUrl",
            "SourceUrl",
            "ApplicationUrl",
            "AppointmentUrl"
        };

        var offenders = Module.GetTypes()
            .Where(type => type.Namespace?.StartsWith(
                "Diaspora.Modules.Guides.Domain", StringComparison.Ordinal) == true)
            .SelectMany(type => type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(property =>
                property.Name.EndsWith("Url", StringComparison.Ordinal)
                || property.Name.EndsWith("Uri", StringComparison.Ordinal))
            .Where(property => !external.Contains(property.Name, StringComparer.Ordinal))
            .Select(property => $"{property.DeclaringType?.Name}.{property.Name}")
            .ToList();

        offenders.Should().BeEmpty(
            "media is stored as a reference, never as a URL (ADR-005): " +
            string.Join(", ", offenders));
    }

    /// <summary>
    /// The other half of the rule above, and the one that gives it teeth: <b>every media-shaped
    /// property on a domain type is an identifier.</b> Widening the URL rule for external links
    /// would be worthless if a media property could then be renamed to <c>MediaSourceUrl</c> and slip
    /// past it.
    /// </summary>
    [Fact]
    public void Every_media_property_on_a_domain_type_is_an_identifier()
    {
        var offenders = Module.GetTypes()
            .Where(type => type.Namespace?.StartsWith(
                "Diaspora.Modules.Guides.Domain", StringComparison.Ordinal) == true)
            .SelectMany(type => type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(property =>
                property.Name.Contains("Media", StringComparison.Ordinal)
                || property.Name.Contains("Image", StringComparison.Ordinal)
                || property.Name.Contains("Cover", StringComparison.Ordinal)
                || property.Name.Contains("Thumbnail", StringComparison.Ordinal))
            .Where(property => property.PropertyType == typeof(string))
            .Select(property => $"{property.DeclaringType?.Name}.{property.Name}")
            .ToList();

        offenders.Should().BeEmpty(
            "an asset this platform hosts is a MediaId, never a string (ADR-005): " +
            string.Join(", ", offenders));
    }

    [Fact]
    public void The_module_computes_no_distance_of_its_own()
    {
        // Every distance comes from B4. A haversine here would be a second, subtly different
        // distance in the same response as Location's.
        var offenders = Module.GetTypes()
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
                | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(method =>
                method.Name.Contains("Haversine", StringComparison.OrdinalIgnoreCase)
                || method.Name.Contains("DistanceBetween", StringComparison.OrdinalIgnoreCase))
            .Select(method => $"{method.DeclaringType?.Name}.{method.Name}")
            .ToList();

        offenders.Should().BeEmpty("geography belongs to B4, not to B8");
    }

    // ── Observability ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_declared_telemetry_sources_have_real_instruments_behind_them()
    {
        // Declaring a source name with nothing behind it subscribes the tracer to silence,
        // which is the mistake the module extension guide names and which B2 actually made.
        var contributor = new GuideObservabilityContributor();

        contributor.ActivitySources.Should().Contain(GuideTelemetry.SourceName);
        contributor.Meters.Should().Contain(GuideTelemetry.SourceName);

        GuideTelemetry.ActivitySource.Name.Should().Be(GuideTelemetry.SourceName);
        GuideTelemetry.Meter.Name.Should().Be(GuideTelemetry.SourceName);
    }

    // ── Wire contract stability ─────────────────────────────────────────────────────────────

    /// <summary>
    /// The module owns exactly one schema, and every route prefix in the assembly is one it
    /// declared.
    /// <para>
    /// <b>B8.1 added a second prefix, and the invariant this test protects is unchanged.</b> The
    /// point was never "one prefix" for its own sake — it is that no public URL space exists that
    /// nobody versioned and nobody declared. <c>/api/v1/knowledge</c> is declared in
    /// <see cref="GuidesModuleMetadata"/>, is versioned, and is a first-class surface rather than a
    /// sub-resource: Jobs, Housing, Marketplace and Community Life all consume it, and nesting it
    /// under <c>/api/v1/guides</c> would have told every one of them that country knowledge is an
    /// implementation detail of guides. One schema, though, stays one schema — B8.1 is the same
    /// module, and a second schema would have implied a second owner of guide content.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_route_prefix_is_declared_and_versioned_and_the_schema_is_singular()
    {
        GuidesModuleMetadata.RoutePrefix.Should().Be("/api/v1/guides");
        GuidesModuleMetadata.KnowledgeRoutePrefix.Should().Be("/api/v1/knowledge");

        // B8.2. A third declared prefix on one module, for the reason B8.1's second one exists:
        // nesting official notices under the knowledge prefix would tell every client that what a
        // government announced is a sub-resource of what a community wrote about it.
        GuidesModuleMetadata.IntelligenceRoutePrefix.Should().Be("/api/v1/intelligence");

        GuidesModuleMetadata.Schema.Should().Be("guides");
        new GuidesDatabaseContributor().Schema.Should().Be(GuidesModuleMetadata.Schema);

        // A prefix declared anywhere in the assembly that is not one of the three above would be a
        // public URL space nobody versioned. The invariant is unchanged: three declared prefixes
        // are fine; one undeclared one is not.
        var prefixes = Module.GetTypes()
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(field => field is { IsLiteral: true, IsInitOnly: false }
                && field.FieldType == typeof(string))
            .Select(field => (string?)field.GetRawConstantValue())
            .Where(value => value?.StartsWith("/api/", StringComparison.Ordinal) == true)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        prefixes.Should().BeEquivalentTo(
        [
            GuidesModuleMetadata.RoutePrefix,
            GuidesModuleMetadata.KnowledgeRoutePrefix,
            GuidesModuleMetadata.IntelligenceRoutePrefix
        ]);

        prefixes.Should().OnlyContain(
            prefix => prefix!.StartsWith("/api/v1/", StringComparison.Ordinal),
            "versioning is baked into the prefix: a breaking wire change is a new prefix");
    }

    [Fact]
    public void The_sort_keys_a_query_string_carries_are_lower_case_kebab()
    {
        // They travel in URLs and the frontend contract pins them that way; the enum names do
        // not, so this is the one place the two vocabularies deliberately differ.
        GuideWireNames.ToWire(Diaspora.Modules.Guides.Domain.Enums.GuideSortOrder.MostHelpful)
            .Should().Be("most-helpful");

        GuideWireNames.ToWire(Diaspora.Modules.Guides.Domain.Enums.GuideSortOrder.RecentlyUpdated)
            .Should().Be("recently-updated");

        GuideWireNames.ToWire(Diaspora.Modules.Guides.Domain.Enums.GuideSortOrder.Trending)
            .Should().Be("trending");
    }

    /// <summary>
    /// Resolved by name rather than by symbol: the endpoint surface is written independently of
    /// this suite, and a test that names the type through reflection still fails if it is
    /// renamed or removed — which is the assertion — without coupling the build of this
    /// assembly to an in-flight file.
    /// </summary>
    private static Type EndpointContributor()
    {
        var contributor = Module.GetType(
            "Diaspora.Modules.Guides.Api.GuideEndpointContributor");

        contributor.Should().NotBeNull(
            "the module maps its own routes; without an endpoint contributor it is a library, " +
            "not a module");

        return contributor!;
    }

    private static AccessTier? Tier(IEnumerable<PermissionDefinition> permissions, string key) =>
        permissions.Single(permission => permission.Key == key).MinimumTier;

    private static void Implements<TContributor>(Type candidate) =>
        typeof(TContributor).IsAssignableFrom(candidate).Should().BeTrue(
            "{0} must implement {1}", candidate.Name, typeof(TContributor).Name);
}
