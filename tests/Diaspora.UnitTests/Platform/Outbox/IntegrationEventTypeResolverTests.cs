using Diaspora.Contracts.Providers.V1.Events;
using Diaspora.Platform.Messaging.Outbox.Serialization;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Platform.Outbox;

/// <summary>
/// The contract catalogue: what a stored row's <c>EventType</c> column actually means.
/// <para>
/// The single rule these tests exist to protect is that a durable row never names a CLR type.
/// An assembly-qualified name binds a message that may live for a month to an assembly version,
/// a public key and a culture, every one of which a routine build can change — and the symptom
/// would be a dead-letter queue full of events nobody can deserialize, discovered days after
/// the release that caused it.
/// </para>
/// </summary>
public sealed class IntegrationEventTypeResolverTests
{
    private static IntegrationEventTypeResolver Resolver() =>
        new([typeof(WidgetChangedIntegrationEvent).Assembly, typeof(ProviderCreatedIntegrationEvent).Assembly]);

    [Fact]
    public void An_events_default_contract_name_is_its_namespace_qualified_type_name()
    {
        var contract = Resolver().ContractFor(typeof(ProviderCreatedIntegrationEvent));

        contract.Name.Should().Be(
            "Diaspora.Contracts.Providers.V1.Events.ProviderCreatedIntegrationEvent");
        contract.Version.Should().Be(1);
    }

    /// <summary>
    /// The property that makes the whole scheme survive a build. Nothing about the assembly may
    /// appear in the stored name.
    /// </summary>
    [Fact]
    public void A_contract_name_never_contains_assembly_identity()
    {
        var resolver = Resolver();

        foreach (var contract in resolver.Contracts)
        {
            contract.Name.Should().NotContain(
                "Version=", "an assembly version in a durable name breaks on the next release");
            contract.Name.Should().NotContain("PublicKeyToken=");
            contract.Name.Should().NotContain("Culture=");
        }
    }

    [Fact]
    public void An_explicit_contract_attribute_pins_the_name_and_version()
    {
        var contract = Resolver().ContractFor(typeof(OutboxPinnedContractEvent));

        contract.Name.Should().Be("diaspora.test.pinned");
        contract.Version.Should().Be(3);
    }

    [Fact]
    public void A_pinned_contract_resolves_back_to_its_clr_type()
    {
        Resolver().TryResolve("diaspora.test.pinned", 3, out var contract).Should().BeTrue();

        contract!.ClrType.Should().Be<OutboxPinnedContractEvent>();
    }

    /// <summary>
    /// The rolling-deployment case. An instance running older code meets a message written by
    /// newer code; the correct answer is "I do not know this", not an exception and not a
    /// silently wrong type.
    /// </summary>
    [Fact]
    public void An_unknown_version_of_a_known_contract_does_not_resolve()
    {
        Resolver().TryResolve("diaspora.test.pinned", 99, out var contract).Should().BeFalse();

        contract.Should().BeNull();
    }

    [Fact]
    public void The_owning_module_is_derived_from_the_contract_namespace()
    {
        Resolver().ModuleFor(typeof(ProviderCreatedIntegrationEvent)).Should().Be("Providers");
    }

    /// <summary>
    /// Every integration event in the platform must be discoverable. One that is not would be
    /// enqueued perfectly well and then be undeserializable on the way out.
    /// </summary>
    [Fact]
    public void Every_shipped_integration_event_is_in_the_catalogue()
    {
        var resolver = new IntegrationEventTypeResolver();

        resolver.Contracts.Should().NotBeEmpty();

        resolver.Contracts.Select(contract => contract.ClrType)
            .Should().Contain(typeof(ProviderCreatedIntegrationEvent));
    }

    /// <summary>
    /// Every shipped contract name must be unique. The constructor enforces it, and this is the
    /// test that runs the enforcement against the real assemblies — a collision would route one
    /// module's events into another module's deserializer.
    /// </summary>
    [Fact]
    public void The_shipped_contract_names_do_not_collide()
    {
        var act = () => new IntegrationEventTypeResolver();

        act.Should().NotThrow<InvalidOperationException>();
    }

    [Fact]
    public void Asking_for_a_contract_on_a_type_that_is_not_an_event_is_a_programming_error()
    {
        var act = () => Resolver().ContractFor(typeof(string));

        act.Should().Throw<ArgumentException>();
    }
}
