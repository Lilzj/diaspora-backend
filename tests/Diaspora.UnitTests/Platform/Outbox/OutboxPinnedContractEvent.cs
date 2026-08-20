using Diaspora.Contracts.Events;
using Diaspora.Platform.Messaging.Outbox.Serialization;

namespace Diaspora.UnitTests.Platform.Outbox;

/// <summary>
/// An event whose wire identity is pinned independently of its CLR type.
/// <para>
/// The case the attribute exists for: this type could be renamed or moved tomorrow and every
/// row already in the outbox would still resolve, because the durable name is
/// <c>diaspora.test.pinned</c> and not the namespace it happens to live in today.
/// </para>
/// </summary>
[IntegrationEventContract("diaspora.test.pinned", version: 3)]
public sealed record OutboxPinnedContractEvent : IntegrationEvent
{
    public required string Value { get; init; }
}
