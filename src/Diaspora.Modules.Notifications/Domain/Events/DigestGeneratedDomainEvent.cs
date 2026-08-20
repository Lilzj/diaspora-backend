using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.Events;

/// <summary>A summary was assembled for a member.</summary>
public sealed record DigestGeneratedDomainEvent : DomainEvent
{
    public required NotificationDigestId DigestId { get; init; }

    public required UserId RecipientUserId { get; init; }

    public required DigestFrequency Frequency { get; init; }

    public string? CategoryKey { get; init; }

    public string? CountryCode { get; init; }

    public required int ItemCount { get; init; }

    public required DateTimeOffset WindowStart { get; init; }

    public required DateTimeOffset WindowEnd { get; init; }

    public required DateTimeOffset GeneratedAt { get; init; }
}
