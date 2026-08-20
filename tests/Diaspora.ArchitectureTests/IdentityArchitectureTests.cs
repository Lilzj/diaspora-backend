using Diaspora.Application.Identity.Abstractions;
using Diaspora.Domain.Identity.AccessStates;
using Diaspora.Domain.Identity.Users;
using Diaspora.SharedKernel.Primitives;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// Structural rules specific to the identity module. These encode the decisions that are
/// easy to erode one pull request at a time.
/// </summary>
public class IdentityArchitectureTests
{
    [Fact]
    public void Identity_domain_types_do_not_depend_on_the_contracts_assembly()
    {
        // Contracts is an outward-facing, versioned surface. A domain type reaching into it
        // would invert the dependency and make the wire format a constraint on the model.
        var result = Types.InAssembly(typeof(User).Assembly)
            .That().ResideInNamespaceStartingWith("Diaspora.Domain.Identity")
            .ShouldNot().HaveDependencyOn("Diaspora.Contracts")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "identity domain types must not depend on Contracts: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Identity_domain_types_do_not_depend_on_entity_framework()
    {
        var result = Types.InAssembly(typeof(User).Assembly)
            .That().ResideInNamespaceStartingWith("Diaspora.Domain.Identity")
            .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the domain must stay persistence-ignorant: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Identity_domain_types_do_not_depend_on_mediatr()
    {
        // The Domain project promises zero framework dependencies. Domain events reach
        // MediatR through the DomainEventNotification wrapper in the Application layer.
        var result = Types.InAssembly(typeof(User).Assembly)
            .That().ResideInNamespaceStartingWith("Diaspora.Domain.Identity")
            .ShouldNot().HaveDependencyOn("MediatR")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the domain must stay framework-free: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Identity_application_types_do_not_depend_on_asp_net_core()
    {
        var result = Types.InAssembly(typeof(IAccessGateway).Assembly)
            .That().ResideInNamespaceStartingWith("Diaspora.Application.Identity")
            .ShouldNot().HaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "use cases must not know about HTTP: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Identity_application_types_do_not_depend_on_entity_framework()
    {
        var result = Types.InAssembly(typeof(IAccessGateway).Assembly)
            .That().ResideInNamespaceStartingWith("Diaspora.Application.Identity")
            .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "use cases must reach persistence only through repository contracts: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Identity_domain_events_are_sealed_records()
    {
        var result = Types.InAssembly(typeof(User).Assembly)
            .That().ResideInNamespace("Diaspora.Domain.Identity.Events")
            .Should().BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "domain events are immutable facts: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Identity_aggregates_are_sealed()
    {
        var result = Types.InAssembly(typeof(User).Assembly)
            .That().ResideInNamespaceStartingWith("Diaspora.Domain.Identity")
            .And().Inherit(typeof(AggregateRoot<>))
            .Should().BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "aggregates must not be subclassed: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Every_access_state_declares_at_least_one_legal_transition()
    {
        // A state with no outbound edge is an account that can never be recovered.
        foreach (var state in AccessState.All)
        {
            AccessStateTransitions.AllowedFrom(state).Should().NotBeEmpty(
                $"'{state.Name}' would otherwise be an inescapable dead end");
        }
    }

    [Fact]
    public void No_access_state_transitions_to_itself()
    {
        foreach (var state in AccessState.All)
        {
            state.CanTransitionTo(state).Should().BeFalse(
                $"'{state.Name}' must not declare a self-transition");
        }
    }

    [Fact]
    public void Verification_is_reachable_only_through_the_trust_engine()
    {
        // The structural expression of the B2/B3 split. If someone adds an edge into
        // Verified from a member-reachable state, this fails.
        var sources = AccessState.All
            .Where(state => state.CanTransitionTo(AccessState.Verified))
            .Select(state => state.Name)
            .ToArray();

        sources.Should().BeEquivalentTo(
            [
                nameof(AccessState.VerificationUnderReview),
                nameof(AccessState.Suspended),
                nameof(AccessState.Locked)
            ],
            "Verified must be reachable only from a Trust Engine review, or by restoring a "
            + "member who was already verified before a restriction");
    }

    [Fact]
    public void Identity_declares_only_identity_owned_permissions()
    {
        // The literal B2.6 rule: Identity must never contain Marketplace, Messaging or Job
        // permissions. Every key Identity's contributor declares must be Identity's own,
        // and none may belong to another module's namespace.
        var contributor = new Application.Identity.Authorization.IdentityPermissionContributor();

        var foreign = contributor.GetPermissions()
            .Where(permission =>
                permission.Key.StartsWith("marketplace", StringComparison.OrdinalIgnoreCase) ||
                permission.Key.StartsWith("messaging", StringComparison.OrdinalIgnoreCase) ||
                permission.Key.StartsWith("jobs", StringComparison.OrdinalIgnoreCase) ||
                permission.Key.StartsWith("organizations", StringComparison.OrdinalIgnoreCase) ||
                permission.Key.StartsWith("content", StringComparison.OrdinalIgnoreCase))
            .Select(permission => permission.Key)
            .ToArray();

        foreign.Should().BeEmpty(
            "Identity must not own another module's permissions: " + string.Join(", ", foreign));
    }

    [Fact]
    public void Identity_declares_only_identity_owned_feature_gates()
    {
        var contributor = new Application.Identity.Authorization.IdentityFeatureContributor();

        var foreign = contributor.GetFeatures()
            .Where(feature =>
                feature.Name.StartsWith("marketplace", StringComparison.OrdinalIgnoreCase) ||
                feature.Name.StartsWith("messaging", StringComparison.OrdinalIgnoreCase) ||
                feature.Name.StartsWith("jobs", StringComparison.OrdinalIgnoreCase) ||
                feature.Name.StartsWith("guides", StringComparison.OrdinalIgnoreCase) ||
                feature.Name.StartsWith("organizations", StringComparison.OrdinalIgnoreCase))
            .Select(feature => feature.Name)
            .ToArray();

        foreign.Should().BeEmpty(
            "Identity must not own another module's feature gates: " + string.Join(", ", foreign));
    }

    [Fact]
    public void Every_user_role_resolves_to_a_permission_set()
    {
        // The resolver now reads a composed catalogue; build one from Identity's own
        // contributor, which is all this needs to prove no role/state pair throws.
        var catalog = new Platform.Authorization.PermissionCatalog(
            [new Application.Identity.Authorization.IdentityPermissionContributor()]);
        var resolver = new Application.Identity.Authorization.PermissionResolver(catalog);

        foreach (var role in UserRole.All)
        {
            foreach (var state in AccessState.All)
            {
                var act = () => resolver.Resolve(role, state);
                act.Should().NotThrow($"{role.Name} in {state.Name} must resolve to a permission set");
            }
        }
    }
}
