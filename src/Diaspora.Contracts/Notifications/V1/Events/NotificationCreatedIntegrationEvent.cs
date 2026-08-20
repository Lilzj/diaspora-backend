using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Notifications.V1.Events;

/// <summary>
/// A notification was addressed to a member.
/// <para>
/// <b>Created is not sent.</b> It means the routing rule matched, the template rendered and the
/// message exists in the recipient's inbox — nothing about any channel has happened yet. Consumers
/// building "what did we tell this person" audit surfaces key on this; consumers measuring reach
/// key on <see cref="NotificationDeliveredIntegrationEvent"/>.
/// </para>
/// <para>
/// It carries no body. A notification's text is the one thing in this module that is genuinely
/// personal — it names the person who messaged you, the flat you applied for, the interview you
/// were offered — and putting it on a bus every other module reads would make the communication
/// platform the platform's largest unnecessary disclosure surface.
/// </para>
/// </summary>
public sealed record NotificationCreatedIntegrationEvent : IntegrationEvent
{
    public required Guid NotificationId { get; init; }

    public required Guid RecipientUserId { get; init; }

    /// <summary>The catalogue key — <c>jobs</c>, <c>security</c>, <c>housing</c>.</summary>
    public required string CategoryKey { get; init; }

    /// <summary><c>Critical</c>, <c>High</c>, <c>Normal</c>, <c>Low</c>, <c>Silent</c>.</summary>
    public required string Priority { get; init; }

    /// <summary>The template that produced it, so template performance can be measured.</summary>
    public required string TemplateKey { get; init; }

    /// <summary>The routing rule that matched. Null when the notification was raised directly.</summary>
    public string? RuleKey { get; init; }

    /// <summary>The module that published the fact behind this message.</summary>
    public required string SourceModule { get; init; }

    /// <summary>The published contract name that triggered it, or null for a direct dispatch.</summary>
    public string? SourceEventName { get; init; }

    /// <summary>The business record the message is about. Opaque here by design.</summary>
    public string? SubjectId { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
