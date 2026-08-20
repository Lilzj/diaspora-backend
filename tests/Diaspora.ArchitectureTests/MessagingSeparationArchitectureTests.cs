using System.Reflection;
using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Events;
using Diaspora.Platform.Messaging;
using Diaspora.Platform.Messaging.Outbox.Abstractions;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Primitives;
using FluentAssertions;
using MediatR;
using NetArchTest.Rules;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The shape of the two messaging hierarchies, asserted at the level the compiler cannot reach.
/// <para>
/// <b>These are the second line, not the first.</b> B1.2's primary guarantee is the compiler's —
/// <c>MessagingCompilerEnforcementTests</c> proves that the illegal call sites do not build. What
/// a compiler cannot object to is somebody re-establishing the substitutability deliberately:
/// declaring a record that implements both interfaces, adding a dispatch port typed on
/// <see cref="IMessage"/>, or reinstating the inheritance in a hurry during a merge. Each of
/// those would make the compiler stop complaining, which is the failure mode this file exists to
/// catch.
/// </para>
/// <para>
/// B1.1's own evolution report set out two options for closing the hole: sever the inheritance,
/// or add an architecture test forbidding the bypass. This phase took the first, and these tests
/// are what keep the first from decaying back into needing the second.
/// </para>
/// </summary>
public sealed class MessagingSeparationArchitectureTests
{
    private static readonly string[] ModuleAssemblyNames =
    [
        "Diaspora.Modules.Trust",
        "Diaspora.Modules.Location",
        "Diaspora.Modules.Media",
        "Diaspora.Modules.Community",
        "Diaspora.Modules.Providers",
        "Diaspora.Modules.Guides",
        "Diaspora.Modules.Bookmarks"
    ];

    private static IReadOnlyList<Assembly> ModuleAssemblies =>
        ModuleAssemblyNames.Select(name => Assembly.Load(new AssemblyName(name))).ToArray();

    private static Assembly ContractsAssembly => typeof(IIntegrationEvent).Assembly;

    private static Assembly SharedKernelAssembly => typeof(IDomainEvent).Assembly;

    private static IReadOnlyList<Assembly> PlatformAndModuleAssemblies =>
    [
        SharedKernelAssembly,
        ContractsAssembly,
        typeof(IIntegrationEventPublisher).Assembly,
        typeof(DomainEventHandler<>).Assembly,
        typeof(Infrastructure.Messaging.InProcessOutboxTransport).Assembly,
        typeof(Persistence.DiasporaDbContext).Assembly,
        .. ModuleAssemblies
    ];

    // ── The hierarchies are independent ──────────────────────────────────

    /// <summary>
    /// <b>The finding B1.1 recorded and B1.2 closed.</b> Everything else in this file follows
    /// from these two assertions; if either fails, the compile errors that enforce the boundary
    /// stop being errors and the platform silently returns to its pre-B1.2 shape.
    /// </summary>
    [Fact]
    public void Neither_messaging_interface_inherits_the_other()
    {
        typeof(IDomainEvent).IsAssignableFrom(typeof(IIntegrationEvent)).Should().BeFalse(
            "an integration event that is also a domain event can be handed to the domain " +
            "dispatcher, which is exactly how 29 of 42 contracts bypassed the outbox before B1.2");

        typeof(IIntegrationEvent).IsAssignableFrom(typeof(IDomainEvent)).Should().BeFalse(
            "a domain event that is also an integration event could be published as a contract " +
            "nobody agreed to, and serialized with the module's own types inside it");
    }

    /// <summary>
    /// The base records, which is where the inheritance actually lived. Asserting on the
    /// interfaces alone would miss <c>IntegrationEvent : DomainEvent</c> being reinstated while
    /// the interfaces stayed separate.
    /// </summary>
    [Fact]
    public void Neither_base_record_inherits_the_other()
    {
        typeof(DomainEvent).IsAssignableFrom(typeof(IntegrationEvent)).Should().BeFalse();
        typeof(IntegrationEvent).IsAssignableFrom(typeof(DomainEvent)).Should().BeFalse();

        typeof(IntegrationEvent).BaseType.Should().Be<object>(
            "an intermediate base would put its properties ahead of the event's own in the " +
            "serialized payload and silently change the wire format");
    }

    /// <summary>
    /// They meet at <see cref="IMessage"/> and nowhere else. A second shared interface — even an
    /// innocuous one added for logging — would be a place for a future dispatch port to be typed.
    /// </summary>
    [Fact]
    public void The_two_hierarchies_share_exactly_one_interface()
    {
        var shared = typeof(IDomainEvent).GetInterfaces()
            .Intersect(typeof(IIntegrationEvent).GetInterfaces())
            .ToArray();

        shared.Should().ContainSingle().Which.Should().Be<IMessage>();
    }

    /// <summary>
    /// <b>The loophole the compiler cannot close.</b> Nothing stops a record from implementing
    /// both interfaces by hand, and such a type would be accepted by both pipelines — which is
    /// the pre-B1.2 defect reintroduced one class at a time.
    /// </summary>
    [Fact]
    public void No_type_in_the_platform_is_both_a_domain_event_and_an_integration_event()
    {
        var offenders = PlatformAndModuleAssemblies
            .SelectMany(SafeTypes)
            .Where(type => typeof(IDomainEvent).IsAssignableFrom(type)
                           && typeof(IIntegrationEvent).IsAssignableFrom(type))
            .Select(type => type.FullName)
            .ToArray();

        offenders.Should().BeEmpty(
            "a type satisfying both contracts is accepted by both pipelines, and which one " +
            "carries it becomes a property of the call site rather than of the type");
    }

    // ── No port straddles the boundary ───────────────────────────────────

    /// <summary>
    /// <see cref="IMessage"/> is a shape, not a channel. A port typed on it would accept both
    /// kinds again, and the separation would survive in the type hierarchy while being gone in
    /// practice — the hardest version of this defect to see in review.
    /// </summary>
    [Fact]
    public void No_dispatch_or_publish_port_is_typed_on_the_shared_root()
    {
        var ports = new[]
        {
            typeof(IDomainEventDispatcher), typeof(IIntegrationEventPublisher),
            typeof(IOutboxWriter), typeof(IOutboxTransport), typeof(IOutboxPartitionKeyStrategy)
        };

        foreach (var port in ports)
        {
            var parameters = port.GetMethods()
                .SelectMany(method => method.GetParameters())
                .Select(parameter => parameter.ParameterType)
                .Select(type => type.IsGenericType && !type.IsGenericTypeDefinition
                    ? type.GetGenericArguments()[0]
                    : type)
                .ToArray();

            parameters.Should().NotContain(
                typeof(IMessage),
                $"{port.Name} must commit to one hierarchy; IMessage commits to neither");
        }
    }

    /// <summary>
    /// The domain dispatcher takes domain events and the publisher takes integration events —
    /// stated as an assertion because these two signatures are the entire compile-time
    /// enforcement, and a widening of either would be a one-word change in review.
    /// </summary>
    [Fact]
    public void Each_port_accepts_exactly_one_kind_of_message()
    {
        var dispatch = typeof(IDomainEventDispatcher).GetMethod(
            nameof(IDomainEventDispatcher.DispatchAsync))!;

        dispatch.GetParameters()[0].ParameterType
            .Should().Be<IEnumerable<IDomainEvent>>();

        var publish = typeof(IIntegrationEventPublisher).GetMethod(
            nameof(IIntegrationEventPublisher.PublishAsync))!;

        publish.GetParameters()[0].ParameterType
            .Should().Be<IIntegrationEvent>();

        typeof(IOutboxWriter).GetMethod(nameof(IOutboxWriter.Enqueue))!
            .GetParameters()[0].ParameterType
            .Should().Be<IIntegrationEvent>(
                "a domain event in the outbox would be a private fact published as a contract");
    }

    // ── The two mediator pipelines stay apart ────────────────────────────

    /// <summary>
    /// The notification wrappers are where the separation could most easily be undone, because a
    /// single <c>IMessage</c>-constrained wrapper is obviously less code and would compile. It
    /// would also let one notification reach both pipelines' handlers, making which ran a
    /// registration accident.
    /// </summary>
    [Fact]
    public void The_notification_wrappers_are_constrained_to_their_own_hierarchy()
    {
        typeof(DomainEventNotification<>).GetGenericArguments()[0]
            .GetGenericParameterConstraints()
            .Should().ContainSingle().Which.Should().Be<IDomainEvent>();

        typeof(IntegrationEventNotification<>).GetGenericArguments()[0]
            .GetGenericParameterConstraints()
            .Should().ContainSingle().Which.Should().Be<IIntegrationEvent>();

        typeof(DomainEventHandler<>).GetGenericArguments()[0]
            .GetGenericParameterConstraints()
            .Should().ContainSingle().Which.Should().Be<IDomainEvent>();

        typeof(IntegrationEventHandler<>).GetGenericArguments()[0]
            .GetGenericParameterConstraints()
            .Should().ContainSingle().Which.Should().Be<IIntegrationEvent>();
    }

    /// <summary>
    /// <b>No consumer straddles the two pipelines.</b> Every handler in the platform reaches its
    /// messages through one wrapper or the other, and this walks the registrations rather than
    /// the base classes — a handler implementing <c>INotificationHandler</c> directly would slip
    /// past a base-class check.
    /// </summary>
    [Fact]
    public void Every_handler_belongs_to_exactly_one_pipeline()
    {
        var handlers = PlatformAndModuleAssemblies
            .SelectMany(SafeTypes)
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Select(type => new
            {
                Type = type,
                Handled = type.GetInterfaces()
                    .Where(contract => contract.IsGenericType
                                       && contract.GetGenericTypeDefinition() == typeof(INotificationHandler<>))
                    .Select(contract => contract.GetGenericArguments()[0])
                    .ToArray()
            })
            .Where(candidate => candidate.Handled.Length > 0)
            .ToArray();

        handlers.Should().NotBeEmpty("the walk must actually find the platform's handlers");

        foreach (var handler in handlers)
        {
            var kinds = handler.Handled
                .Where(notification => notification.IsGenericType)
                .Select(notification => notification.GetGenericTypeDefinition())
                .Where(definition => definition == typeof(DomainEventNotification<>)
                                     || definition == typeof(IntegrationEventNotification<>))
                .Distinct()
                .ToArray();

            kinds.Length.Should().BeLessThanOrEqualTo(
                1,
                $"{handler.Type.FullName} handles both a domain event and an integration event, " +
                "so a change to one pipeline's delivery guarantees silently changes the other's");
        }
    }

    // ── Ownership stays where it belongs ─────────────────────────────────

    /// <summary>
    /// Contracts is the published surface and must not learn about either pipeline. A reference
    /// to the dispatcher or the publisher here would let a contract prescribe how it travels,
    /// and would make an out-of-process consumer take a dependency on the platform kernel to
    /// parse a payload.
    /// </summary>
    [Fact]
    public void The_contracts_assembly_names_no_pipeline()
    {
        var result = Types.InAssembly(ContractsAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Diaspora.Platform", "Diaspora.Application", "MediatR")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "a published contract must be parseable by a consumer holding nothing but this " +
            $"assembly: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    /// <summary>
    /// Every published contract lives in <c>Diaspora.Contracts</c> and derives the shared base.
    /// A module-local integration event would still serialize, and its durable contract name
    /// would carry the module's namespace — pinning the wire format to a project that is
    /// expected to move when the module is extracted.
    /// </summary>
    [Fact]
    public void Every_integration_event_is_a_contract_in_the_contracts_assembly()
    {
        var declared = PlatformAndModuleAssemblies
            .SelectMany(SafeTypes)
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(type => typeof(IIntegrationEvent).IsAssignableFrom(type))
            .ToArray();

        declared.Should().NotBeEmpty();

        declared.Should().OnlyContain(
            type => type.Assembly == ContractsAssembly,
            "an integration event outside the contracts assembly is a contract a consumer " +
            "cannot reference without referencing the module that raised it");

        declared.Should().OnlyContain(
            type => typeof(IntegrationEvent).IsAssignableFrom(type),
            "implementing IIntegrationEvent by hand skips EventId and OccurredAt, which the " +
            "outbox uses as the message identity and the consumer as the idempotency key");
    }

    /// <summary>
    /// And the mirror: no domain event escapes into the published surface.
    /// </summary>
    [Fact]
    public void No_domain_event_lives_in_the_contracts_assembly()
    {
        var offenders = SafeTypes(ContractsAssembly)
            .Where(type => typeof(IDomainEvent).IsAssignableFrom(type))
            .Select(type => type.FullName)
            .ToArray();

        offenders.Should().BeEmpty(
            "Contracts is what other services parse; a domain event here would be an internal " +
            "fact presented as a promise");
    }

    private static IEnumerable<Type> SafeTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            // A type that fails to load cannot be a violation of anything asserted here, and
            // taking the suite down over one would make an unrelated packaging problem look like
            // an architecture failure.
            return exception.Types.Where(type => type is not null)!;
        }
    }
}
