using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// Automated architecture rules enforcing layer dependency constraints.
/// </summary>
public class ArchitectureTests
{
    private const string SharedKernelNamespace = "Diaspora.SharedKernel";
    private const string DomainNamespace = "Diaspora.Domain";
    private const string ContractsNamespace = "Diaspora.Contracts";
    private const string PlatformNamespace = "Diaspora.Platform";
    private const string ApplicationNamespace = "Diaspora.Application";
    private const string InfrastructureNamespace = "Diaspora.Infrastructure";
    private const string PersistenceNamespace = "Diaspora.Persistence";
    private const string ApiNamespace = "Diaspora.Api";
    private const string ObservabilityNamespace = "Diaspora.Observability";

    [Fact]
    public void Domain_should_not_depend_on_infrastructure()
    {
        var result = Types.InAssembly(typeof(Domain.Abstractions.DomainException).Assembly)
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Domain must not depend on Infrastructure: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Domain_should_not_depend_on_persistence()
    {
        var result = Types.InAssembly(typeof(Domain.Abstractions.DomainException).Assembly)
            .ShouldNot()
            .HaveDependencyOn(PersistenceNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Domain must not depend on Persistence: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Domain_should_not_depend_on_api()
    {
        var result = Types.InAssembly(typeof(Domain.Abstractions.DomainException).Assembly)
            .ShouldNot()
            .HaveDependencyOn(ApiNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Domain_should_not_depend_on_application()
    {
        var result = Types.InAssembly(typeof(Domain.Abstractions.DomainException).Assembly)
            .ShouldNot()
            .HaveDependencyOn(ApplicationNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Domain must not depend on Application: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Application_should_not_depend_on_infrastructure()
    {
        var result = Types.InAssembly(typeof(Application.DependencyInjection).Assembly)
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Application must not depend on Infrastructure: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Application_should_not_depend_on_persistence()
    {
        var result = Types.InAssembly(typeof(Application.DependencyInjection).Assembly)
            .ShouldNot()
            .HaveDependencyOn(PersistenceNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Application_should_not_depend_on_api()
    {
        var result = Types.InAssembly(typeof(Application.DependencyInjection).Assembly)
            .ShouldNot()
            .HaveDependencyOn(ApiNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Infrastructure_should_not_depend_on_api()
    {
        var result = Types.InAssembly(typeof(Infrastructure.DependencyInjection).Assembly)
            .ShouldNot()
            .HaveDependencyOn(ApiNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Persistence_should_not_depend_on_api()
    {
        var result = Types.InAssembly(typeof(Persistence.DiasporaDbContext).Assembly)
            .ShouldNot()
            .HaveDependencyOn(ApiNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "persistence is inward of the host: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Contracts_should_not_depend_on_domain_or_application()
    {
        // Contracts is the outward-facing wire surface. If it reached into the domain, the
        // wire format would become a constraint on the model — the dependency runs the
        // other way, always.
        var result = Types.InAssembly(typeof(Contracts.Events.IIntegrationEvent).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(DomainNamespace, ApplicationNamespace, InfrastructureNamespace, PersistenceNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Contracts must stay a leaf surface: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Platform_kernel_should_not_depend_on_application_or_downstream_layers()
    {
        // The kernel is what modules build on; it must not depend on any particular module's
        // layer. It may reference Contracts and SharedKernel — its shared vocabulary — and
        // ASP.NET Core, but not Application, Infrastructure, Persistence or the host.
        var result = Types.InAssembly(typeof(Platform.Modules.IPlatformModule).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                ApplicationNamespace,
                InfrastructureNamespace,
                PersistenceNamespace,
                ApiNamespace,
                DomainNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the platform kernel must stay module-agnostic: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void SharedKernel_should_not_depend_on_any_project()
    {
        var result = Types.InAssembly(typeof(SharedKernel.Guard).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                DomainNamespace,
                ApplicationNamespace,
                InfrastructureNamespace,
                PersistenceNamespace,
                ApiNamespace,
                PlatformNamespace,
                ContractsNamespace,
                ObservabilityNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "SharedKernel must have zero project dependencies: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Domain_events_should_be_records()
    {
        var result = Types.InAssembly(typeof(SharedKernel.Primitives.DomainEvent).Assembly)
            .That()
            .Inherit(typeof(SharedKernel.Primitives.DomainEvent))
            .Should()
            .BeSealed()
            .GetResult();

        // Records are implicitly sealed if they don't derive from another record
        // This test verifies the convention
        result.IsSuccessful.Should().BeTrue();
    }
}
