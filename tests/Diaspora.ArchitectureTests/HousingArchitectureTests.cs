using System.Reflection;
using Diaspora.Contracts.Housing.V1.Events;
using Diaspora.Contracts.Trust.V1.Submission;
using Diaspora.Modules.Community.Extensibility.Content;
using Diaspora.Modules.Housing;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Authorization;
using Diaspora.Modules.Housing.Domain.Reservations;
using Diaspora.Modules.Housing.Extensibility.Availability;
using Diaspora.Modules.Housing.Extensibility.Catalog;
using Diaspora.Modules.Housing.Extensibility.Fraud;
using Diaspora.Modules.Housing.Extensibility.Neighborhoods;
using Diaspora.Modules.Housing.Extensibility.Privacy;
using Diaspora.Modules.Housing.Extensibility.Ranking;
using Diaspora.Modules.Housing.Extensibility.Search;
using Diaspora.Modules.Housing.Extensibility.Views;
using Diaspora.Modules.Housing.Infrastructure.Community;
using Diaspora.Modules.Housing.Infrastructure.Location;
using Diaspora.Modules.Housing.Infrastructure.Trust;
using Diaspora.Modules.Housing.Integration;
using Diaspora.Modules.Housing.Observability;
using Diaspora.Modules.Housing.Persistence;
using Diaspora.Persistence.Modules;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Contributors;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The boundaries the Housing module must not cross, enforced rather than documented.
/// <para>
/// The B11 brief names six explicitly — Housing never owns Trust data, Marketplace, Community, Jobs
/// or Media, and neighbourhoods stay reusable. Each is a test below, and each is the kind of rule
/// that is true on the day it is written and quietly false two releases later unless something
/// checks.
/// </para>
/// </summary>
public class HousingArchitectureTests
{
    private static readonly Assembly Module = typeof(HousingModule).Assembly;

    // ── The ownership rules from the brief ──────────────────────────────────────────────────

    /// <summary>
    /// <b>Housing never owns Trust data.</b> The badge on a landlord is Trust's; B11 holds a
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
            "ADR-002 draws the line at Identity's and Trust's aggregates. Housing reaches "
            + "Identity through ICurrentUser and Trust through Diaspora.Contracts: "
            + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// <b>Housing never owns Marketplace or Jobs.</b> They are sibling verticals, and a reference
    /// to either would be the first edge in the graph B2.6 exists to keep acyclic.
    /// <para>
    /// "Nearby jobs" and "neighbourhood marketplace" are real requirements in the B11 brief, and
    /// both are answered through Location's resource registry that Jobs and Marketplace already
    /// contribute to — which is why this test can pass and the features can still exist.
    /// </para>
    /// </summary>
    [Fact]
    public void The_module_does_not_reference_its_sibling_verticals()
    {
        var result = Types.InAssembly(Module)
            .Should()
            .NotHaveDependencyOnAny("Diaspora.Modules.Marketplace", "Diaspora.Modules.Jobs")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Jobs and Marketplace are siblings; neighbourhood jobs and neighbourhood marketplace "
            + "come through Location's INearbyResourceProvider registry instead: "
            + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// <b>Housing never owns Media.</b> It stores identifiers and roles; the bytes, the variants,
    /// the stripped EXIF and the signed URL are B5's.
    /// <para>
    /// Stripped EXIF is not incidental here: a photograph taken inside a flat carries that flat's
    /// coordinates, and a housing platform storing originals would defeat its own address privacy
    /// with the first gallery image.
    /// </para>
    /// </summary>
    [Fact]
    public void No_housing_domain_type_stores_media_bytes_or_a_media_url()
    {
        var properties = Module.GetTypes()
            .Where(type => type.Namespace?.StartsWith(
                "Diaspora.Modules.Housing.Domain", StringComparison.Ordinal) == true)
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .ToList();

        var offenders = properties
            .Where(property =>
                property.PropertyType == typeof(byte[])
                || property.PropertyType == typeof(Stream)
                || (property.Name.EndsWith("Url", StringComparison.Ordinal)
                    && property.DeclaringType!.Name.Contains("Media", StringComparison.Ordinal)))
            .Select(property => $"{property.DeclaringType!.Name}.{property.Name}")
            .Distinct()
            .ToList();

        offenders.Should().BeEmpty(
            "Media owns the bytes and the URL; Housing stores an identifier and a role. "
            + "A landlord's own website is a different thing and stays here");
    }

    /// <summary>
    /// <b>Housing never owns Community interactions.</b> Comments, reactions, reports and bookmarks
    /// on a property are B6's, reached by registering one content source.
    /// </summary>
    [Fact]
    public void Community_is_reached_only_through_the_published_content_source()
    {
        typeof(HousingContentSource).Should().BeAssignableTo<ICommunityContentSource>();

        DeclaredTypesDependingOn("Diaspora.Modules.Community").Should().BeEquivalentTo(
            [nameof(HousingContentSource)],
            "exactly one class in B11 knows Community exists");
    }

    /// <summary>
    /// <b>Neighbourhoods remain reusable.</b> The published contract is a narrow interface plus a
    /// flat record, so a consumer binds to neither a Housing aggregate nor a Housing identifier.
    /// </summary>
    [Fact]
    public void The_published_neighborhood_contract_exposes_no_other_housing_type()
    {
        var surface = typeof(INeighborhoodDirectory)
            .GetMethods()
            .SelectMany(method => method.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .Append(method.ReturnType))
            .Concat(typeof(NeighborhoodSummary)
                .GetProperties()
                .Select(property => property.PropertyType))
            .SelectMany(Unwrap)
            .Where(type => type.Namespace?.StartsWith(
                "Diaspora.Modules.Housing", StringComparison.Ordinal) == true)
            .Select(type => type.FullName!)
            .Distinct()
            .ToList();

        surface.Should().BeEquivalentTo(
            [typeof(NeighborhoodSummary).FullName],
            "a consumer references the directory and the summary and nothing else, which is what "
            + "makes a neighbourhood a shared platform concept rather than a Housing one");
    }

    /// <summary>
    /// <b>Housing owns no geography.</b> The neighbourhood itself is B4's and B11 keeps a profile
    /// keyed on it, so a Location type may appear where the two are joined up and nowhere else.
    /// <para>
    /// The rule is drawn by layer rather than by class list because the layer is what actually
    /// matters: a <c>NeighborhoodProfile</c> holding a Location <c>Area</c> instead of an
    /// <c>AreaId</c> is the change that would quietly make neighbourhoods Housing's property, and
    /// it is a change no naming convention would catch.
    /// </para>
    /// </summary>
    [Fact]
    public void Geography_never_reaches_the_domain_or_the_wire()
    {
        var offenders = Types.InAssembly(Module)
            .That().HaveDependencyOn("Diaspora.Modules.Location")
            .GetTypes()
            .Select(type => type.DeclaringType ?? type)
            .Where(type => type.Namespace is not null
                && (type.Namespace.StartsWith(
                        "Diaspora.Modules.Housing.Domain", StringComparison.Ordinal)
                    || type.Namespace.StartsWith(
                        "Diaspora.Modules.Housing.Contracts", StringComparison.Ordinal)
                    || type.Namespace.StartsWith(
                        "Diaspora.Modules.Housing.Integration", StringComparison.Ordinal)))
            .Select(type => type.FullName!)
            .Distinct()
            .ToList();

        offenders.Should().BeEmpty(
            "the aggregates key on an AreaId and the wire speaks in identifiers, so Location is "
            + "joined up in the application and infrastructure layers only");
    }

    /// <summary>
    /// And where geography <em>is</em> reached, it is reached from a countable set of adapters —
    /// two of which are the search handlers, which hold a resolved scope rather than resolving one.
    /// </summary>
    [Fact]
    public void Location_is_reached_from_a_named_handful_of_classes()
    {
        DeclaredTypesDependingOn("Diaspora.Modules.Location").Should().BeEquivalentTo(
            [
                nameof(HousingGeographicScopeAdapter),
                nameof(HousingNeighborhoodResolver),
                nameof(HousingNearbyResourceProvider),
                nameof(HousingResourceCounter),
                nameof(NeighborhoodStatisticsService),
                "SearchHousingQueryHandler",
                "SearchNeighborhoodsQueryHandler"
            ],
            "one adapter resolves a scope and the handlers carry it; nothing else needs B4");
    }

    // ── B3.0.2: the verification contract, not the module ───────────────────────────────────

    /// <summary>
    /// The verification claim really does reach Trust — through the published contract in
    /// <c>Diaspora.Contracts</c> rather than through the module that decides.
    /// </summary>
    [Fact]
    public void Verification_is_submitted_through_the_published_contract()
    {
        typeof(HousingTrustVerificationProvider)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .Should().Contain(typeof(ITrustVerificationSubmission));
    }

    // ── B1.1 / B1.2: messaging ──────────────────────────────────────────────────────────────

    /// <summary>
    /// A module never configures delivery. The outbox is the platform's, and a module that
    /// referenced it would be a module that could bypass it.
    /// </summary>
    [Fact]
    public void The_module_never_touches_the_outbox()
    {
        var result = Types.InAssembly(Module)
            .Should()
            .NotHaveDependencyOnAny(
                "Diaspora.Platform.Messaging.Outbox", "Diaspora.Persistence.Outbox")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "retry, ordering, dead letters and replay are platform concerns: "
            + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// Every published fact lives in <c>Diaspora.Contracts</c> and carries primitives only, so a
    /// consumer parses it without referencing this module.
    /// </summary>
    [Fact]
    public void Published_contracts_carry_no_housing_type()
    {
        var contracts = typeof(HousingPublishedIntegrationEvent)
            .Assembly
            .GetTypes()
            .Where(type => type.Namespace == "Diaspora.Contracts.Housing.V1.Events")
            .ToList();

        // Thirteen from B11, plus the two B23.x Wave 12 added. Both close a hole B11 left rather than
        // widening the module's surface: `HousingUpdated` publishes the changed-field list the domain
        // event has always carried and nothing consumed, and `HousingApplicationStatusChanged`
        // publishes the forward transitions the relay dropped — a seeker could learn a landlord had
        // refused and not that one had agreed. The count is bumped deliberately, so a contract added
        // without a reason still fails here.
        contracts.Should().HaveCount(15, "B11 named thirteen published facts and Wave 12 added two");

        var leaked = contracts
            .SelectMany(type => type.GetProperties())
            .SelectMany(property => Unwrap(property.PropertyType))
            .Where(type => type.Namespace?.StartsWith(
                "Diaspora.Modules.Housing", StringComparison.Ordinal) == true)
            .Select(type => type.FullName!)
            .Distinct()
            .ToList();

        leaked.Should().BeEmpty(
            "an integration event carries flat primitives a consumer can read without referencing "
            + "the module that raised it");
    }

    // ── B2.6: the contributor model ─────────────────────────────────────────────────────────

    /// <summary>Every contributor the kernel scans for is present.</summary>
    [Fact]
    public void The_module_registers_entirely_through_contributors()
    {
        typeof(HousingModule).Should().BeAssignableTo<IPlatformModule>();
        typeof(HousingPermissionContributor).Should().BeAssignableTo<IPermissionContributor>();
        typeof(HousingFeatureContributor).Should().BeAssignableTo<IFeatureContributor>();
        typeof(HousingPolicyContributor).Should().BeAssignableTo<IPolicyContributor>();
        typeof(HousingObservabilityContributor).Should().BeAssignableTo<IObservabilityContributor>();
        typeof(HousingDatabaseContributor).Should().BeAssignableTo<IDatabaseContributor>();
    }

    /// <summary>
    /// The nine seams the module claims. A test rather than a comment, because "extensible" stops
    /// being true the moment one of them is quietly turned into a concrete class.
    /// </summary>
    [Fact]
    public void Every_extension_seam_is_an_interface()
    {
        Type[] seams =
        [
            typeof(IAccommodationTypeContributor),
            typeof(IHousingAmenityContributor),
            typeof(IHousingRankingStrategy),
            typeof(IHousingFraudSignalProvider),
            typeof(IHousingSearchEngine),
            typeof(IHousingViewTracker),
            typeof(INeighborhoodMetricProvider),
            typeof(IAddressDisclosurePolicy),
            typeof(IHousingAvailabilityCalendar)
        ];

        seams.Should().OnlyContain(type => type.IsInterface);
    }

    /// <summary>
    /// <b>B10's outstanding platform recommendation, honoured rather than worked around.</b> The
    /// concurrency mechanism sits behind a port, so the day a guarded atomic counter lands in
    /// <c>Diaspora.Persistence</c> exactly one class in this module changes.
    /// </summary>
    [Fact]
    public void The_reservation_workflow_depends_on_the_guard_abstraction()
    {
        typeof(IHousingUnitReservationGuard).IsInterface.Should().BeTrue();

        Module.GetTypes()
            .Where(type => typeof(IHousingUnitReservationGuard).IsAssignableFrom(type)
                && type is { IsInterface: false, IsAbstract: false })
            .Should().ContainSingle(
                "one adapter today, and one class to change when the platform primitive arrives");
    }

    /// <summary>
    /// The guard is declared in the domain and implemented in persistence — the direction that
    /// makes it a port rather than a helper.
    /// </summary>
    [Fact]
    public void The_reservation_guard_is_declared_in_the_domain_and_implemented_in_persistence()
    {
        typeof(IHousingUnitReservationGuard).Namespace
            .Should().Be("Diaspora.Modules.Housing.Domain.Reservations");

        Module.GetTypes()
            .Single(type => typeof(IHousingUnitReservationGuard).IsAssignableFrom(type)
                && type is { IsInterface: false, IsAbstract: false })
            .Namespace!
            .Should().StartWith("Diaspora.Modules.Housing.Persistence");
    }

    // ── Route and telemetry surface ─────────────────────────────────────────────────────────

    /// <summary>
    /// One declared prefix, and it is not <c>/listings</c> — a word Housing, Marketplace and Jobs
    /// all want, which the first module to claim would take from the other two.
    /// </summary>
    [Fact]
    public void The_module_declares_exactly_one_route_prefix()
    {
        HousingModuleMetadata.RoutePrefix.Should().Be("/api/v1/housing");
        HousingModuleMetadata.Schema.Should().Be("housing");

        var hardCoded = Module.GetTypes()
            .Where(type => type.Namespace == "Diaspora.Modules.Housing.Api")
            .SelectMany(type => type.GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .Where(field => field is { IsLiteral: true } && field.FieldType == typeof(string))
            .Select(field => (string?)field.GetRawConstantValue())
            .Where(value => value?.StartsWith("/api", StringComparison.Ordinal) == true)
            .ToList();

        hardCoded.Should().BeEmpty(
            "the prefix is declared once in module metadata and nowhere else");
    }

    /// <summary>
    /// Telemetry names a source that actually exists. Declaring one without creating it subscribes
    /// the tracer to silence, which is the B2 mistake.
    /// </summary>
    [Fact]
    public void The_telemetry_source_is_real()
    {
        var contributor = new HousingObservabilityContributor();

        contributor.ActivitySources.Should().Contain(HousingTelemetry.SourceName);
        contributor.Meters.Should().Contain(HousingTelemetry.SourceName);
        HousingTelemetry.Source.Name.Should().Be(HousingTelemetry.SourceName);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The module's own classes that depend on a namespace, ignoring the closures and iterator
    /// state machines the compiler generates beside them.
    /// </summary>
    private static IReadOnlyList<string> DeclaredTypesDependingOn(string dependency) =>
        [.. Types.InAssembly(Module)
            .That().HaveDependencyOn(dependency)
            .GetTypes()
            .Select(type => type.DeclaringType?.Name ?? type.Name)
            .Where(name => !name.StartsWith('<'))
            .Distinct()];

    /// <summary>Unwraps a task, a nullable or a collection to the type that actually matters.</summary>
    private static IEnumerable<Type> Unwrap(Type type)
    {
        if (!type.IsGenericType)
        {
            yield return type;
            yield break;
        }

        foreach (var argument in type.GetGenericArguments().SelectMany(Unwrap))
        {
            yield return argument;
        }
    }
}
