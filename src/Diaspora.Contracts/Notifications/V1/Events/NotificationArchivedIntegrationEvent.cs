using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Notifications.V1.Events;

/// <summary>
/// A member put a message away.
/// <para>
/// Archiving and dismissing are both published through this contract and separated by
/// <see cref="Reason"/>, because they are the same fact about the inbox with different intent:
/// <c>Archived</c> is filing, <c>Dismissed</c> is refusal, <c>Expired</c> is the sweep.
/// </para>
/// </summary>
public sealed record NotificationArchivedIntegrationEvent : IntegrationEvent
{
    public required Guid NotificationId { get; init; }

    public required Guid RecipientUserId { get; init; }

    public required string CategoryKey { get; init; }

    /// <summary><c>Archived</c>, <c>Dismissed</c> or <c>Expired</c>.</summary>
    public required string Reason { get; init; }

    /// <summary>False when the sweep did it rather than the member.</summary>
    public required bool ByRecipient { get; init; }

    public required DateTimeOffset ArchivedAt { get; init; }
}
