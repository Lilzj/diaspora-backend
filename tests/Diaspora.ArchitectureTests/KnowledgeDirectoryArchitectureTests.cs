using System.Reflection;
using Diaspora.Contracts.Authorization;
using Diaspora.Modules.Guides;
using Diaspora.Modules.Guides.Api;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Authorization;
using Diaspora.Modules.Guides.Extensibility.Analytics;
using Diaspora.Modules.Guides.Extensibility.Caching;
using Diaspora.Modules.Guides.Extensibility.Knowledge;
using Diaspora.Modules.Guides.Extensibility.Search;
using Diaspora.Modules.Guides.Integration;
using Diaspora.Modules.Guides.Observability;
using Diaspora.Platform.Modules.Contributors;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The five rules B8.1 exists to hold, enforced rather than documented.
/// <para>
/// The brief names them, and each is the kind of rule that holds until the next author: only the
/// Guides module owns guide content; consumers reach curated knowledge through the directory rather
/// than reimplementing discovery; the directory consumes other verticals through published contracts
/// only; search stays provider-independent; and learning paths are metadata that never duplicate a
/// guide. Three consecutive phases on this platform recommended turning prose rules into build
/// failures before the persistence suite existed, and the cost of not doing it is on the record.
/// </para>
/// </summary>
public class KnowledgeDirectoryArchitectureTests
{
    private static readonly Assembly Module = typeof(GuidesModule).Assembly;

    private const string KnowledgeNamespace = "Diaspora.Modules.Guides.Application.Knowledge";

    private const string KnowledgeDomainPrefix = "Diaspora.Modules.Guides.Domain.";

    // ── Rule 1: only the Guides module owns guide content ───────────────────────────────────

    /// <summary>
    /// <b>The claim of the phase, stated as a boundary.</b> B8.1 curates, organises and presents
    /// guides; it does not own them. The proof is that its aggregates hold identifiers and keys and
    /// not one field of guide content — no title, no body, no word count, no author. The moment one
    /// did, publishing a guide would have to remember to update a second copy of it, and one day it
    /// would not.
    /// </summary>
    [Fact]
    public void No_knowledge_aggregate_stores_guide_content()
    {
        var contentish = new[] { "Title", "Body", "Content", "Summary", "Excerpt", "WordCount", "Slug" };

        var offenders = KnowledgeDomainTypes()
            .SelectMany(type => type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))

            // A property that names a guide and a content-shaped word together would be a cached
            // copy of somebody else's document.
            .Where(property =>
                property.Name.StartsWith("Guide", StringComparison.Ordinal)
                && contentish.Any(word =>
                    property.Name.Contains(word, StringComparison.Ordinal)))
            .Select(property => $"{property.DeclaringType?.Name}.{property.Name}")
            .ToList();

        offenders.Should().BeEmpty(
            "the directory holds guide identifiers and resolves them at read time: " +
            string.Join(", ", offenders));
    }

    /// <summary>
    /// The knowledge domain holds guide identifiers as identifiers or primitives, and never reaches
    /// into the guide aggregate — which is what keeps curation writable without loading, locking or
    /// bumping the revision of a published document.
    /// </summary>
    [Fact]
    public void No_knowledge_aggregate_holds_a_reference_to_the_guide_aggregate()
    {
        var guideAggregate = Module.GetType("Diaspora.Modules.Guides.Domain.Guides.Guide");

        guideAggregate.Should().NotBeNull();

        var offenders = KnowledgeDomainTypes()
            .SelectMany(type => type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(property => property.PropertyType == guideAggregate)
            .Select(property => $"{property.DeclaringType?.Name}.{property.Name}")
            .ToList();

        offenders.Should().BeEmpty(
            "curating a guide must not require loading it: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// The knowledge domain is testable without a database, a web host or a mediator — the same
    /// promise B8's domain makes.
    /// </summary>
    [Fact]
    public void The_knowledge_domain_depends_on_no_framework()
    {
        var result = Types.InAssembly(Module)
            .That().ResideInNamespaceStartingWith("Diaspora.Modules.Guides.Domain.Knowledge")
            .Or().ResideInNamespaceStartingWith("Diaspora.Modules.Guides.Domain.Directories")
            .Or().ResideInNamespaceStartingWith("Diaspora.Modules.Guides.Domain.LearningPaths")
            .Or().ResideInNamespaceStartingWith("Diaspora.Modules.Guides.Domain.Recommendations")
            .Or().ResideInNamespaceStartingWith("Diaspora.Modules.Guides.Domain.CountryContext")
            .Or().ResideInNamespaceStartingWith("Diaspora.Modules.Guides.Domain.KnowledgeAnalytics")
            .Should()
            .NotHaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "MediatR",
                "FluentValidation")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the domain must be testable without infrastructure: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    // ── Rule 2: consumers reach knowledge through the directory ─────────────────────────────

    /// <summary>
    /// <b>The interface that is the whole point of the phase.</b> B11 and B12 each arrived at the
    /// same requirement independently, and the answer was going to be a fourth, fifth and sixth
    /// private implementation of guide discovery. There is one now, it is published, and it answers
    /// only from reader-visible state.
    /// </summary>
    [Fact]
    public void The_consumer_contract_exists_and_is_reachable_from_another_assembly()
    {
        typeof(IKnowledgeDirectory).IsInterface.Should().BeTrue();
        typeof(IKnowledgeDirectory).IsPublic.Should().BeTrue();

        typeof(IKnowledgeDirectory).IsAssignableFrom(typeof(KnowledgeDirectoryService))
            .Should().BeTrue();
    }

    /// <summary>
    /// A consuming vertical must be able to read a country summary without referencing B8.1's
    /// domain. The moment one of these carried a value object, every consumer would be coupled to
    /// this module's internals rather than to its contract.
    /// </summary>
    [Fact]
    public void The_integration_surface_carries_no_domain_types()
    {
        var result = Types.InAssembly(Module)
            .That().ResideInNamespace("Diaspora.Modules.Guides.Integration")
            .And().AreClasses()
            .Should()
            .NotHaveDependencyOnAny(
                "Diaspora.Modules.Guides.Domain.Knowledge",
                "Diaspora.Modules.Guides.Domain.Directories",
                "Diaspora.Modules.Guides.Domain.LearningPaths",
                "Diaspora.Modules.Guides.Domain.ValueObjects")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the published records must be flat primitives: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// <b>Personalized onboarding is out of scope for this phase, and the signature is where that is
    /// enforced.</b> A method on the consumer contract that accepted a member identifier would be an
    /// invitation to build it behind the interface, where no consumer could see it happening.
    /// </summary>
    [Fact]
    public void The_consumer_contract_offers_nothing_personalized()
    {
        var offenders = typeof(IKnowledgeDirectory)
            .GetMethods()
            .SelectMany(method => method.GetParameters()
                .Where(parameter =>
                    parameter.Name?.Contains("user", StringComparison.OrdinalIgnoreCase) == true
                    || parameter.Name?.Contains("member", StringComparison.OrdinalIgnoreCase) == true
                    || parameter.Name?.Contains("viewer", StringComparison.OrdinalIgnoreCase) == true)
                .Select(parameter => $"{method.Name}({parameter.Name})"))
            .ToList();

        offenders.Should().BeEmpty(
            "AI recommendations and personalized onboarding are explicitly out of scope: " +
            string.Join(", ", offenders));
    }

    // ── Rule 3: other verticals are consumed through published contracts only ───────────────

    /// <summary>
    /// <b>The rule that keeps the dependency direction upright.</b> Jobs, Housing, Marketplace and
    /// Community Life consume the Knowledge Directory. A reference the other way would invert that
    /// and make every one of them queue behind a Guides release — so the contextual country counters
    /// are maintained entirely from published integration contracts in <c>Diaspora.Contracts</c>,
    /// and this test is what stops the first person to want a title from adding a project reference.
    /// </summary>
    [Theory]
    [InlineData("Diaspora.Modules.Jobs")]
    [InlineData("Diaspora.Modules.Housing")]
    [InlineData("Diaspora.Modules.Marketplace")]
    [InlineData("Diaspora.Modules.CommunityLife")]
    public void The_directory_consumes_a_vertical_through_its_contracts_and_never_its_module(
        string vertical)
    {
        var result = Types.InAssembly(Module)
            .ShouldNot()
            .HaveDependencyOn(vertical)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "those modules consume the Knowledge Directory, never the other way round: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// The counterpart: the contracts really are consumed, so the rule above cannot pass by the
    /// integration having been deleted.
    /// </summary>
    [Fact]
    public void The_cross_vertical_projection_really_subscribes_to_published_contracts()
    {
        var handlers = Module.GetTypes()
            .Where(type => type.Namespace == $"{KnowledgeNamespace}.EventHandlers")
            .Where(type => type.Name.EndsWith("ContextHandler", StringComparison.Ordinal))
            .ToList();

        handlers.Should().HaveCountGreaterThanOrEqualTo(
            5, "jobs, housing, marketplace, events, organizations and providers all report a country");

        foreach (var handler in handlers)
        {
            var contract = handler.BaseType?.GetGenericArguments().SingleOrDefault();

            contract.Should().NotBeNull("{0} must consume exactly one contract", handler.Name);

            contract!.Assembly.GetName().Name.Should().Be(
                "Diaspora.Contracts",
                "{0} must consume a published contract, not a module type", handler.Name);
        }
    }

    /// <summary>
    /// The projection holds counts and nothing that could be mistaken for content. A row with a
    /// title would be a cache of somebody else's data, rendered without their visibility rules,
    /// moderation state or freshness.
    /// </summary>
    [Fact]
    public void The_cross_vertical_projection_stores_counts_and_not_content()
    {
        var counter = Module.GetType(
            "Diaspora.Modules.Guides.Domain.CountryContext.CountryContextCounter");

        counter.Should().NotBeNull();

        counter!.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .Should().BeEquivalentTo(["Kind", "Count", "UpdatedAt"]);
    }

    // ── Rule 4: search stays provider-independent ───────────────────────────────────────────

    /// <summary>
    /// <b>The brief's success criterion is that a future OpenSearch integration is an infrastructure
    /// change and nothing more.</b> That is only true while the port speaks primitives: a request
    /// carrying an expression, a delegate or a domain object could only ever be answered in process.
    /// </summary>
    [Fact]
    public void The_search_port_speaks_primitives_a_query_dsl_could_be_built_from()
    {
        var allowed = new[]
        {
            typeof(string), typeof(int), typeof(bool), typeof(Guid),
            typeof(int?), typeof(bool?), typeof(double)
        };

        var offenders = typeof(KnowledgeSearchRequest)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property =>
            {
                var type = property.PropertyType;

                if (allowed.Contains(type))
                {
                    return false;
                }

                // A read-only list of an allowed scalar is still a translatable filter.
                return !(type.IsGenericType
                    && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)
                    && allowed.Contains(type.GetGenericArguments()[0]));
            })
            .Select(property => $"{property.Name} ({property.PropertyType.Name})")
            .ToList();

        offenders.Should().BeEmpty(
            "an engine adapter must be able to translate every field into a query DSL: " +
            string.Join(", ", offenders));
    }

    /// <summary>
    /// Hits carry an identifier and a score. An engine returning titles would be returning its own
    /// copy of them — and an external index is exactly where that copy is most likely to be a day
    /// old.
    /// </summary>
    [Fact]
    public void A_search_hit_carries_an_identifier_and_never_a_document()
    {
        typeof(KnowledgeSearchHit)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .Should().BeEquivalentTo(["GuideId", "Score"]);
    }

    /// <summary>
    /// No query handler, endpoint or response contract may name the shipped engine. The moment one
    /// does, replacing it stops being a registration change.
    /// </summary>
    [Fact]
    public void Nothing_above_the_port_names_the_shipped_engine()
    {
        var engine = Module.GetType(
            "Diaspora.Modules.Guides.Infrastructure.Search.EfKnowledgeSearchEngine");

        engine.Should().NotBeNull();

        var result = Types.InAssembly(Module)
            .That().ResideInNamespaceStartingWith(KnowledgeNamespace)
            .Or().ResideInNamespaceStartingWith("Diaspora.Modules.Guides.Api")
            .Or().ResideInNamespaceStartingWith("Diaspora.Modules.Guides.Contracts")
            .Should()
            .NotHaveDependencyOn("Diaspora.Modules.Guides.Infrastructure.Search")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the engine is reached through IKnowledgeSearchEngine and nothing else: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// B8.1 reuses B8's ranking seam rather than declaring a second one. A platform with two answers
    /// to "what does most helpful mean" has no answer.
    /// </summary>
    [Fact]
    public void The_directory_declares_no_second_ranking_seam()
    {
        var offenders = Module.GetTypes()
            .Where(type => type.IsInterface)
            .Where(type =>
                type.Name.Contains("Knowledge", StringComparison.Ordinal)
                && type.Name.Contains("Ranking", StringComparison.Ordinal))
            .Select(type => type.Name)
            .ToList();

        offenders.Should().BeEmpty(
            "ordering within a shelf is IGuideRankingStrategy, reused: " +
            string.Join(", ", offenders));
    }

    // ── Rule 5: learning paths are metadata, never content ─────────────────────────────────

    /// <summary>
    /// A step carries its own framing sentence and an identifier. Copying the guide's title would
    /// make the path silently rewrite itself when an author retitles — and would make the path a
    /// second, stale copy of the thing it points at.
    /// </summary>
    [Fact]
    public void A_learning_path_step_holds_its_own_framing_and_an_identifier()
    {
        var step = Module.GetType(
            "Diaspora.Modules.Guides.Domain.LearningPaths.LearningPathStep");

        step.Should().NotBeNull();

        step!.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .Should().BeEquivalentTo(
            [
                "StepId", "Position", "Title", "Description",
                "GuideId", "KnowledgeCategoryKey", "IsOptional"
            ]);

        step.GetProperty("GuideId")!.PropertyType.Should().Be<Guid?>(
            "a step names a guide; it does not embed one");
    }

    [Fact]
    public void A_curated_shelf_row_holds_an_identifier_a_position_and_a_note()
    {
        var featured = Module.GetType(
            "Diaspora.Modules.Guides.Domain.Directories.CountryFeaturedGuide");

        featured.Should().NotBeNull();

        featured!.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .Should().BeEquivalentTo(
            [
                "FeatureId", "GuideId", "KnowledgeCategoryKey", "Position", "Note", "FeaturedAt"
            ]);
    }

    // ── Composition ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Every_knowledge_contributor_seam_the_brief_names_exists_and_is_public()
    {
        var seams = new[]
        {
            typeof(IKnowledgeCategoryContributor),
            typeof(ICountryDirectoryTemplateContributor),
            typeof(IKnowledgeSectionProvider),
            typeof(ICountryContextContributor),
            typeof(IKnowledgeSearchEngine),
            typeof(IKnowledgeCache),
            typeof(IKnowledgeAnalyticsSink)
        };

        foreach (var seam in seams)
        {
            seam.IsInterface.Should().BeTrue("{0} must be a contributor seam", seam.Name);
            seam.IsPublic.Should().BeTrue("{0} must be reachable from another assembly", seam.Name);
        }

        typeof(KnowledgeSectionProviderRegistry).IsPublic.Should().BeTrue();
        typeof(CountryContextContributorRegistry).IsPublic.Should().BeTrue();
    }

    [Fact]
    public void The_directory_registers_through_the_platform_contributor_model()
    {
        typeof(IPermissionContributor)
            .IsAssignableFrom(typeof(KnowledgePermissionContributor)).Should().BeTrue();

        typeof(IPolicyContributor)
            .IsAssignableFrom(typeof(KnowledgePolicyContributor)).Should().BeTrue();

        typeof(IFeatureContributor)
            .IsAssignableFrom(typeof(KnowledgeFeatureContributor)).Should().BeTrue();

        typeof(IObservabilityContributor)
            .IsAssignableFrom(typeof(KnowledgeObservabilityContributor)).Should().BeTrue();

        typeof(IEndpointContributor)
            .IsAssignableFrom(typeof(KnowledgeEndpointContributor)).Should().BeTrue();
    }

    [Fact]
    public void Every_knowledge_contributor_is_sealed_and_constructible_before_the_container_exists()
    {
        var contributors = new[]
        {
            typeof(KnowledgePermissionContributor),
            typeof(KnowledgePolicyContributor),
            typeof(KnowledgeFeatureContributor),
            typeof(KnowledgeObservabilityContributor),
            typeof(KnowledgeEndpointContributor)
        };

        foreach (var contributor in contributors)
        {
            contributor.IsSealed.Should().BeTrue("{0} must be sealed", contributor.Name);

            contributor.GetConstructor(Type.EmptyTypes).Should().NotBeNull(
                "{0} is built during host construction, before the container exists",
                contributor.Name);
        }
    }

    // ── Authorization ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void Every_knowledge_permission_and_gate_is_namespaced_to_this_module()
    {
        foreach (var permission in new KnowledgePermissionContributor().GetPermissions())
        {
            permission.Key.Should().StartWith("guides:knowledge:");
            permission.Module.Should().Be(GuidesModuleMetadata.Name);
        }

        foreach (var feature in new KnowledgeFeatureContributor().GetFeatures())
        {
            feature.Name.Should().StartWith("guides.knowledge.");
            feature.Module.Should().Be(GuidesModuleMetadata.Name);
        }

        foreach (var policy in new KnowledgePolicyContributor().GetPolicies())
        {
            policy.Name.Should().StartWith("guides.knowledge.");
            policy.Module.Should().Be(GuidesModuleMetadata.Name);
        }
    }

    [Fact]
    public void Every_knowledge_policy_names_a_permission_this_module_declares()
    {
        var permissions = new KnowledgePermissionContributor()
            .GetPermissions()
            .Select(permission => permission.Key)
            .ToHashSet(StringComparer.Ordinal);

        var policies = new KnowledgePolicyContributor().GetPolicies().ToList();

        policies.Should().NotBeEmpty();

        foreach (var policy in policies.Where(policy => policy.Permission is not null))
        {
            permissions.Should().Contain(
                policy.Permission!,
                "policy '{0}' names a permission that must exist", policy.Name);
        }
    }

    /// <summary>
    /// <b>The sharpest application of B2.0.1 anywhere on the platform.</b> A tier measures
    /// progression and a role grants privilege — and no amount of progression should let a member
    /// rearrange the page every newcomer to a country sees. Curation is editorial authority over what
    /// the platform tells people, and it is granted, never earned.
    /// </summary>
    [Fact]
    public void Every_curation_permission_is_role_scoped_and_none_is_a_progression()
    {
        var permissions = new KnowledgePermissionContributor().GetPermissions().ToList();

        permissions.Should().NotBeEmpty();

        permissions.Should().OnlyContain(
            permission => permission.Roles != null && permission.Roles.Count > 0,
            "curation is granted by a role, never reached by verifying a phone number");

        permissions.Should().OnlyContain(
            permission => permission.MinimumTier == AccessTier.Limited,
            "a floor above Limited would imply verifying unlocks curation; below it would grant " +
            "the gate to phone-unverified accounts");
    }

    [Fact]
    public void Both_knowledge_gates_are_staff_only()
    {
        new KnowledgeFeatureContributor().GetFeatures()
            .Should().OnlyContain(
                feature => feature.StaffOnly && feature.MinimumTier >= AccessTier.Limited);
    }

    [Fact]
    public void The_knowledge_permission_keys_do_not_collide_with_the_guide_ones()
    {
        var guides = new GuidePermissionContributor()
            .GetPermissions()
            .Select(permission => permission.Key)
            .ToHashSet(StringComparer.Ordinal);

        new KnowledgePermissionContributor().GetPermissions()
            .Should().OnlyContain(
                permission => !guides.Contains(permission.Key),
                "the permission catalogue refuses two contributors declaring one key, and the " +
                "resulting startup failure takes down every request that resolves it");
    }

    // ── Observability ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_declared_telemetry_source_has_real_instruments_behind_it()
    {
        var contributor = new KnowledgeObservabilityContributor();

        contributor.ActivitySources.Should().Contain(KnowledgeTelemetry.SourceName);
        contributor.Meters.Should().Contain(KnowledgeTelemetry.SourceName);

        KnowledgeTelemetry.ActivitySource.Name.Should().Be(KnowledgeTelemetry.SourceName);
        KnowledgeTelemetry.Meter.Name.Should().Be(KnowledgeTelemetry.SourceName);

        KnowledgeTelemetry.SourceName.Should().NotBe(
            GuideTelemetry.SourceName,
            "discovery is high-volume and authoring is not; a deployment samples them differently");
    }

    private static IReadOnlyList<Type> KnowledgeDomainTypes() =>
    [
        .. Module.GetTypes().Where(type =>
            type.Namespace is { } space
            && space.StartsWith(KnowledgeDomainPrefix, StringComparison.Ordinal)
            && (space.EndsWith(".Knowledge", StringComparison.Ordinal)
                || space.EndsWith(".Directories", StringComparison.Ordinal)
                || space.EndsWith(".LearningPaths", StringComparison.Ordinal)
                || space.EndsWith(".Recommendations", StringComparison.Ordinal)
                || space.EndsWith(".CountryContext", StringComparison.Ordinal)
                || space.EndsWith(".KnowledgeAnalytics", StringComparison.Ordinal)))
    ];
}
