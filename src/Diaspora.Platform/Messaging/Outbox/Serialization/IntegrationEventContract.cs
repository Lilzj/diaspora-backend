namespace Diaspora.Platform.Messaging.Outbox.Serialization;

/// <summary>
/// The published identity of an integration event type: a name, a version and the CLR type
/// that currently implements them.
/// </summary>
/// <param name="Name">
/// The stable contract name. Defaults to the namespace-qualified type name, which for every
/// event in <c>Diaspora.Contracts</c> is already a published, reviewed string.
/// </param>
/// <param name="Version">The contract version. Starts at 1 and increments on a breaking change.</param>
/// <param name="ClrType">The type that deserializes this contract in this deployment.</param>
/// <param name="Module">The module that owns the contract, derived from its namespace.</param>
public sealed record IntegrationEventContract(
    string Name,
    int Version,
    Type ClrType,
    string Module);
