using System.Reflection;
using Diaspora.Contracts.Authorization;
using Diaspora.Modules.Guides;
using Diaspora.Modules.Guides.Api;
using Diaspora.Modules.Guides.Authorization;
using Diaspora.Modules.Guides.Extensibility.Analytics;
using Diaspora.Modules.Guides.Extensibility.Caching;
using Diaspora.Modules.Guides.Extensibility.Intelligence;
using Diaspora.Modules.Guides.Extensibility.Search;
using Diaspora.Modules.Guides.Observability;
using Diaspora.Persistence.Modules;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Contributors;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

/// <summary>
/// The seven rules B8.2 exists to hold, enforced rather than documented.
/// <para>
/// The brief names each of them: country intelligence stays inside the Guides bounded context; guide
/// and knowledge ownership is not duplicated; official notices are consumed only through published
/// contracts; the Communication Platform creates notifications and this module does not; the
/// Recommendation Platform consumes published contracts only; embassies are not modelled as
/// Providers; and search stays provider-independent. Every one is the kind of rule that holds until
/// the next author, and three consecutive phases on this platform recommended turning prose rules
/// into build failures before the persistence suite existed.
/// </para>
/// </summary>
namespace Diaspora.ArchitectureTests;

public class CountryIntelligenceArchitectureTests
{
    private static readonly Assembly Module = typeof(GuidesModule).Assembly;

    private const string IntelligenceNamespace =
        "Diaspora.Modules.Guides.Application.Intelligence";

    private const string DomainPrefix = "Diaspora.Modules.Guides.Domain.";

    // ── Rule 1: no new bounded context ──────────────────────────────────────────────────────

    /// <summary>
    /// <b>The claim of the phase, stated as a count.</b> Country intelligence introduces no new
    /// bounded context: it is a phase of the Guides module, registered from inside it, sharing its
    /// schema, its order and its <c>IPlatformModule</c>. A second module would have needed an order
    /// slot, a database contributor and its own answer to who owns what a member needs to know about
    /// a country — which is the duplication the brief exists to prevent.
    /// </summary>
    [Fact]
    public void Country_intelligence_ships_inside_the_guides_module_and_declares_no_second_one()
    {
        var modules = Module.GetTypes()
            .Where(type => typeof(IPlatformModule).IsAssignableFrom(type))
            .Where(type => type is { IsInterface: false, IsAbstract: false })
            .ToList();

        modules.Should().ContainSingle(
            "one assembly, one platform module — B8, B8.1 and B8.2 are phases, not modules");

        modules[0].Should().Be<GuidesModule>();
    }

    /// <summary>
    /// One database contributor, so one schema and one owner. A second would make the boundary
    /// between what a community knows and what a government said a deployment concern.
    /// </summary>
    [Fact]
    public void The_module_declares_exactly_one_database_contributor()
    {
        Module.GetTypes()
            .Where(type => typeof(IDatabaseContributor).IsAssignableFrom(type))
            .Where(type => type is { IsInterface: false, IsAbstract: false })
            .Should().ContainSingle();
    }

    /// <summary>
    /// Every intelligence table is <c>intel_</c>-prefixed. SQLite ignores schemas entirely, so two
    /// modules whose tables differ only by schema collide there and nowhere else — which is the worst
    /// possible place for a name clash to first appear. <c>categories</c>, <c>authorities</c> and
    /// <c>holidays</c> are all names a later module will want.
    /// </summary>
    [Fact]
    public void Every_intelligence_table_is_prefixed_so_it_cannot_collide_on_sqlite()
    {
        var constants = Module.GetType(
            "Diaspora.Modules.Guides.Persistence.Configurations.IntelligencePersistenceConstants");

        constants.Should().NotBeNull();

        var tables = constants!
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.Name.EndsWith("Table", StringComparison.Ordinal))
            .Select(field => (string)field.GetValue(null)!)
            .ToList();

        tables.Should().HaveCountGreaterThanOrEqualTo(20);

        tables.Should().OnlyContain(
            table => table.StartsWith("intel_", StringComparison.Ordinal),
            "an unprefixed table collides with another module's on SQLite and nowhere else");
    }

    // ── Rule 2: guide and knowledge ownership is not duplicated ─────────────────────────────

    /// <summary>
    /// <b>Country intelligence references guides; it never holds one.</b> A notice points at a guide
    /// through an identifier and a kind, and the read path resolves it through B8's own contract with
    /// B8's visibility rules. The moment an intelligence aggregate held a guide title, publishing a
    /// guide would have to remember to update a second copy of it — and one day it would not.
    /// </summary>
    [Fact]
    public void No_intelligence_aggregate_stores_guide_or_knowledge_content()
    {
        var contentish = new[] { "Title", "Body", "Content", "Summary", "Excerpt", "Slug" };

        var offenders = IntelligenceDomainTypes()
            .SelectMany(type => type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(property =>
                (property.Name.StartsWith("Guide", StringComparison.Ordinal)
                    || property.Name.StartsWith("LearningPath", StringComparison.Ordinal)
                    || property.Name.StartsWith("Knowledge", StringComparison.Ordinal))
                && contentish.Any(word => property.Name.Contains(word, StringComparison.Ordinal)))
            .Select(property => $"{property.DeclaringType?.Name}.{property.Name}")
            .ToList();

        offenders.Should().BeEmpty(
            "intelligence holds identifiers and resolves them at read time: " +
            string.Join(", ", offenders));
    }

    /// <summary>
    /// The intelligence domain never reaches into the guide aggregate, which is what keeps
    /// referencing a guide from a notice free of loading, locking or bumping the revision of a
    /// published document.
    /// </summary>
    [Fact]
    public void No_intelligence_aggregate_holds_a_reference_to_the_guide_aggregate()
    {
        var guide = Module.GetType("Diaspora.Modules.Guides.Domain.Guides.Guide");

        guide.Should().NotBeNull();

        var offenders = IntelligenceDomainTypes()
            .SelectMany(type => type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(property => property.PropertyType == guide)
            .Select(property => $"{property.DeclaringType?.Name}.{property.Name}")
            .ToList();

        offenders.Should().BeEmpty(
            "referencing a guide must not require loading it: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// The intelligence domain is testable without a database, a web host or a mediator — the same
    /// promise B8's and B8.1's domains make.
    /// </summary>
    [Fact]
    public void The_intelligence_domain_depends_on_no_framework()
    {
        var result = Types.InAssembly(Module)
            .That().ResideInNamespaceStartingWith($"{DomainPrefix}Intelligence")
            .Or().ResideInNamespaceStartingWith($"{DomainPrefix}Embassies")
            .Or().ResideInNamespaceStartingWith($"{DomainPrefix}Emergency")
            .Or().ResideInNamespaceStartingWith($"{DomainPrefix}Visas")
            .Or().ResideInNamespaceStartingWith($"{DomainPrefix}Holidays")
            .Or().ResideInNamespaceStartingWith($"{DomainPrefix}IntelligenceAnalytics")
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

    // ── Rule 3: embassies are not Providers ─────────────────────────────────────────────────

    /// <summary>
    /// <b>A mission is managed content about an institution that will never hold an account.</b>
    /// B7's provider is a member's business: it registers itself, an account owns it, customers review
    /// it, and staff can suspend it. Modelling missions as providers would put a claim-this-listing
    /// button on an embassy and invite five-star ratings of a passport office.
    /// </summary>
    [Fact]
    public void An_embassy_has_no_owner_no_rating_and_no_review_count()
    {
        var embassy = Module.GetType("Diaspora.Modules.Guides.Domain.Embassies.Embassy");

        embassy.Should().NotBeNull();

        var forbidden = new[]
        {
            "OwnerId", "OwnerUserId", "PrimaryOwnerId", "Owners",
            "Rating", "AverageRating", "ReviewCount", "Reviews",
            "SubscriptionTier", "IsSuspended", "ClaimedBy"
        };

        var offenders = embassy!
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .Where(name => forbidden.Contains(name, StringComparer.Ordinal))
            .ToList();

        offenders.Should().BeEmpty(
            "an embassy is managed content, not a Provider: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// The counterpart: this module never references the Providers module, so an embassy cannot
    /// quietly become one.
    /// </summary>
    [Fact]
    public void The_module_does_not_reference_the_providers_module()
    {
        var result = Types.InAssembly(Module)
            .That().ResideInNamespaceStartingWith(IntelligenceNamespace)
            .Should()
            .NotHaveDependencyOn("Diaspora.Modules.Providers")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "an embassy is not a provider: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    // ── Rule 4: other modules are reached through published contracts only ──────────────────

    /// <summary>
    /// <b>The rule that keeps the dependency direction upright.</b> Trust, Notifications and
    /// Recommendations are reached through <c>Diaspora.Contracts</c> and nothing else — the whole
    /// Trust integration is one published contract and one stored case identifier, and this test is
    /// what stops the first person who wants a field from adding a project reference.
    /// </summary>
    [Theory]
    [InlineData("Diaspora.Modules.Trust")]
    [InlineData("Diaspora.Modules.Notifications")]
    [InlineData("Diaspora.Modules.Recommendations")]
    [InlineData("Diaspora.Modules.Jobs")]
    [InlineData("Diaspora.Modules.Housing")]
    [InlineData("Diaspora.Modules.Marketplace")]
    [InlineData("Diaspora.Modules.CommunityLife")]
    public void Intelligence_consumes_a_module_through_its_contracts_and_never_its_assembly(
        string module)
    {
        var result = Types.InAssembly(Module)
            .ShouldNot()
            .HaveDependencyOn(module)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the integration is a published contract, not a reference: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// The counterpart: the Trust integration really is subscribed, so the rule above cannot pass by
    /// the integration having been deleted.
    /// <para>
    /// <b>The subscription is B8's, and B8.2 fans out from it rather than adding its own — which is
    /// a correction the platform's own guardrail forced.</b> A module may consume one published
    /// contract exactly once; a second handler for <c>VerificationApproved</c> would be two
    /// subscriptions racing over one delivery, and
    /// <c>DomainEventSafetyArchitectureTests.No_module_consumes_one_integration_event_twice</c>
    /// fails the build on it. So the four Trust handlers this module already had now call both
    /// projectors, and each ignores what is not its own — both correlate on the case identifier they
    /// stored at submission.
    /// </para>
    /// </summary>
    [Fact]
    public void The_trust_projection_is_reached_from_the_module_s_single_subscription()
    {
        var projector = Module.GetType(
            $"{IntelligenceNamespace}.EventHandlers.AuthorityVerificationProjector");

        projector.Should().NotBeNull();

        // Selected by the contract they consume rather than by their name, and narrowed to the four
        // that carry a verification <em>outcome</em>. This module consumes other Trust contracts —
        // a badge award moves an author's standing and has nothing to say about an institution — and
        // demanding those reach the authority projector would be asserting something untrue.
        var outcomes = new[]
        {
            "VerificationApprovedIntegrationEvent",
            "VerificationRejectedIntegrationEvent",
            "VerificationRevokedIntegrationEvent",
            "VerificationExpiredIntegrationEvent"
        };

        var handlers = Module.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => type.BaseType?.IsGenericType == true)
            .Where(type =>
            {
                var contract = type.BaseType!.GetGenericArguments().SingleOrDefault();

                return contract?.Namespace == "Diaspora.Contracts.Trust.V1.Events"
                    && outcomes.Contains(contract.Name, StringComparer.Ordinal);
            })
            .ToList();

        handlers.Should().HaveCount(
            4, "approved, rejected, revoked and expired all move a badge, and each is consumed once");

        foreach (var handler in handlers)
        {
            var contract = handler.BaseType!.GetGenericArguments().Single();

            contract.Assembly.GetName().Name.Should().Be(
                "Diaspora.Contracts",
                "{0} must consume a published contract, not a module type", handler.Name);

            handler.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Select(parameter => parameter.ParameterType)
                .Should().Contain(
                    projector!,
                    "{0} must reach the authority projector rather than a second subscription " +
                    "being added beside it",
                    handler.Name);
        }
    }

    /// <summary>
    /// The rule that forced the shape above, asserted here too so B8.2's own suite explains it: no
    /// Trust contract is consumed by two handlers in this module.
    /// </summary>
    [Fact]
    public void No_trust_contract_is_consumed_twice_by_this_module()
    {
        var duplicates = Module.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => type.BaseType?.IsGenericType == true)
            .Select(type => new
            {
                Handler = type,
                Contract = type.BaseType!.GetGenericArguments().SingleOrDefault()
            })
            .Where(entry => entry.Contract?.Namespace == "Diaspora.Contracts.Trust.V1.Events")
            .GroupBy(entry => entry.Contract!)
            .Where(group => group.Count() > 1)
            .Select(group =>
                $"{group.Key.Name}: {string.Join(", ", group.Select(entry => entry.Handler.Name))}")
            .ToList();

        duplicates.Should().BeEmpty(
            "two handlers for one delivery race each other: " + string.Join("; ", duplicates));
    }

    /// <summary>
    /// <b>Communication creates notifications; this module publishes facts.</b> The brief is explicit
    /// and the platform's analyzer pack agrees — B8.2 publishes severity, category and country on a
    /// contract, and B13 decides recipients, channels, timing, localization and quiet hours. Nothing
    /// here names a notification.
    /// </summary>
    [Fact]
    public void Nothing_in_intelligence_creates_a_notification()
    {
        var offenders = Module.GetTypes()
            .Where(type => type.Namespace?.StartsWith(
                IntelligenceNamespace, StringComparison.Ordinal) == true)
            .Where(type =>
                type.Name.Contains("Notification", StringComparison.Ordinal)
                || type.Name.Contains("Push", StringComparison.Ordinal)
                || type.Name.Contains("Digest", StringComparison.Ordinal))
            .Select(type => type.Name)
            .ToList();

        offenders.Should().BeEmpty(
            "B13 determines recipients, channels and timing from published events: " +
            string.Join(", ", offenders));
    }

    /// <summary>
    /// <b>An official notice has no recipient, and that absence is deliberate.</b> B13.1 established
    /// that every recipient identity on a published contract is a nullable <c>Guid?</c> — and a
    /// notice's audience is "people who care about this country", which only B13 can resolve.
    /// Inventing a recipient field would be that phase's <c>Guid.Empty</c> defect with an audience
    /// instead of a person.
    /// </summary>
    [Fact]
    public void No_published_intelligence_contract_carries_a_recipient()
    {
        var contracts = typeof(Contracts.Intelligence.V1.Events
                .OfficialNoticePublishedIntegrationEvent).Assembly
            .GetTypes()
            .Where(type => type.Namespace == "Diaspora.Contracts.Intelligence.V1.Events")
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .ToList();

        contracts.Should().HaveCountGreaterThanOrEqualTo(10);

        var offenders = contracts
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(property =>
                property.Name.Contains("Recipient", StringComparison.Ordinal)
                || property.Name.EndsWith("UserId", StringComparison.Ordinal))
            .Select(property => $"{property.DeclaringType?.Name}.{property.Name}")
            .ToList();

        offenders.Should().BeEmpty(
            "a notice is broadcast; only B13 can resolve its audience: " +
            string.Join(", ", offenders));
    }

    /// <summary>
    /// Every published contract is a flat record of primitives, so a consumer can parse it without
    /// referencing this module.
    /// </summary>
    [Fact]
    public void Every_published_intelligence_contract_is_flat_primitives()
    {
        var allowed = new[]
        {
            typeof(string), typeof(int), typeof(bool), typeof(Guid), typeof(Guid?),
            typeof(bool?), typeof(int?), typeof(DateTimeOffset), typeof(DateTimeOffset?)
        };

        var offenders = typeof(Contracts.Intelligence.V1.Events
                .OfficialNoticePublishedIntegrationEvent).Assembly
            .GetTypes()
            .Where(type => type.Namespace == "Diaspora.Contracts.Intelligence.V1.Events")
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .SelectMany(type => type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(property => !allowed.Contains(property.PropertyType))
            .Select(property => $"{property.DeclaringType?.Name}.{property.Name}")
            .ToList();

        offenders.Should().BeEmpty(
            "a consumer must be able to parse a contract without this module: " +
            string.Join(", ", offenders));
    }

    // ── Rule 5: search stays provider-independent ───────────────────────────────────────────

    /// <summary>
    /// <b>The brief's success criterion is that a future OpenSearch integration is an infrastructure
    /// change and nothing more.</b> That is only true while the port speaks primitives: a request
    /// carrying an expression, a delegate or a domain object could only ever be answered in process.
    /// Dates cross as strings for the same reason — a query DSL is JSON, and a
    /// <c>DateTimeOffset</c> is the one common type two engines will disagree about serialising.
    /// </summary>
    [Fact]
    public void The_notice_search_port_speaks_primitives_a_query_dsl_could_be_built_from()
    {
        var allowed = new[]
        {
            typeof(string), typeof(int), typeof(bool), typeof(Guid),
            typeof(int?), typeof(bool?), typeof(double)
        };

        var offenders = typeof(NoticeSearchRequest)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property =>
            {
                var type = property.PropertyType;

                if (allowed.Contains(type))
                {
                    return false;
                }

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
    /// A hit carries an identifier and a score. An engine returning titles would be returning its own
    /// copy of them — and on official information a stale copy is worse than none.
    /// </summary>
    [Fact]
    public void A_notice_search_hit_carries_an_identifier_and_never_a_document()
    {
        typeof(NoticeSearchHit)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .Should().BeEquivalentTo(["OfficialNoticeId", "Score"]);
    }

    /// <summary>
    /// No query handler, endpoint or response contract may name the shipped engine. The moment one
    /// does, replacing it stops being a registration change.
    /// </summary>
    [Fact]
    public void Nothing_above_the_notice_search_port_names_the_shipped_engine()
    {
        var engine = Module.GetType(
            "Diaspora.Modules.Guides.Infrastructure.Search.EfOfficialNoticeSearchEngine");

        engine.Should().NotBeNull();

        var result = Types.InAssembly(Module)
            .That().ResideInNamespaceStartingWith(IntelligenceNamespace)
            .Or().ResideInNamespaceStartingWith("Diaspora.Modules.Guides.Api")
            .Or().ResideInNamespaceStartingWith("Diaspora.Modules.Guides.Contracts")
            .Should()
            .NotHaveDependencyOn("Diaspora.Modules.Guides.Infrastructure.Search")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the engine is reached through IOfficialNoticeSearchEngine and nothing else: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// <b>B8.2 reuses the cache seam rather than declaring a second one</b>, on the reasoning B8.1
    /// uses for the ranking seam. A deployment configuring Redis should do it once for the module,
    /// not once per phase.
    /// </summary>
    [Fact]
    public void Intelligence_declares_no_second_cache_seam()
    {
        var offenders = Module.GetTypes()
            .Where(type => type.IsInterface)
            .Where(type =>
                type.Name.Contains("Intelligence", StringComparison.Ordinal)
                && type.Name.Contains("Cache", StringComparison.Ordinal))
            .Select(type => type.Name)
            .ToList();

        offenders.Should().BeEmpty(
            "caching is IKnowledgeCache, reused: " + string.Join(", ", offenders));
    }

    // ── Rule 6: analytics never becomes a behavioural log ───────────────────────────────────

    /// <summary>
    /// <b>The signal stream includes people opening domestic-violence and mental-health lines.</b> A
    /// shape that could carry a member identifier would make a behavioural log of that possible, and
    /// the only reliable way to guarantee it is never built is to leave nowhere for it to go.
    /// </summary>
    [Fact]
    public void No_analytics_signal_or_counter_can_carry_a_reader_identity()
    {
        var types = new[]
        {
            typeof(IntelligenceAnalyticsSignal),
            Module.GetType(
                "Diaspora.Modules.Guides.Domain.IntelligenceAnalytics.IntelligenceCounter")!,
            Module.GetType(
                "Diaspora.Modules.Guides.Domain.IntelligenceAnalytics.CountryIntelligenceAnalytics")!
        };

        var offenders = types
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(property =>
                property.Name.Contains("User", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Member", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Viewer", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Session", StringComparison.OrdinalIgnoreCase))
            .Select(property => $"{property.DeclaringType?.Name}.{property.Name}")
            .ToList();

        offenders.Should().BeEmpty(
            "these numbers answer whether advisories were seen, never by whom: " +
            string.Join(", ", offenders));
    }

    // ── Composition ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Every_intelligence_seam_the_brief_names_exists_and_is_public()
    {
        var seams = new[]
        {
            typeof(IOfficialNoticeCategoryContributor),
            typeof(IEmergencyContactKindContributor),
            typeof(IIntelligenceSourceValidator),
            typeof(IAuthorityVerificationProvider),
            typeof(IOfficialNoticeSearchEngine),
            typeof(IIntelligenceAnalyticsSink)
        };

        foreach (var seam in seams)
        {
            seam.IsInterface.Should().BeTrue("{0} must be a contributor seam", seam.Name);
            seam.IsPublic.Should().BeTrue("{0} must be reachable from another assembly", seam.Name);
        }

        typeof(AuthorityVerificationProviderRegistry).IsPublic.Should().BeTrue();

        // Reused rather than redeclared.
        typeof(IKnowledgeCache).IsPublic.Should().BeTrue();
    }

    [Fact]
    public void Intelligence_registers_through_the_platform_contributor_model()
    {
        typeof(IPermissionContributor)
            .IsAssignableFrom(typeof(IntelligencePermissionContributor)).Should().BeTrue();

        typeof(IPolicyContributor)
            .IsAssignableFrom(typeof(IntelligencePolicyContributor)).Should().BeTrue();

        typeof(IFeatureContributor)
            .IsAssignableFrom(typeof(IntelligenceFeatureContributor)).Should().BeTrue();

        typeof(IObservabilityContributor)
            .IsAssignableFrom(typeof(IntelligenceObservabilityContributor)).Should().BeTrue();

        typeof(IEndpointContributor)
            .IsAssignableFrom(typeof(IntelligenceEndpointContributor)).Should().BeTrue();
    }

    [Fact]
    public void Every_intelligence_contributor_is_sealed_and_constructible_before_the_container()
    {
        var contributors = new[]
        {
            typeof(IntelligencePermissionContributor),
            typeof(IntelligencePolicyContributor),
            typeof(IntelligenceFeatureContributor),
            typeof(IntelligenceObservabilityContributor),
            typeof(IntelligenceEndpointContributor)
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
    public void Every_intelligence_permission_and_gate_is_namespaced_to_this_module()
    {
        foreach (var permission in new IntelligencePermissionContributor().GetPermissions())
        {
            permission.Key.Should().StartWith("guides:intelligence:");
            permission.Module.Should().Be(GuidesModuleMetadata.Name);
        }

        foreach (var feature in new IntelligenceFeatureContributor().GetFeatures())
        {
            feature.Name.Should().StartWith("guides.intelligence.");
            feature.Module.Should().Be(GuidesModuleMetadata.Name);
        }

        foreach (var policy in new IntelligencePolicyContributor().GetPolicies())
        {
            policy.Name.Should().StartWith("guides.intelligence.");
            policy.Module.Should().Be(GuidesModuleMetadata.Name);
        }
    }

    [Fact]
    public void Every_intelligence_policy_names_a_permission_this_module_declares()
    {
        var permissions = new IntelligencePermissionContributor()
            .GetPermissions()
            .Select(permission => permission.Key)
            .ToHashSet(StringComparer.Ordinal);

        var policies = new IntelligencePolicyContributor().GetPolicies().ToList();

        policies.Should().NotBeEmpty();

        foreach (var policy in policies.Where(policy => policy.Permission is not null))
        {
            permissions.Should().Contain(
                policy.Permission!,
                "policy '{0}' names a permission that must exist", policy.Name);
        }
    }

    /// <summary>
    /// <b>B2.0.1 at its sharpest.</b> A tier measures progression and a role grants privilege, and no
    /// amount of progression should let a member publish something under a government badge. This is
    /// editorial authority over what the platform presents as official information.
    /// </summary>
    [Fact]
    public void Every_intelligence_permission_is_role_scoped_and_none_is_a_progression()
    {
        var permissions = new IntelligencePermissionContributor().GetPermissions().ToList();

        permissions.Should().NotBeEmpty();

        permissions.Should().OnlyContain(
            permission => permission.Roles != null && permission.Roles.Count > 0,
            "publishing official information is granted by a role, never earned by verifying a phone");

        permissions.Should().OnlyContain(
            permission => permission.MinimumTier == AccessTier.Limited,
            "a floor above Limited would imply verifying unlocks the editorial desk; below it " +
            "would grant the gate to phone-unverified accounts");
    }

    /// <summary>
    /// <b>The separation of duty, asserted.</b> Writing is held by moderators; verifying and
    /// publishing are administrators only — because a workflow where the person who transcribes a
    /// circular also approves and publishes it has no verification step at all.
    /// </summary>
    [Fact]
    public void Verifying_and_publishing_are_narrower_grants_than_writing()
    {
        var permissions = new IntelligencePermissionContributor()
            .GetPermissions()
            .ToDictionary(permission => permission.Key, StringComparer.Ordinal);

        var writers = permissions[IntelligencePermissions.ManageNotices].Roles!;
        var verifiers = permissions[IntelligencePermissions.VerifyNotices].Roles!;
        var publishers = permissions[IntelligencePermissions.PublishNotices].Roles!;

        verifiers.Should().BeSubsetOf(writers);
        publishers.Should().BeSubsetOf(writers);

        verifiers.Should().NotBeEquivalentTo(
            writers, "anybody who can write must not automatically be able to approve");
    }

    [Fact]
    public void All_three_intelligence_gates_are_staff_only()
    {
        new IntelligenceFeatureContributor().GetFeatures()
            .Should().OnlyContain(
                feature => feature.StaffOnly && feature.MinimumTier >= AccessTier.Limited);
    }

    [Fact]
    public void The_intelligence_permission_keys_collide_with_neither_earlier_phase()
    {
        var earlier = new GuidePermissionContributor()
            .GetPermissions()
            .Concat(new KnowledgePermissionContributor().GetPermissions())
            .Select(permission => permission.Key)
            .ToHashSet(StringComparer.Ordinal);

        new IntelligencePermissionContributor().GetPermissions()
            .Should().OnlyContain(
                permission => !earlier.Contains(permission.Key),
                "the permission catalogue refuses two contributors declaring one key, and the " +
                "resulting startup failure takes down every request that resolves it");
    }

    // ── URL space and observability ─────────────────────────────────────────────────────────

    /// <summary>
    /// Three declared prefixes on one module are fine; one undeclared one is not. The invariant is
    /// that every prefix in the assembly is declared in the metadata and is versioned.
    /// </summary>
    [Fact]
    public void The_intelligence_prefix_is_declared_and_versioned()
    {
        GuidesModuleMetadata.IntelligenceRoutePrefix.Should().Be("/api/v1/intelligence");

        GuidesModuleMetadata.IntelligenceRoutePrefix
            .Should().NotBe(GuidesModuleMetadata.RoutePrefix)
            .And.NotBe(GuidesModuleMetadata.KnowledgeRoutePrefix);
    }

    [Fact]
    public void The_declared_telemetry_source_has_real_instruments_behind_it()
    {
        var contributor = new IntelligenceObservabilityContributor();

        contributor.ActivitySources.Should().Contain(IntelligenceTelemetry.SourceName);
        contributor.Meters.Should().Contain(IntelligenceTelemetry.SourceName);

        IntelligenceTelemetry.ActivitySource.Name.Should().Be(IntelligenceTelemetry.SourceName);
        IntelligenceTelemetry.Meter.Name.Should().Be(IntelligenceTelemetry.SourceName);

        IntelligenceTelemetry.SourceName
            .Should().NotBe(GuideTelemetry.SourceName)
            .And.NotBe(KnowledgeTelemetry.SourceName,
                "publication, discovery and authoring are sampled differently");
    }

    private static IReadOnlyList<Type> IntelligenceDomainTypes() =>
    [
        .. Module.GetTypes().Where(type =>
            type.Namespace is { } space
            && space.StartsWith(DomainPrefix, StringComparison.Ordinal)
            && (space.EndsWith(".Intelligence", StringComparison.Ordinal)
                || space.EndsWith(".Embassies", StringComparison.Ordinal)
                || space.EndsWith(".Emergency", StringComparison.Ordinal)
                || space.EndsWith(".Visas", StringComparison.Ordinal)
                || space.EndsWith(".Holidays", StringComparison.Ordinal)
                || space.EndsWith(".IntelligenceAnalytics", StringComparison.Ordinal)))
    ];
}
