using System.Reflection;
using Diaspora.Contracts.Trust.V1.Submission;
using Diaspora.Modules.Marketplace;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Authorization;
using Diaspora.Modules.Marketplace.Extensibility.Attributes;
using Diaspora.Modules.Marketplace.Extensibility.Categories;
using Diaspora.Modules.Marketplace.Extensibility.Conversations;
using Diaspora.Modules.Marketplace.Extensibility.Fraud;
using Diaspora.Modules.Marketplace.Extensibility.Ranking;
using Diaspora.Modules.Marketplace.Extensibility.Search;
using Diaspora.Modules.Marketplace.Extensibility.Views;
using Diaspora.Modules.Marketplace.Infrastructure.Community;
using Diaspora.Modules.Marketplace.Infrastructure.Location;
using Diaspora.Modules.Marketplace.Infrastructure.Trust;
using Diaspora.Modules.Marketplace.Observability;
using Diaspora.Modules.Marketplace.Persistence;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Contributors;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;
using Diaspora.Modules.Media.Extensibility.Access;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The boundaries the Marketplace module must not cross, enforced rather than documented.
/// <para>
/// The brief names five explicitly — Marketplace never owns Trust data, Media assets, Community
/// interactions or Location entities, and communicates through published contracts and abstractions
/// only. Each is a test below, and each is the kind of rule that is true on the day it is written
/// and quietly false two releases later unless something checks.
/// </para>
/// </summary>
public class MarketplaceArchitectureTests
{
    private static readonly Assembly Module = typeof(MarketplaceModule).Assembly;

    // ── The five ownership rules from the brief ─────────────────────────────────────────────

    /// <summary>
    /// <b>Marketplace never owns Trust data.</b> The badge on a seller is Trust's; B10 holds a
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
            "ADR-002 draws the line at Identity's and Trust's aggregates. Marketplace reaches "
            + "Identity through ICurrentUser and Trust through Diaspora.Contracts: "
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
        var dependencies = typeof(MarketplaceTrustVerificationProvider)
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
    /// <b>Marketplace never owns Media.</b> Every photograph is a <c>MediaId</c>; the module holds
    /// no stream, no byte array and no stored URL, and exactly one class knows how to call B5.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The rule is about direction, not about a count.</b> It used to assert that exactly one
    /// type in the assembly depended on Media at all, which conflated two opposite things: a class
    /// that <em>calls</em> Media, and a class Media <em>calls</em>. The first is the coupling
    /// ADR-005 exists to contain. The second is an extension point Media publishes and instructs
    /// verticals to implement in their own assembly — refusing it would mean the seam could never
    /// be used by the modules it was built for.
    /// </para>
    /// <para>
    /// So implementations of <see cref="IMediaAccessPolicy"/> are excluded and everything else
    /// still has to be the one claimer. A new class reaching into Media fails exactly as before;
    /// the exemption cannot be borrowed, because implementing the interface means Media is
    /// driving.
    /// </para>
    /// </remarks>
    [Fact]
    public void Only_one_class_calls_into_media()
    {
        var callers = Types.InAssembly(Module)
            .That().HaveDependencyOn("Diaspora.Modules.Media")
            .GetTypes()
            .Where(type => !typeof(IMediaAccessPolicy).IsAssignableFrom(type))
            .Select(type => type.Name)
            .ToList();

        callers.Should().BeEquivalentTo(
            [nameof(MarketplaceMediaClaimer)],
            "ADR-005's rule is attach-by-reference, and containing the knowledge in one class is "
            + "what makes it checkable");
    }

    /// <summary>
    /// The listing media policy is registered for marketplace listings and nothing else.
    /// </summary>
    /// <remarks>
    /// Media resolves one policy per owner type and refuses two claiming the same one. A policy
    /// answering for the wrong type would decide access for another vertical's files — which is
    /// why this is asserted rather than left to the registry to discover at startup.
    /// </remarks>
    [Fact]
    public void The_listing_media_policy_speaks_only_for_marketplace_listings()
    {
        var policies = Types.InAssembly(Module)
            .That().ImplementInterface(typeof(IMediaAccessPolicy))
            .GetTypes()
            .ToList();

        policies.Should().ContainSingle(
            "one vertical, one owner type, one policy")
            .Which.Should().Be(typeof(MarketplaceListingMediaPolicy));
    }

    /// <summary>
    /// <b>Marketplace never owns Location.</b> Geography is <em>asked</em> through B4's published
    /// scope resolver by exactly one adapter.
    /// <para>
    /// As with the provider rule, this is about the <em>call seam</em> rather than the published
    /// DTO. Only one class may hold <c>IGeographicScopeResolver</c> and know how to ask B4 a
    /// question; the handlers that receive the resolved <c>GeographicScope</c> and pass it to the
    /// filter are doing exactly what a published consumption contract is for. Asserting on any
    /// dependency at all would forbid the thing the contract exists to allow.
    /// </para>
    /// </summary>
    [Fact]
    public void Only_one_class_asks_location_to_resolve_geography()
    {
        var askers = Types.InAssembly(Module)
            .GetTypes()
            .Where(type => type.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Any(parameter =>
                    parameter.ParameterType.Name == "IGeographicScopeResolver"))
            .Select(type => type.Name)
            .ToList();

        askers.Should().BeEquivalentTo(
            [nameof(MarketplaceGeographicScopeAdapter)],
            "B10 parses no radius, resolves no city name and computes no distance");
    }

    /// <summary>
    /// The other half of the Location rule: B10 never reaches into B4's aggregates.
    /// <para>
    /// The two contributions back into Location's own seams — the nearby provider and the resource
    /// counter — name B4's published value objects because <c>ILocationResourceCounter</c>'s
    /// signature requires them. That is B4's Extensibility surface, which is exactly what a vertical
    /// is meant to consume; its <c>Domain.Areas</c> aggregates are not.
    /// </para>
    /// </summary>
    [Fact]
    public void The_module_does_not_reach_into_location_aggregates()
    {
        var result = Types.InAssembly(Module)
            .Should()
            .NotHaveDependencyOnAny(
                "Diaspora.Modules.Location.Domain.Areas",
                "Diaspora.Modules.Location.Domain.Countries",
                "Diaspora.Modules.Location.Persistence")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "a vertical consumes B4's Integration and Extensibility contracts, never its "
            + "aggregates or its tables: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// <b>Marketplace never owns Community interactions.</b> Comments, reactions and content
    /// reports on a listing are B6's, reached by registering one content source.
    /// </summary>
    [Fact]
    public void Only_one_class_talks_to_community()
    {
        var talkers = Types.InAssembly(Module)
            .That().HaveDependencyOn("Diaspora.Modules.Community")
            .GetTypes()
            .Select(type => type.Name)
            .ToList();

        talkers.Should().BeEquivalentTo(
            [nameof(MarketplaceContentSource)],
            "B10 wrote no social code: it registers a content source and B6 does the rest");
    }

    /// <summary>
    /// <b>Marketplace never owns Provider data.</b> A storefront stores an identifier; the name,
    /// badge and rating are B7's answers at read time.
    /// <para>
    /// The rule is about the <em>call seam</em>, not about the published DTO. Exactly one class may
    /// hold <c>IProviderDirectory</c> and know how to ask B7 a question; the mapper and the read
    /// handler legitimately pass the returned <c>ProviderSummary</c> around, which is what a
    /// published consumption contract is for.
    /// </para>
    /// </summary>
    [Fact]
    public void Only_one_class_asks_the_provider_directory()
    {
        var askers = Types.InAssembly(Module)
            .GetTypes()
            .Where(type => type.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Any(parameter =>
                    parameter.ParameterType.Name == "IProviderDirectory"))
            .Select(type => type.Name)
            .ToList();

        askers.Should().BeEquivalentTo(
            [nameof(MarketplaceProviderResolver)],
            "a listing stores an identifier and B7 answers for the shop");
    }

    /// <summary>
    /// <b>Marketplace is a sibling of Jobs, not a consumer of it.</b> A reference between two
    /// verticals would be the first edge in the graph B2.6 exists to keep acyclic.
    /// </summary>
    [Fact]
    public void The_module_does_not_reach_into_a_sibling_vertical()
    {
        var result = Types.InAssembly(Module)
            .Should()
            .NotHaveDependencyOnAny("Diaspora.Modules.Jobs", "Diaspora.Modules.Bookmarks")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "verticals compose through the kernel and through published contracts, never through "
            + "each other: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    // ── Composition ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The module registers entirely through B2.6 contributor interfaces. Every one the kernel
    /// scans for is present, so nothing about the module has to be named in the host.
    /// </summary>
    [Fact]
    public void Every_contributor_the_kernel_scans_for_is_present()
    {
        var types = Types.InAssembly(Module).GetTypes().ToList();

        types.Should().Contain(typeof(MarketplacePermissionContributor));
        types.Should().Contain(typeof(MarketplaceFeatureContributor));
        types.Should().Contain(typeof(MarketplacePolicyContributor));
        types.Should().Contain(typeof(MarketplaceDatabaseContributor));
        types.Should().Contain(typeof(MarketplaceObservabilityContributor));

        typeof(MarketplaceModule).Should().BeAssignableTo<IPlatformModule>();
    }

    /// <summary>
    /// <b>Contributors are constructed by the kernel before the container exists.</b> A contributor
    /// with a dependency is one the kernel cannot build, and the failure is a startup crash rather
    /// than a compile error — which is why it is checked here.
    /// </summary>
    [Fact]
    public void Every_contributor_has_a_parameterless_constructor()
    {
        var contributors = Types.InAssembly(Module)
            .That().ImplementInterface(typeof(IPermissionContributor))
            .Or().ImplementInterface(typeof(IFeatureContributor))
            .Or().ImplementInterface(typeof(IPolicyContributor))
            .GetTypes();

        foreach (var contributor in contributors)
        {
            contributor.GetConstructor(Type.EmptyTypes).Should().NotBeNull(
                $"{contributor.Name} is constructed before the container exists");
        }
    }

    /// <summary>
    /// The module declares one route prefix. A second would be a second public URL space nobody
    /// versioned.
    /// </summary>
    [Fact]
    public void The_module_declares_exactly_one_route_prefix()
    {
        MarketplaceModuleMetadata.RoutePrefix.Should().Be("/api/v1/marketplace");

        typeof(MarketplaceModuleMetadata)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Count(field => field.Name.Contains("RoutePrefix", StringComparison.Ordinal))
            .Should().Be(1);
    }

    // ── Extensibility ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The seven seams the module's architectural claim rests on.</b> Each is an interface in
    /// the Extensibility surface, which is what makes "a new ranking is a new class" literally true
    /// — and what makes the brief's "future OpenSearch integration must require infrastructure
    /// changes only" a checkable statement rather than an aspiration.
    /// </summary>
    [Fact]
    public void The_seven_extension_seams_are_interfaces_in_the_extensibility_surface()
    {
        foreach (var seam in new[]
        {
            typeof(IListingCategoryContributor),
            typeof(IListingAttributeContributor),
            typeof(IMarketplaceSearchEngine),
            typeof(IListingRankingStrategy),
            typeof(IListingFraudSignalProvider),
            typeof(IMarketplaceConversationProvider),
            typeof(IListingViewTracker)
        })
        {
            seam.IsInterface.Should().BeTrue($"{seam.Name} is a seam, not a base class");

            seam.Namespace.Should().StartWith(
                "Diaspora.Modules.Marketplace.Extensibility",
                $"{seam.Name} is part of the published extension surface");
        }
    }

    /// <summary>
    /// <b>The search seam answers with identifiers, never with documents.</b> That is what keeps a
    /// stale index from leaking a listing whose visibility, price or stock changed after it was
    /// indexed — the read path re-applies the audience rules to whatever comes back.
    /// </summary>
    [Fact]
    public void The_search_engine_answers_with_identifiers()
    {
        var hit = typeof(MarketplaceSearchHit);

        hit.GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo(["ListingId", "Score"],
                "a hit is an identifier and a score; the card is hydrated from the write model");
    }

    /// <summary>
    /// The search document carries flat primitives only. An adapter for OpenSearch must be
    /// writable without referencing this module's domain, which is the whole of the
    /// engine-independence claim.
    /// </summary>
    [Fact]
    public void The_search_document_carries_no_domain_types()
    {
        var domainTypes = typeof(MarketplaceSearchDocument)
            .GetProperties()
            .Select(property => property.PropertyType)
            .Where(type => type.Namespace?.StartsWith(
                "Diaspora.Modules.Marketplace.Domain", StringComparison.Ordinal) == true)
            .Select(type => type.Name)
            .ToList();

        domainTypes.Should().BeEmpty(
            "an index document is flat primitives, so an adapter needs no reference to B10's "
            + "domain");
    }
}
