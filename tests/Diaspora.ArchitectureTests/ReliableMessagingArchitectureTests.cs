using System.Reflection;
using Diaspora.Contracts.Events;
using Diaspora.Infrastructure.Messaging;
using Diaspora.Persistence;
using Diaspora.Persistence.Outbox;
using Diaspora.Platform.Messaging;
using Diaspora.Platform.Messaging.Outbox.Abstractions;
using Diaspora.Platform.Messaging.Outbox.Model;
using Diaspora.Platform.Messaging.Outbox.Serialization;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The boundaries B1.1 depends on, asserted rather than described.
/// <para>
/// The phase's central claim is that reliable delivery became a platform property without any
/// business module participating. That claim decays the moment a module reaches past the outbox
/// — publishing directly, configuring a retry policy, or referencing a dispatcher — and every
/// such reach would work perfectly in the deployment that introduced it and be discovered as a
/// class of lost events much later. These tests are how the claim stays true.
/// </para>
/// </summary>
public sealed class ReliableMessagingArchitectureTests
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
        ModuleAssemblyNames
            .Select(name => Assembly.Load(new AssemblyName(name)))
            .ToArray();

    private static Assembly PlatformAssembly => typeof(OutboxMessage).Assembly;

    private static Assembly PersistenceAssembly => typeof(DiasporaDbContext).Assembly;

    // ── No module bypasses the outbox ────────────────────────────────────

    /// <summary>
    /// <b>The rule the whole phase rests on.</b> A module publishes through
    /// <c>IIntegrationEventPublisher</c> and knows nothing else. A module that referenced the
    /// outbox directly could enqueue outside a transaction, or configure its own retries, and the
    /// platform-wide guarantee would quietly become a per-module one.
    /// <para>
    /// <b>B1.2 narrowed this from <c>Diaspora.Platform.Messaging</c> to its <c>.Outbox</c>
    /// subtree, and the narrowing is not a relaxation.</b> The publisher port moved into the
    /// messaging namespace in this phase — it had been sitting in
    /// <c>Diaspora.Application.Identity.Abstractions</c> because Identity happened to publish
    /// first — so the old prefix would now forbid the one dependency every module is
    /// <em>supposed</em> to have. What must stay unreachable is the machinery: the writer, the
    /// dispatcher, the retry policy, the serializer, the store.
    /// <see cref="A_module_names_no_messaging_type_but_the_publisher_port"/> pins that the
    /// widened prefix admits nothing else, so the two tests together are strictly stronger than
    /// the one they replace.
    /// </para>
    /// </summary>
    [Fact]
    public void No_business_module_references_the_outbox()
    {
        foreach (var assembly in ModuleAssemblies)
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOn("Diaspora.Platform.Messaging.Outbox")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"{assembly.GetName().Name} must reach delivery only through " +
                $"IIntegrationEventPublisher: {string.Join(", ", result.FailingTypeNames ?? [])}");
        }
    }

    /// <summary>
    /// The companion to the rule above: outside the <c>.Outbox</c> subtree the messaging
    /// namespace contains exactly two public types — the publisher port and the composition
    /// root that wires the kernel — and a module may name only the first.
    /// <para>
    /// Without this, a later addition to <c>Diaspora.Platform.Messaging</c> would become
    /// module-reachable silently, and the narrowing B1.2 made to the rule above would have been
    /// the loophole it looks like at first glance.
    /// </para>
    /// </summary>
    [Fact]
    public void A_module_names_no_messaging_type_but_the_publisher_port()
    {
        var messagingRoot = PlatformAssembly.GetTypes()
            .Where(type => type.IsPublic)
            .Where(type => string.Equals(
                type.Namespace, "Diaspora.Platform.Messaging", StringComparison.Ordinal))
            .Select(type => type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        messagingRoot.Should().BeEquivalentTo(
            ["IIntegrationEventPublisher", "PlatformMessagingRegistration"],
            "a new type here would become reachable by every module without review");

        foreach (var assembly in ModuleAssemblies)
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOn("Diaspora.Platform.Messaging.PlatformMessagingRegistration")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"{assembly.GetName().Name} must not compose the messaging kernel: " +
                $"{string.Join(", ", result.FailingTypeNames ?? [])}");
        }
    }

    /// <summary>
    /// The storage adapters are equally off-limits. A module that touched <c>EfOutboxWriter</c>
    /// could write a message without the aggregate, which is the defect inverted.
    /// </summary>
    [Fact]
    public void No_business_module_references_the_outbox_storage_adapters()
    {
        foreach (var assembly in ModuleAssemblies)
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOn("Diaspora.Persistence.Outbox")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"{assembly.GetName().Name} must not touch outbox storage: " +
                $"{string.Join(", ", result.FailingTypeNames ?? [])}");
        }
    }

    /// <summary>
    /// The publisher port is the only way out. Exactly one implementation is registered, and it
    /// is the durable one — two implementations of a single-resolution interface would leave
    /// which one wins to registration order.
    /// </summary>
    [Fact]
    public void The_registered_integration_event_publisher_is_the_durable_one()
    {
        typeof(OutboxIntegrationEventPublisher).Should().Implement<IIntegrationEventPublisher>();

        var implementations = typeof(OutboxIntegrationEventPublisher).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(type => typeof(IIntegrationEventPublisher).IsAssignableFrom(type))
            .Select(type => type.Name)
            .ToArray();

        // The pre-B1.1 publisher is kept, unregistered, as the documented rollback path. Any
        // third implementation is a fork of the delivery guarantee.
        implementations.Should().BeEquivalentTo(
            ["OutboxIntegrationEventPublisher", "MediatRIntegrationEventPublisher"]);
    }

    // ── The platform owns dispatch ───────────────────────────────────────

    /// <summary>
    /// Serialization, partitioning, retry policy and dispatch live in the kernel, and the kernel
    /// has no database in it. That is what allows a second storage adapter — a different
    /// provider, a sharded store — without any of those decisions being reimplemented.
    /// </summary>
    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Npgsql")]
    [InlineData("Microsoft.Data.Sqlite")]
    public void The_outbox_kernel_names_no_persistence_technology(string dependency)
    {
        var result = Types.InAssembly(PlatformAssembly)
            .That().ResideInNamespaceStartingWith("Diaspora.Platform.Messaging")
            .ShouldNot()
            .HaveDependencyOn(dependency)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the outbox kernel must stay storage-agnostic: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// And no transport. The kernel decides <em>whether and when</em> to deliver; a transport
    /// decides <em>where</em>. A mediator reference here would weld the two together and make
    /// the eventual extraction of a module a rewrite rather than a registration.
    /// </summary>
    [Fact]
    public void The_outbox_kernel_names_no_transport()
    {
        var result = Types.InAssembly(PlatformAssembly)
            .That().ResideInNamespaceStartingWith("Diaspora.Platform.Messaging")
            .ShouldNot()
            .HaveDependencyOn("MediatR")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "delivery must reach consumers through IOutboxTransport alone: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// One transport is registered by default and it is the in-process one — the correct choice
    /// for a modular monolith, and the single class that changes when a module is extracted.
    /// </summary>
    [Fact]
    public void The_in_process_transport_is_the_only_shipped_transport()
    {
        var transports = typeof(InProcessOutboxTransport).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(type => typeof(IOutboxTransport).IsAssignableFrom(type))
            .ToArray();

        transports.Should().ContainSingle().Which.Should().Be<InProcessOutboxTransport>();
    }

    // ── Durable identity is never a CLR identity ─────────────────────────

    /// <summary>
    /// <b>The rule that keeps a stored message readable after a release.</b> An
    /// assembly-qualified name in a durable row binds it to an assembly version, a public key
    /// and a culture; the symptom of getting this wrong is a dead-letter queue nobody can drain,
    /// discovered days later.
    /// </summary>
    [Fact]
    public void No_contract_name_carries_assembly_identity()
    {
        var resolver = new IntegrationEventTypeResolver();

        resolver.Contracts.Should().NotBeEmpty();

        resolver.Contracts.Should().OnlyContain(
            contract =>
                !contract.Name.Contains("Version=", StringComparison.Ordinal) &&
                !contract.Name.Contains("PublicKeyToken=", StringComparison.Ordinal) &&
                !contract.Name.Contains("Culture=", StringComparison.Ordinal));
    }

    /// <summary>
    /// Every shipped integration event resolves. One that does not would enqueue perfectly well
    /// and be undeliverable on the way out — a defect that only appears once the message is
    /// already committed.
    /// </summary>
    [Fact]
    public void Every_shipped_integration_event_resolves_both_ways()
    {
        var resolver = new IntegrationEventTypeResolver();

        var declared = typeof(IIntegrationEvent).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(type => typeof(IIntegrationEvent).IsAssignableFrom(type))
            .ToArray();

        declared.Should().NotBeEmpty();

        foreach (var type in declared)
        {
            var contract = resolver.ContractFor(type);

            resolver.TryResolve(contract.Name, contract.Version, out var round)
                .Should().BeTrue($"'{type.FullName}' must be resolvable from its stored name");

            round!.ClrType.Should().Be(type);
        }
    }

    // ── The outbox tables stay outside the aggregate pipeline ────────────

    /// <summary>
    /// An outbox row that raised domain events would enqueue a message about its own creation,
    /// forever. A soft-deletable one would be filtered out of the dispatcher's own query while
    /// still occupying the table. Both interfaces are applied by convention elsewhere in the
    /// persistence layer, which is precisely why this needs asserting.
    /// </summary>
    [Fact]
    public void The_outbox_tables_are_not_aggregates()
    {
        foreach (var type in new[]
                 {
                     typeof(OutboxMessage), typeof(OutboxDeadLetter), typeof(OutboxDeliveryAttempt)
                 })
        {
            type.GetInterfaces().Select(contract => contract.Name)
                .Should().NotContain(["IHasDomainEvents", "ISoftDeletable", "IAuditableEntity"]);
        }
    }

    /// <summary>
    /// The outbox owns its own schema rather than borrowing a module's. Putting it in
    /// <c>identity</c> because identity happened to publish first is the accident the platform's
    /// schema-ownership check exists to prevent.
    /// </summary>
    [Fact]
    public void The_outbox_owns_its_own_schema()
    {
        Persistence.Outbox.Configurations.OutboxSchema.Name.Should().Be("platform");
    }

    /// <summary>
    /// The storage adapters live in one place, so a change to how messages are claimed cannot be
    /// made in six.
    /// </summary>
    [Fact]
    public void The_storage_ports_have_exactly_one_implementation_each()
    {
        foreach (var port in new[]
                 {
                     typeof(IOutboxWriter), typeof(IOutboxStore),
                     typeof(IOutboxQueries), typeof(IOutboxMaintenance)
                 })
        {
            PersistenceAssembly.GetTypes()
                .Where(type => type is { IsAbstract: false, IsInterface: false })
                .Where(port.IsAssignableFrom)
                .Should().ContainSingle($"{port.Name} must have exactly one storage implementation");
        }
    }
}
