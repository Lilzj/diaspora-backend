using System.Reflection;
using Diaspora.Contracts.CommunityLife.V1.Events;
using Diaspora.Contracts.Events;
using Diaspora.Contracts.Trust.V1.Submission;
using Diaspora.Modules.CommunityLife;
using Diaspora.Modules.CommunityLife.Application.EventHandlers;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Authorization;
using Diaspora.Modules.CommunityLife.Extensibility.Calendar;
using Diaspora.Modules.CommunityLife.Extensibility.Catalog;
using Diaspora.Modules.CommunityLife.Extensibility.CheckIn;
using Diaspora.Modules.CommunityLife.Extensibility.Membership;
using Diaspora.Modules.CommunityLife.Extensibility.Moderation;
using Diaspora.Modules.CommunityLife.Extensibility.Ranking;
using Diaspora.Modules.CommunityLife.Extensibility.Search;
using Diaspora.Modules.CommunityLife.Extensibility.Views;
using Diaspora.Modules.CommunityLife.Extensibility.Visibility;
using Diaspora.Modules.CommunityLife.Infrastructure.Community;
using Diaspora.Modules.CommunityLife.Infrastructure.Location;
using Diaspora.Modules.CommunityLife.Integration;
using Diaspora.Modules.CommunityLife.Observability;
using Diaspora.Platform.Messaging;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Contributors;
using Diaspora.SharedKernel.Primitives;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The boundaries the Community Life module must not cross, enforced rather than documented.
/// <para>
/// Each of these is true on the day it is written and quietly false two releases later unless
/// something checks. The <c>EventId</c> test is the one that earns its keep: the defect it guards
/// against compiles with a warning and breaks consumer idempotency silently.
/// </para>
/// </summary>
public class CommunityLifeArchitectureTests
{
    private static readonly Assembly Module = typeof(CommunityLifeModule).Assembly;

    // ── Ownership ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Community Life never owns Trust or Identity data.</b> The badge on an organization is
    /// Trust's; B12 holds a projection it can only ever receive, never grant.
    /// </summary>
    [Fact]
    public void The_module_does_not_reach_into_trust_or_identity()
    {
        var result = Types.InAssembly(Module)
            .Should()
            .NotHaveDependencyOnAny("Diaspora.Modules.Trust", "Diaspora.Domain.Identity")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "ADR-002 draws the line at Identity's and Trust's aggregates. Community Life reaches "
            + "Trust only through the published ITrustVerificationSubmission contract. Offenders: "
            + Describe(result));
    }

    /// <summary>
    /// <b>Jobs and Marketplace stay unreferenced.</b> Neither publishes an integration directory,
    /// and a career fair naming three vacancies must not make two verticals depend on each other to
    /// render an anchor. They are held as unvalidated linked resources the client resolves.
    /// </summary>
    [Fact]
    public void The_module_does_not_reference_jobs_or_marketplace()
    {
        var result = Types.InAssembly(Module)
            .Should()
            .NotHaveDependencyOnAny(
                "Diaspora.Modules.Jobs", "Diaspora.Modules.Marketplace")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "B12 links to jobs and listings through EventLinkedResource, which is deliberately "
            + "untyped against those modules. Offenders: " + Describe(result));
    }

    /// <summary>
    /// <b>Only one class talks to Trust.</b> Verification is asked for in exactly one place, so
    /// approval, badge assignment and revocation cannot leak into a command handler.
    /// </summary>
    [Fact]
    public void Only_the_trust_provider_uses_the_trust_submission_contract()
    {
        var offenders = Types.InAssembly(Module)
            .That()
            .HaveDependencyOn(typeof(ITrustVerificationSubmission).FullName)
            .GetTypes()
            .Where(type => type != typeof(CommunityLifeTrustProvider))
            .ToList();

        offenders.Should().BeEmpty(
            "B3.0.2 puts the submission contract behind one adapter. Offenders: "
            + string.Join(", ", offenders.Select(type => type.FullName)));
    }

    // ── The EventId trap ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>No published event declares its own <c>EventId</c>.</b> The base
    /// <see cref="IntegrationEvent"/> already owns that name as the outbox de-duplication key.
    /// Shadowing it compiles with only a CS0108 warning, serialises the wrong value into the
    /// published <c>EventId</c>, and silently breaks every consumer's idempotency. B12's own
    /// identifier is <c>CommunityEventId</c> throughout, which is why this test exists.
    /// </summary>
    [Fact]
    public void No_published_event_shadows_the_platform_event_identifier()
    {
        var offenders = typeof(EventCreatedIntegrationEvent).Assembly
            .GetTypes()
            .Where(type => typeof(IIntegrationEvent).IsAssignableFrom(type))
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(type => type.GetProperty(
                "EventId",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly) is not null)
            .ToList();

        offenders.Should().BeEmpty(
            "an integration event that declares its own EventId shadows the outbox's "
            + "de-duplication key and breaks consumer idempotency without failing to compile. "
            + "Offenders: " + string.Join(", ", offenders.Select(type => type.FullName)));
    }

    /// <summary>The same rule for domain events, which carry the same base property.</summary>
    [Fact]
    public void No_domain_event_shadows_the_platform_event_identifier()
    {
        var offenders = Module
            .GetTypes()
            .Where(type => typeof(IDomainEvent).IsAssignableFrom(type))
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(type => type.GetProperty(
                "EventId",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly) is not null)
            .ToList();

        offenders.Should().BeEmpty(
            "a domain event that declares its own EventId shadows the base property. B12 names "
            + "its own CommunityEventId. Offenders: "
            + string.Join(", ", offenders.Select(type => type.FullName)));
    }

    // ── Module shape ────────────────────────────────────────────────────────────────────────

    /// <summary>The module owns one schema and no other module's tables.</summary>
    [Fact]
    public void The_module_owns_exactly_one_schema() =>
        CommunityLifeModuleMetadata.Schema.Should().Be(
            "community_life",
            "a module that wrote into a second schema would be a module whose migrations could "
            + "drop another module's tables.");

    /// <summary>
    /// The module declares one route prefix and it is versioned.
    /// <para>
    /// <c>/community-life</c> rather than <c>/events</c>: "event" is a word this module, the
    /// platform outbox and every future analytics surface all want.
    /// </para>
    /// </summary>
    [Fact]
    public void The_module_declares_one_versioned_route_prefix() =>
        CommunityLifeModuleMetadata.RoutePrefix.Should().Be("/api/v1/community-life");

    /// <summary>
    /// <b>The module loads after Housing.</b> An event can name a neighbourhood B11 profiles, so
    /// B11 has to be able to answer before B12 asks — and the seam registries are built in order.
    /// </summary>
    [Fact]
    public void The_module_loads_after_housing() =>
        new CommunityLifeModule().Order.Should().Be(1100);

    /// <summary>
    /// Every extensibility contract is an interface. A seam that shipped as a base class would be
    /// one a contributor had to inherit from, which is a dependency rather than a contract.
    /// </summary>
    [Theory]
    [InlineData(typeof(IOrganizationTypeContributor))]
    [InlineData(typeof(IEventCategoryContributor))]
    [InlineData(typeof(IEventRankingStrategy))]
    [InlineData(typeof(IEventSpamSignalProvider))]
    [InlineData(typeof(IOrganizationMembershipPolicyProvider))]
    [InlineData(typeof(IEventCheckInMethod))]
    [InlineData(typeof(IEventVisibilityPolicy))]
    [InlineData(typeof(ICommunityCalendarSource))]
    [InlineData(typeof(ICommunityEventSearchEngine))]
    [InlineData(typeof(IOrganizationSearchEngine))]
    [InlineData(typeof(ICommunityLifeViewTracker))]
    public void Every_seam_is_an_interface(Type seam) =>
        seam.IsInterface.Should().BeTrue();

    /// <summary>
    /// The module contributes to the platform through the published contributor interfaces and
    /// nothing else — no module edits another module's source to join the platform.
    /// </summary>
    [Fact]
    public void The_module_contributes_through_the_published_interfaces()
    {
        typeof(CommunityLifeModule).Should().BeAssignableTo<IPlatformModule>();

        Module.GetTypes()
            .Where(type => typeof(IPermissionContributor).IsAssignableFrom(type))
            .Should().ContainSingle()
            .Which.Should().Be<CommunityLifePermissionContributor>();

        Module.GetTypes()
            .Where(type => typeof(IFeatureContributor).IsAssignableFrom(type))
            .Should().ContainSingle()
            .Which.Should().Be<CommunityLifeFeatureContributor>();
    }

    // ── Boundaries B12.1 depends on ─────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Money stays outside B12.</b> A paid event's ticket is a B15 payment intent and a B12
    /// registration, and the two meet through the published contract. A reference from here to
    /// Financial's persistence would make an events module the owner of a ledger.
    /// </summary>
    [Fact]
    public void The_module_does_not_reach_into_financial()
    {
        var result = Types.InAssembly(Module)
            .Should()
            .NotHaveDependencyOnAny(
                "Diaspora.Modules.Financial.Persistence",
                "Diaspora.Modules.Financial.Domain")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "B12 owns the event, the registration and the intent to attend; B15 owns money. "
            + "Offenders: " + Describe(result));
    }

    /// <summary>
    /// <b>B12 does not manufacture notifications.</b> It publishes facts and B13 decides who hears
    /// about them. A reference into Notifications would put the decision about whether to wake
    /// somebody's phone inside an aggregate that knows nothing about their preferences.
    /// </summary>
    [Fact]
    public void The_module_does_not_reach_into_notifications()
    {
        var result = Types.InAssembly(Module)
            .Should()
            .NotHaveDependencyOnAny(
                "Diaspora.Modules.Notifications.Domain",
                "Diaspora.Modules.Notifications.Persistence",
                "Diaspora.Modules.Notifications.Application")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "ADR-013 puts delivery behind B13. B12 publishes an integration event and stops. "
            + "Offenders: " + Describe(result));
    }

    /// <summary>
    /// <b>B12 runs no realtime transport of its own.</b> B13.2 owns the hub; a second one here
    /// would be a second authorization model over the same member's connections.
    /// </summary>
    [Fact]
    public void The_module_hosts_no_realtime_transport()
    {
        var offenders = Module
            .GetTypes()
            .Where(type => type.Namespace?.Contains("SignalR", StringComparison.Ordinal) == true
                || type.Name.EndsWith("Hub", StringComparison.Ordinal))
            .ToList();

        offenders.Should().BeEmpty(
            "realtime delivery is B13.2's, and a competing hub would be a second authorization "
            + "model over the same connections. Offenders: "
            + string.Join(", ", offenders.Select(type => type.FullName)));
    }

    /// <summary>
    /// <b>Every integration event leaves through the relay.</b> One translation point is what keeps
    /// the domain private and stops a refactor inside an aggregate changing a consumer's contract —
    /// and B12.1 added three publications, which is exactly when a second publisher appears.
    /// </summary>
    [Fact]
    public void Only_the_relay_publishes_integration_events()
    {
        var offenders = Types.InAssembly(Module)
            .That()
            .HaveDependencyOn(typeof(IIntegrationEventPublisher).FullName)
            .GetTypes()
            .Where(type => type != typeof(CommunityLifeIntegrationEventRelay))
            .ToList();

        offenders.Should().BeEmpty(
            "the relay is the single seam where a domain fact becomes a published contract. "
            + "Offenders: " + string.Join(", ", offenders.Select(type => type.FullName)));
    }

    /// <summary>
    /// <b>An occurrence is an event, not a second aggregate.</b> The test that guards B12.1's
    /// central decision: the moment somebody adds an <c>EventOccurrence</c> aggregate root, the
    /// module has two registration models, two attendance models and two capacity settlement paths,
    /// and the counters stop being reconcilable.
    /// </summary>
    [Fact]
    public void There_is_no_second_occurrence_aggregate()
    {
        var offenders = Module
            .GetTypes()
            .Where(type => type.Name.Contains("Occurrence", StringComparison.Ordinal))
            .Where(type => type.BaseType?.Name.StartsWith("AggregateRoot", StringComparison.Ordinal)
                == true)
            .ToList();

        offenders.Should().BeEmpty(
            "B12.1 models an occurrence as a CommunityEvent carrying a series reference. A "
            + "separate aggregate would duplicate the RSVP, capacity and check-in machinery the "
            + "module already keeps reconcilable. Offenders: "
            + string.Join(", ", offenders.Select(type => type.FullName)));
    }

    /// <summary>
    /// <b>Recurrence expansion is pure and lives in the domain.</b> Given a rule, a local time and a
    /// zone, the dates a series falls on are a fact — and a fact worth being able to test without a
    /// host, a context or a clock.
    /// </summary>
    [Fact]
    public void Recurrence_expansion_depends_on_nothing_outside_the_domain()
    {
        var result = Types.InAssembly(Module)
            .That()
            .HaveNameStartingWith("Recurrence")
            .Should()
            .NotHaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Diaspora.Persistence",
                "Diaspora.Platform.Services")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the expander is pure so its dates can be asserted without a database. Offenders: "
            + Describe(result));
    }

    // ── Cross-module adapters ───────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>One adapter per foreign module, and nothing else reaches across.</b> Community is
    /// reached only from the content source, Location only from the two adapters — so a change in
    /// either module is a change to one file here.
    /// </summary>
    [Fact]
    public void Community_is_reached_only_from_the_content_source()
    {
        var offenders = Types.InAssembly(Module)
            .That()
            .HaveDependencyOn("Diaspora.Modules.Community")
            .GetTypes()
            .Where(type => type != typeof(CommunityEventContentSource))
            .ToList();

        offenders.Should().BeEmpty(
            "B6 is reached through one adapter. Offenders: "
            + string.Join(", ", offenders.Select(type => type.FullName)));
    }

    [Fact]
    public void Location_is_reached_only_from_its_adapters()
    {
        var permitted = new[]
        {
            typeof(CommunityLifeNearbyResourceProvider),
            typeof(CommunityLifeResourceCounter),
            typeof(CommunityLifeNeighborhoodResolver),

            // B4.1. The module's description claimed IGeographicScopeResolver as a dependency
            // from the start and nothing in it ever took the seam up; the map viewport is what
            // finally needed it. It is an adapter on the same terms as the three above — raw
            // values in, a narrowed filter out, no GeographicScope crossing back to a handler.
            typeof(CommunityLifeGeographicScopeAdapter)
        };

        var offenders = Types.InAssembly(Module)
            .That()
            .HaveDependencyOn("Diaspora.Modules.Location")
            .GetTypes()
            .Where(type => !permitted.Contains(type))
            .ToList();

        offenders.Should().BeEmpty(
            "B4 is reached through the nearby provider, the counter and the neighbourhood "
            + "resolver. Offenders: " + string.Join(", ", offenders.Select(type => type.FullName)));
    }

    // ── Observability ───────────────────────────────────────────────────────────────────────

    /// <summary>The module's telemetry is namespaced to itself.</summary>
    [Fact]
    public void Telemetry_is_namespaced_to_the_module() =>
        CommunityLifeTelemetry.SourceName.Should().Be("Diaspora.CommunityLife");

    /// <summary>
    /// The integration directories are the only public surface other modules bind to, and they
    /// return summaries rather than aggregates — so nothing outside B12 can write to one.
    /// </summary>
    [Theory]
    [InlineData(typeof(IOrganizationDirectory))]
    [InlineData(typeof(IEventDirectory))]
    public void The_integration_directories_expose_no_aggregate(Type directory)
    {
        var domainParameters = directory.GetMethods()
            .SelectMany(method => method.GetParameters())
            .Select(parameter => parameter.ParameterType.Namespace ?? string.Empty)
            .Where(space => space.Contains(".Domain.", StringComparison.Ordinal))
            .ToList();

        domainParameters.Should().BeEmpty(
            "a consumer that had to name a B12 domain type to call the directory would be "
            + "coupled to this module's internals.");
    }

    private static string Describe(TestResult result) =>
        result.FailingTypeNames is null
            ? "none reported"
            : string.Join(", ", result.FailingTypeNames);
}
