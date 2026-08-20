using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Notifications.V1.Events;

/// <summary>
/// A member followed the message somewhere.
/// <para>
/// The click-through signal. It carries the resolved route rather than the destination's data,
/// because this module owns the link and never the thing at the end of it.
/// </para>
/// </summary>
public sealed record NotificationClickedIntegrationEvent : IntegrationEvent
{
    public required Guid NotificationId { get; init; }

    public required Guid RecipientUserId { get; init; }

    public required string CategoryKey { get; init; }

    public required string TemplateKey { get; init; }

    /// <summary>The client route that was followed, already substituted.</summary>
    public string? TargetRoute { get; init; }

    /// <summary>Which action was taken, when the message offered more than one.</summary>
    public string? ActionKey { get; init; }

    public required DateTimeOffset ClickedAt { get; init; }
}
