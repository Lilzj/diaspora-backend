using System.Reflection;
using Diaspora.Contracts.Trust.V1.Submission;
using Diaspora.Modules.Jobs;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Authorization;
using Diaspora.Modules.Jobs.Extensibility.Categories;
using Diaspora.Modules.Jobs.Extensibility.Ranking;
using Diaspora.Modules.Jobs.Extensibility.Recommendations;
using Diaspora.Modules.Jobs.Extensibility.Search;
using Diaspora.Modules.Jobs.Infrastructure.Location;
using Diaspora.Modules.Jobs.Infrastructure.Trust;
using Diaspora.Modules.Jobs.Observability;
using Diaspora.Modules.Jobs.Persistence;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Contributors;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The boundaries the Jobs module must not cross, enforced rather than documented.
/// <para>
/// The brief names five explicitly — Jobs never owns Trust data, Media, Location or Provider
/// data, and consumes platform capabilities only through published contracts. Each is a test
/// below, and each is the kind of rule that is true on the day it is written and quietly false
/// two releases later unless something checks.
/// </para>
/// </summary>
public class JobsArchitectureTests
{
    private static readonly Assembly Module = typeof(JobsModule).Assembly;

    // ── The five ownership rules from the brief ─────────────────────────────────────────────

    /// <summary>
    /// <b>Jobs never owns Trust data.</b> The badge on an employer is Trust's; B9 holds a
    /// projection it can only ever receive, never grant.
    /// </summary>
    [Fact]
    public void The_module_does_not_reach_into_trust_or_identity()
    {
        var result = Types.InAssembly(Module)
            .Should()
            .NotHaveDependencyOnAny("Diaspora.Modules.Trust", "Diaspora.Domain.Identity")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "ADR-002 draws the line at Identity's and Trust's aggregates. Jobs reaches Identity "
            + "through ICurrentUser and Trust through Diaspora.Contracts: "
            + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// B3.0.2. The verification claim really reaches Trust, and the point of this test is that
    /// doing so cost no coupling: it goes out through the published contract in
    /// <c>Diaspora.Contracts</c>, so the test above still passes.
    /// </summary>
    [Fact]
    public void The_module_submits_verifications_through_the_published_contract()
    {
        var dependencies = typeof(JobTrustVerificationProvider)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToList();

        dependencies.Should().Contain(typeof(ITrustVerificationSubmission),
            "the claim goes out through the published contract, not a stub and not an HTTP call");

        dependencies.Should().NotContain(
            type => type.Assembly.GetName().Name == "Diaspora.Modules.Trust",
            "consuming the contract must not mean referencing the module that decides");
    }

    /// <summary>
    /// <b>Jobs never owns Media.</b> Every attachment is a <c>MediaId</c>; the module holds no
    /// stream, no byte array and no stored URL, and exactly one class knows how to talk to B5.
    /// </summary>
    [Fact]
    public void Only_one_class_talks_to_media()
    {
        var talkers = Types.InAssembly(Module)
            .That().HaveDependencyOn("Diaspora.Modules.Media")
            .GetTypes()
            .Select(type => type.Name)
            .ToList();

        talkers.Should().BeEquivalentTo(
            [nameof(JobMediaClaimer)],
            "B5's rule is attach-by-reference, and containing the knowledge in one class is what "
            + "makes it checkable");
    }

    /// <summary>
    /// <b>Jobs never owns Location.</b> Geography is resolved through B4's published scope
    /// resolver, and contributed back through B4's published counter seam. Two classes, one in each
    /// direction, and nothing else in the module knows Location exists.
    /// <para>
    /// <b>Why the counter is permitted rather than a breach.</b> The rule this test keeps is what
    /// its message says: B9 parses no radius, resolves no city name and computes no distance.
    /// <c>JobResourceCounter</c> does none of those — it answers "how many open postings are filed
    /// under this path" from Jobs' own repository, which is a question only Jobs can answer. The
    /// dependency is on <c>ILocationResourceCounter</c> and the value objects its signature takes,
    /// which is a contribution seam B4 declares and this module's own project file has named as a
    /// contract it consumes since B9 shipped.
    /// </para>
    /// <para>
    /// B21 added it. Jobs was the one vertical of seven with no counter registered, so every
    /// neighbourhood hub rendered a dash where a jobs figure belongs and the density layer had no
    /// jobs source at all — and because an unreachable counter and an unregistered one are the same
    /// absent key, nothing said so.
    /// </para>
    /// <para>
    /// <b>Filing added two more on 16 Sep 2026, and the rule they keep is the same one.</b> The
    /// counter aggregates by area path and no posting had ever been given one, so every town's jobs
    /// figure — and the map's cluster count — read zero. <c>JobAreaFiling</c> hands the country,
    /// region, city and area id a posting already holds to Location's <c>IAreaLocator</c> and stores
    /// the path it answers; <c>JobAreaFilingSource</c> is the contribution Location's sweep calls to
    /// file postings written before that. Neither resolves a city name: both ask B4 to.
    /// </para>
    /// </summary>
    [Fact]
    public void Only_the_location_seams_talk_to_location()
    {
        var talkers = Types.InAssembly(Module)
            .That().HaveDependencyOn("Diaspora.Modules.Location")
            .GetTypes()
            .Select(type => type.Name)
            .ToList();

        talkers.Should().BeEquivalentTo(
            [
                nameof(JobGeographicScopeAdapter),
                nameof(JobResourceCounter),
                nameof(JobAreaFiling),
                nameof(JobAreaFilingSource)
            ],
            "B9 parses no radius, resolves no city name and computes no distance; it narrows a "
            + "filter through B4's resolver, files a posting through B4's locator and reports its "
            + "own count through B4's counter seam");
    }

    /// <summary>
    /// <b>Jobs never owns Provider data.</b> A posting stores an identifier; the name, badge and
    /// rating are B7's answers at read time.
    /// <para>
    /// The rule is about the <em>call seam</em>, not about the published DTO. Exactly one class
    /// may hold <c>IProviderDirectory</c> and know how to ask B7 a question; the mapper and the
    /// read handler legitimately pass the returned <c>ProviderSummary</c> around, which is what
    /// a published consumption contract is for. Asserting on any dependency at all would forbid
    /// consuming the contract, which is the opposite of the intent.
    /// </para>
    /// </summary>
    [Fact]
    public void Only_one_class_holds_the_provider_directory()
    {
        HoldersOf("Diaspora.Modules.Providers.Integration.IProviderDirectory")
            .Should().BeEquivalentTo(
                [nameof(JobProviderResolver)],
                "an agency that renames itself must never be misrepresented by an old posting, "
                + "which means one class asks B7 and nothing caches the answer");
    }

    /// <summary>Guides likewise: identifiers stored, titles resolved at read time.</summary>
    [Fact]
    public void Only_one_class_holds_the_guide_directory()
    {
        HoldersOf("Diaspora.Modules.Guides.Integration.IGuideDirectory")
            .Should().BeEquivalentTo(
                [nameof(JobGuideResolver)],
                "a posting must never render a link to a guide that has since been unpublished");
    }

    /// <summary>
    /// <b>Neither module's aggregates are reachable.</b> The published Integration namespaces
    /// are the whole consumption surface; the domain behind them is not B9's business.
    /// </summary>
    [Theory]
    [InlineData("Diaspora.Modules.Providers.Domain.Providers")]
    [InlineData("Diaspora.Modules.Providers.Domain.Reviews")]
    [InlineData("Diaspora.Modules.Guides.Domain.Guides")]
    [InlineData("Diaspora.Modules.Guides.Domain.Versions")]
    public void The_module_reaches_no_foreign_aggregate(string aggregateNamespace)
    {
        var result = Types.InAssembly(Module)
            .Should()
            .NotHaveDependencyOn(aggregateNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "B9 consumes published Integration contracts, never another module's aggregates: "
            + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// The types whose constructors take a given service. A constructor dependency is what
    /// "holds" means here: it is the class that can actually call the foreign module.
    /// </summary>
    private static List<string> HoldersOf(string serviceTypeName) =>
        [.. Module.GetTypes()
            .Where(type => type.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Any(parameter =>
                    string.Equals(
                        parameter.ParameterType.FullName,
                        serviceTypeName,
                        StringComparison.Ordinal)))
            .Select(type => type.Name)];

    // ── Layering ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_domain_layer_depends_on_no_framework()
    {
        var result = Types.InAssembly(Module)
            .That().ResideInNamespaceStartingWith("Diaspora.Modules.Jobs.Domain")
            .Should()
            .NotHaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "MediatR",
                "FluentValidation")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the domain is where the rules live and it must be testable without a host: "
            + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// The domain reaches no other module at all — not even the ones B9 legitimately consumes.
    /// Cross-module work belongs in the application and infrastructure layers, where it is
    /// visible.
    /// </summary>
    [Fact]
    public void The_domain_layer_reaches_no_other_module()
    {
        var result = Types.InAssembly(Module)
            .That().ResideInNamespaceStartingWith("Diaspora.Modules.Jobs.Domain")
            .Should()
            .NotHaveDependencyOnAny(
                "Diaspora.Modules.Location",
                "Diaspora.Modules.Media",
                "Diaspora.Modules.Community",
                "Diaspora.Modules.Providers",
                "Diaspora.Modules.Guides")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "an aggregate that knew about another module could not be unit tested: "
            + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// <b>The search document is flat primitives only.</b> This is the test that actually
    /// enforces the OpenSearch-independence claim: the moment a value object or a domain enum
    /// leaked into it, an adapter would need a reference to B9's domain.
    /// </summary>
    [Fact]
    public void The_search_document_carries_no_domain_type()
    {
        var domainProperties = typeof(JobSearchDocument)
            .GetProperties()
            .Where(property =>
                property.PropertyType.Namespace?.StartsWith(
                    "Diaspora.Modules.Jobs.Domain", StringComparison.Ordinal) == true)
            .Select(property => property.Name)
            .ToList();

        domainProperties.Should().BeEmpty(
            "an engine adapter must serialise the document without referencing this module's "
            + "domain — that is the whole OpenSearch-independence claim");
    }

    // ── Composition ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_module_names_no_vertical_that_will_consume_it()
    {
        var result = Types.InAssembly(Module)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Diaspora.Modules.Marketplace",
                "Diaspora.Modules.Housing",
                "Diaspora.Modules.Messaging",
                "Diaspora.Modules.Events",
                "Diaspora.Modules.Search",
                "Diaspora.Modules.Notifications",
                "Diaspora.Modules.Ai")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "later modules read Jobs through published contracts, never the other way round: "
            + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void The_module_contributes_through_every_kernel_seam()
    {
        var contributors = Types.InAssembly(Module).GetTypes().ToList();

        contributors.Should().Contain(typeof(JobsModule));
        contributors.Should().Contain(typeof(JobPermissionContributor));
        contributors.Should().Contain(typeof(JobFeatureContributor));
        contributors.Should().Contain(typeof(JobPolicyContributor));
        contributors.Should().Contain(typeof(JobObservabilityContributor));
        contributors.Should().Contain(typeof(JobsDatabaseContributor));

        typeof(IPlatformModule).IsAssignableFrom(typeof(JobsModule)).Should().BeTrue();
        typeof(IPermissionContributor)
            .IsAssignableFrom(typeof(JobPermissionContributor)).Should().BeTrue();
        typeof(IFeatureContributor)
            .IsAssignableFrom(typeof(JobFeatureContributor)).Should().BeTrue();
        typeof(IPolicyContributor)
            .IsAssignableFrom(typeof(JobPolicyContributor)).Should().BeTrue();
    }

    /// <summary>
    /// Every contributor the kernel constructs before the container exists must have a public
    /// parameterless constructor. A dependency here is a startup crash, not a compile error.
    /// </summary>
    [Fact]
    public void Every_catalog_contributor_is_constructible_without_the_container()
    {
        var contributors = new[]
        {
            typeof(JobPermissionContributor),
            typeof(JobFeatureContributor),
            typeof(JobPolicyContributor),
            typeof(JobObservabilityContributor),
            typeof(JobsDatabaseContributor)
        };

        foreach (var contributor in contributors)
        {
            contributor.GetConstructor(Type.EmptyTypes).Should().NotBeNull(
                $"{contributor.Name} is constructed by the kernel before the container exists");
        }
    }

    [Fact]
    public void The_module_declares_one_route_prefix()
    {
        JobsModuleMetadata.RoutePrefix.Should().Be("/api/v1/jobs");
        JobsModuleMetadata.Schema.Should().Be("jobs");
    }

    // ── The seams are seams ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Each extension point is an interface with at least one shipped implementation, so the
    /// seam is exercised rather than merely declared.
    /// </summary>
    [Theory]
    [InlineData(typeof(IJobCategoryContributor))]
    [InlineData(typeof(IJobRankingStrategy))]
    [InlineData(typeof(IJobRecommendationProvider))]
    [InlineData(typeof(IJobSearchEngine))]
    public void Every_seam_has_a_shipped_implementation(Type seam)
    {
        seam.IsInterface.Should().BeTrue();

        var implementations = Types.InAssembly(Module)
            .That().ImplementInterface(seam)
            .GetTypes()
            .ToList();

        implementations.Should().NotBeEmpty(
            $"{seam.Name} must ship with a working implementation, not only a contract");
    }

    /// <summary>
    /// The Extensibility namespace is the published surface a foreign adapter compiles against.
    /// It must not depend on the module's own infrastructure or persistence.
    /// </summary>
    [Fact]
    public void The_extensibility_surface_depends_on_no_infrastructure()
    {
        var result = Types.InAssembly(Module)
            .That().ResideInNamespaceStartingWith("Diaspora.Modules.Jobs.Extensibility")
            .Should()
            .NotHaveDependencyOnAny(
                "Diaspora.Modules.Jobs.Infrastructure",
                "Diaspora.Modules.Jobs.Persistence",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "an adapter compiles against the seam, not against how B9 happens to store things: "
            + string.Join(", ", result.FailingTypeNames ?? []));
    }
}
