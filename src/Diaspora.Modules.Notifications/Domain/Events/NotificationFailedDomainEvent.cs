using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.Events;

/// <summary>
/// A channel gave up.
/// <para>
/// Raised on the final failure only. A retry is not news, and a handler that ran on every rung
/// would multiply an outage by the retry count.
/// </para>
/// </summary>
public sealed record NotificationFailedDomainEvent : DomainEvent
{
    public required NotificationId NotificationId { get; init; }

    public required UserId RecipientUserId { get; init; }

    public required string CategoryKey { get; init; }

    public required NotificationChannelKind Channel { get; init; }

    public required int Attempts { get; init; }

    public required string ReasonCode { get; init; }

    public required bool IsPermanent { get; init; }

    public required DateTimeOffset FailedAt { get; init; }
}
