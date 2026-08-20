using System.Diagnostics.CodeAnalysis;
using Diaspora.Contracts.Events;

namespace Diaspora.Platform.Messaging.Outbox.Serialization;

/// <summary>
/// Translates between an integration event's CLR type and its published contract identity.
/// <para>
/// The single reason this abstraction exists: <b>never write a CLR type name into a durable
/// store</b>. An assembly-qualified name binds a row that may live for months to an assembly
/// name, a version, a public key and a culture, every one of which a routine build can change.
/// A contract name is a string somebody chose on purpose and can be searched for.
/// </para>
/// </summary>
public interface IIntegrationEventTypeResolver
{
    /// <summary>Every contract this deployment can read or write.</summary>
    IReadOnlyCollection<IntegrationEventContract> Contracts { get; }

    /// <summary>
    /// The contract for a CLR type. Throws when the type is not an integration event, which is
    /// a programming error rather than a runtime condition.
    /// </summary>
    IntegrationEventContract ContractFor(Type clrType);

    /// <summary>
    /// Finds the type that reads a stored contract, or returns false.
    /// <para>
    /// False is a normal, expected answer — it is what a rolling deployment looks like from the
    /// old instance's side — and it is why the caller dead-letters with a distinguishable
    /// reason rather than throwing.
    /// </para>
    /// </summary>
    bool TryResolve(string name, int version, [NotNullWhen(true)] out IntegrationEventContract? contract);

    /// <summary>
    /// The module a contract belongs to, used for per-module statistics.
    /// </summary>
    string ModuleFor(Type clrType);

    /// <summary>Whether a type is an integration event this resolver knows.</summary>
    bool IsKnown(Type clrType);

    /// <summary>Convenience for the writer, which always has an instance rather than a type.</summary>
    IntegrationEventContract ContractFor(IIntegrationEvent integrationEvent);
}
