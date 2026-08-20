using System.Reflection;
using Diaspora.Contracts.Trust.V1.Submission;
using Diaspora.Modules.Trust;
using Diaspora.Modules.Trust.Api;
using Diaspora.Modules.Trust.Authorization;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Extensibility;
using Diaspora.Modules.Trust.Observability;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Contributors;
using Diaspora.Platform.Services.Abstractions;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The boundaries the Trust module must not cross, enforced rather than documented.
/// <para>
/// ADR-002's separation between Identity and Trust is the load-bearing decision of the whole
/// phase, and a documented boundary is a boundary until someone is in a hurry. These tests
/// are what make it survive the hurry.
/// </para>
/// </summary>
public sealed class TrustArchitectureTests
{
    private static Assembly TrustAssembly => typeof(TrustModule).Assembly;

    /// <summary>
    /// The single most important rule in the module. Trust owns trust; Identity owns
    /// identity; neither reaches into the other. If this fails, verification evidence and
    /// credential storage have started to merge, which is exactly what ADR-002 exists to
    /// prevent.
    /// </summary>
    [Fact]
    public void Trust_must_not_depend_on_the_identity_domain()
    {
        var result = Types.InAssembly(TrustAssembly)
            .ShouldNot()
            .HaveDependencyOn("Diaspora.Domain.Identity")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Trust must reach the caller through the kernel's ICurrentUser, never through " +
            "Identity's domain: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Trust_must_not_depend_on_identity_application_services()
    {
        var result = Types.InAssembly(TrustAssembly)
            .ShouldNot()
            .HaveDependencyOn("Diaspora.Application.Identity")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "reporting an outcome goes through the published trust-decision contract, not " +
            "through Identity's command handlers: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Trust_must_not_depend_on_the_api_host()
    {
        var result = Types.InAssembly(TrustAssembly)
            .ShouldNot()
            .HaveDependencyOn("Diaspora.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "a module the host must reference back is not a module: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Trust_must_not_depend_on_infrastructure()
    {
        var result = Types.InAssembly(TrustAssembly)
            .ShouldNot()
            .HaveDependencyOn("Diaspora.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the module writes against platform abstractions, not concrete implementations: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Trust_must_not_depend_on_another_feature_module()
    {
        var result = Types.InAssembly(TrustAssembly)
            .ShouldNot()
            .HaveDependencyOn("Diaspora.Modules.Bookmarks")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "feature modules communicate through integration events, never by reference: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// The route collision that would break ADR-002's inbound channel.
    /// <c>/api/v1/trust/decisions</c> belongs to Identity's <c>TrustDecisionsController</c>;
    /// Trust calls it and must never serve it.
    /// </summary>
    [Fact]
    public void Trust_must_not_claim_the_identity_owned_decisions_route()
    {
        var source = ReadEndpointContributorSource();

        source.Should().NotContain("\"/decisions\"",
            "POST /api/v1/trust/decisions is Identity's inbound channel from this module");

        TrustModuleMetadata.RoutePrefix.Should().Be("/api/v1/trust");
    }

    [Fact]
    public void Every_trust_route_lives_under_the_module_prefix()
    {
        var source = ReadEndpointContributorSource();

        // The group is mapped once, from the metadata constant, so no endpoint can quietly
        // register itself outside the module's own namespace.
        source.Should().Contain("MapGroup(TrustModuleMetadata.RoutePrefix)");
    }

    // ── Contributor model ────────────────────────────────────────────────

    [Fact]
    public void Trust_registers_through_every_contributor_the_kernel_scans_for()
    {
        var assemblies = new[] { TrustAssembly };

        ModuleAssemblyScanner.FindImplementations<IPermissionContributor>(assemblies)
            .Should().Contain(typeof(TrustPermissionContributor));

        ModuleAssemblyScanner.FindImplementations<IFeatureContributor>(assemblies)
            .Should().Contain(typeof(TrustFeatureContributor));

        ModuleAssemblyScanner.FindImplementations<IPolicyContributor>(assemblies)
            .Should().Contain(typeof(TrustPolicyContributor));

        ModuleAssemblyScanner.FindImplementations<IEndpointContributor>(assemblies)
            .Should().Contain(typeof(TrustEndpointContributor));

        ModuleAssemblyScanner.FindImplementations<IObservabilityContributor>(assemblies)
            .Should().Contain(typeof(TrustObservabilityContributor));
    }

    [Fact]
    public void The_module_is_discoverable_as_a_platform_module()
    {
        var registry = new ModuleRegistry().DiscoverModules([TrustAssembly]);

        registry.Modules.Should().Contain(module => module.Name == TrustModuleMetadata.Name);
    }

    [Fact]
    public void Every_declared_policy_names_a_permission_the_module_owns()
    {
        var permissions = new TrustPermissionContributor()
            .GetPermissions()
            .Select(permission => permission.Key)
            .ToHashSet(StringComparer.Ordinal);

        var policies = new TrustPolicyContributor().GetPolicies().ToList();

        policies.Should().NotBeEmpty();

        foreach (var policy in policies)
        {
            policy.Permission.Should().NotBeNull();
            permissions.Should().Contain(policy.Permission!,
                $"policy '{policy.Name}' references a permission that must exist, or the boot fails");
        }
    }

    [Fact]
    public void Every_permission_and_gate_is_owned_by_this_module()
    {
        new TrustPermissionContributor().GetPermissions()
            .Should().OnlyContain(permission => permission.Module == TrustModuleMetadata.Name);

        new TrustFeatureContributor().GetFeatures()
            .Should().OnlyContain(feature => feature.Module == TrustModuleMetadata.Name);
    }

    /// <summary>
    /// Permission keys are part of the frozen contract — a client or a role assignment can
    /// name one. This pins the prefix so a rename is a deliberate, visible act.
    /// </summary>
    [Fact]
    public void Every_permission_key_is_namespaced_to_the_module()
    {
        new TrustPermissionContributor().GetPermissions()
            .Should().OnlyContain(permission => permission.Key.StartsWith("trust:", StringComparison.Ordinal));

        new TrustFeatureContributor().GetFeatures()
            .Should().OnlyContain(feature => feature.Name.StartsWith("trust.", StringComparison.Ordinal));
    }

    /// <summary>
    /// A restricted member must keep exactly the permissions they need to understand and
    /// challenge the restriction — and nothing else. Widening this set is how a suspension
    /// quietly stops being a suspension.
    /// </summary>
    [Fact]
    public void Only_the_self_service_permissions_survive_restriction()
    {
        var survivors = new TrustPermissionContributor()
            .GetPermissions()
            .Where(permission => permission.GrantedWhenRestricted)
            .Select(permission => permission.Key)
            .ToList();

        survivors.Should().BeEquivalentTo(
        [
            TrustPermissions.ReadSelf,
            TrustPermissions.SubmitVerification,
            TrustPermissions.SubmitAppeal
        ]);
    }

    [Fact]
    public void Staff_permissions_require_both_a_role_and_a_verified_tier()
    {
        var staffKeys = new[]
        {
            TrustPermissions.ReviewVerification,
            TrustPermissions.ReviewAppeal,
            TrustPermissions.Moderate,
            TrustPermissions.ManageQueue,
            TrustPermissions.ManageBadges
        };

        var staff = new TrustPermissionContributor()
            .GetPermissions()
            .Where(permission => staffKeys.Contains(permission.Key, StringComparer.Ordinal))
            .ToList();

        staff.Should().HaveCount(staffKeys.Length);

        staff.Should().OnlyContain(permission => permission.Roles.Count > 0);
        staff.Should().OnlyContain(
            permission => permission.MinimumTier == Contracts.Authorization.AccessTier.Verified,
            "someone who cannot hold their own standing should not rule on anyone else's");
        staff.Should().OnlyContain(permission => !permission.GrantedWhenRestricted);
    }

    // ── Domain integrity ─────────────────────────────────────────────────

    [Fact]
    public void Aggregate_roots_expose_no_public_setters()
    {
        var aggregates = new[]
        {
            typeof(VerificationRequest), typeof(TrustProfile), typeof(ReputationProfile),
            typeof(Appeal), typeof(TrustEvent), typeof(ModerationItem),
            typeof(CommunityContribution)
        };

        foreach (var aggregate in aggregates)
        {
            var settable = aggregate
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.SetMethod is { IsPublic: true })
                .Select(property => $"{aggregate.Name}.{property.Name}")
                .ToList();

            settable.Should().BeEmpty(
                "state changes must go through methods that can enforce invariants");
        }
    }

    [Fact]
    public void The_domain_layer_does_not_reference_entity_framework()
    {
        var result = Types.InAssembly(TrustAssembly)
            .That().ResideInNamespace("Diaspora.Modules.Trust.Domain")
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "persistence concerns belong in Persistence/: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void The_domain_layer_does_not_reference_aspnet()
    {
        var result = Types.InAssembly(TrustAssembly)
            .That().ResideInNamespace("Diaspora.Modules.Trust.Domain")
            .ShouldNot()
            .HaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Every_table_lives_in_the_modules_own_schema()
    {
        var contributor = new TrustDatabaseContributor();

        contributor.Schema.Should().Be(TrustModuleMetadata.Schema);
        contributor.Module.Should().Be(TrustModuleMetadata.Name);
    }

    /// <summary>
    /// Declaring a telemetry source without creating one subscribes the tracer to silence —
    /// the B2 mistake B2.6 exists to prevent.
    /// </summary>
    [Fact]
    public void The_declared_telemetry_source_actually_exists()
    {
        var declared = new TrustObservabilityContributor().ActivitySources.Single();

        declared.Should().Be(TrustTelemetry.SourceName);
        TrustTelemetrySource.Activity.Name.Should().Be(declared);
        TrustTelemetrySource.Meter.Name.Should().Be(declared);
    }

    // ── Extensibility ────────────────────────────────────────────────────

    /// <summary>
    /// The brief's "extensible without modifying existing code", asserted. Every seam must be
    /// an interface the module discovers, not a type it names.
    /// </summary>
    [Fact]
    public void Every_extension_point_is_a_discoverable_interface()
    {
        var seams = new[]
        {
            typeof(IVerificationTypeHandler), typeof(IBadgeRule), typeof(ITrustRule),
            typeof(IReputationRuleContributor), typeof(ITrustRequirementContributor),
            typeof(IReputationCalculator)
        };

        seams.Should().OnlyContain(seam => seam.IsInterface);
    }

    [Fact]
    public void Every_shipped_verification_handler_is_discoverable()
    {
        var handlers = ModuleAssemblyScanner
            .FindImplementations<IVerificationTypeHandler>([TrustAssembly]);

        handlers.Should().HaveCountGreaterThan(10,
            "the platform ships a handler for each type it can verify");
    }

    /// <summary>
    /// The composed registry must not throw on the module's own handlers — a duplicate among
    /// the shipped set would fail every boot.
    /// </summary>
    [Fact]
    public void The_shipped_handlers_compose_without_conflict()
    {
        var handlers = ModuleAssemblyScanner
            .FindImplementations<IVerificationTypeHandler>([TrustAssembly])
            .Select(type => (IVerificationTypeHandler)Activator.CreateInstance(type)!)
            .ToList();

        var build = () => new VerificationTypeHandlerRegistry(handlers);

        build.Should().NotThrow();
    }

    [Fact]
    public void The_shipped_badge_rules_compose_without_conflict()
    {
        var rules = ModuleAssemblyScanner
            .FindImplementations<IBadgeRule>([TrustAssembly])
            .Select(type => (IBadgeRule)Activator.CreateInstance(type)!)
            .ToList();

        var build = () => new BadgeRuleCatalog(rules);

        build.Should().NotThrow();
    }

    /// <summary>
    /// A type that unlocks publishing must never have a path to a grant no human reviewed.
    /// </summary>
    [Fact]
    public void No_publishing_gate_verification_can_be_auto_approved()
    {
        var gating = new IVerificationTypeHandler[]
        {
            new Modules.Trust.Extensibility.Handlers.DiasporaVerificationHandler(),
            new Modules.Trust.Extensibility.Handlers.IdentityVerificationHandler(),
            new Modules.Trust.Extensibility.Handlers.CountryVerificationHandler()
        };

        gating.Should().OnlyContain(handler => !handler.SupportsAutomaticApproval);
    }

    // ── B3.0.2 submission contract ───────────────────────────────────────

    /// <summary>
    /// The property the whole phase turns on. If the contract moved into the Trust module, a
    /// consumer would need a project reference to the module that decides — which is the gap
    /// B3.0.2 closed and the reason B7 shipped a stub.
    /// </summary>
    [Fact]
    public void The_submission_contract_is_published_outside_the_module()
    {
        typeof(ITrustVerificationSubmission).Assembly.GetName().Name
            .Should().Be("Diaspora.Contracts");

        typeof(TrustVerificationSubmissionRequest).Assembly.GetName().Name
            .Should().Be("Diaspora.Contracts");

        typeof(TrustVerificationSubmissionResult).Assembly.GetName().Name
            .Should().Be("Diaspora.Contracts");

        typeof(TrustVerificationSubject).Assembly.GetName().Name
            .Should().Be("Diaspora.Contracts");
    }

    /// <summary>
    /// A published contract that reaches back into the module that publishes it is not
    /// published. Every type on the request and the result must be resolvable by a consumer
    /// holding <c>Diaspora.Contracts</c> and nothing of Trust's.
    /// </summary>
    [Fact]
    public void The_submission_contract_exposes_no_trust_type()
    {
        var surface = new[]
        {
            typeof(TrustVerificationSubmissionRequest),
            typeof(TrustVerificationSubmissionResult),
            typeof(TrustVerificationSubject)
        };

        var leaked = surface
            .SelectMany(type => type.GetProperties())
            .Select(property => property.PropertyType)
            .Select(type => Nullable.GetUnderlyingType(type) ?? type)
            .SelectMany(type => type.IsGenericType ? type.GetGenericArguments() : [type])
            .Where(type => type.Assembly.GetName().Name == TrustAssembly.GetName().Name)
            .Select(type => type.FullName!)
            .ToList();

        leaked.Should().BeEmpty(
            "a consumer must be able to build a submission without referencing Trust: "
            + string.Join(", ", leaked));
    }

    /// <summary>
    /// Submission is not approval. The contract's entire surface is one method that opens a
    /// case — there is deliberately no way to approve, reject, revoke, expire or award a badge
    /// through it, and a later addition of one would be the moment a module could verify itself.
    /// </summary>
    [Fact]
    public void The_submission_contract_cannot_decide_anything()
    {
        var methods = typeof(ITrustVerificationSubmission)
            .GetMethods()
            .Select(method => method.Name)
            .ToList();

        methods.Should().ContainSingle().Which.Should().Be("SubmitAsync");

        var forbidden = new[] { "Approve", "Reject", "Revoke", "Expire", "Grant", "Badge", "Award" };

        foreach (var verb in forbidden)
        {
            methods.Should().NotContain(
                name => name.Contains(verb, StringComparison.OrdinalIgnoreCase),
                $"only Trust may {verb.ToLowerInvariant()} a verification");
        }

        // Nor may the result carry a decision a caller could act on as a grant.
        typeof(TrustVerificationSubmissionResult)
            .GetProperties()
            .Select(property => property.Name)
            .Should().NotContain(["IsApproved", "IsVerified", "Badge", "ExpiresAt"]);
    }

    /// <summary>
    /// The module must actually implement what it publishes, and the implementation must stay
    /// inside the module — a contract nobody implements is the stub B7 already had.
    /// </summary>
    [Fact]
    public void Trust_implements_the_contract_it_publishes()
    {
        var implementations = ModuleAssemblyScanner
            .FindImplementations<ITrustVerificationSubmission>([TrustAssembly]);

        implementations.Should().ContainSingle(
            "exactly one implementation, in the module that owns the decision");
    }

    /// <summary>
    /// Both entry points must run the same workflow. Two copies would drift, and the copy that
    /// drifted would be the one a vertical uses — the member's own route is the one anybody
    /// tests by hand.
    /// </summary>
    [Fact]
    public void Both_submission_entry_points_share_one_workflow()
    {
        var coordinator = TrustAssembly.GetType(
            "Diaspora.Modules.Trust.Application.Services.VerificationSubmissionCoordinator");

        coordinator.Should().NotBeNull("the shared workflow B3.0.2 extracted");

        var dependsOnCoordinator = (Type type) => type
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Any(parameter => parameter.ParameterType == coordinator);

        var httpHandler = TrustAssembly.GetType(
            "Diaspora.Modules.Trust.Application.Verifications.SubmitVerificationCommandHandler")!;

        var contractService = TrustAssembly.GetType(
            "Diaspora.Modules.Trust.Application.Services.TrustVerificationSubmissionService")!;

        dependsOnCoordinator(httpHandler).Should().BeTrue(
            "the member's own route runs the shared workflow");

        dependsOnCoordinator(contractService).Should().BeTrue(
            "the published contract runs the same shared workflow");
    }

    private static string ReadEndpointContributorSource()
    {
        // Walk up from the test binary to the repository root. Reading the source is the only
        // way to assert on minimal-API route literals, which are not reflectable.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Diaspora.slnx")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the test must be able to find the repository root");

        var path = Path.Combine(
            directory!.FullName,
            "src", "Diaspora.Modules.Trust", "Api", "TrustEndpointContributor.cs");

        File.Exists(path).Should().BeTrue($"expected the endpoint contributor at {path}");

        return File.ReadAllText(path);
    }
}
