using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Notifications.V1.Events;

/// <summary>
/// A digest was assembled for a member.
/// <para>
/// Generation and delivery are separate facts. A digest is built channel-independently and then
/// handed to whichever channels the member allows, so this event says a summary exists — the
/// delivery events say it went somewhere.
/// </para>
/// </summary>
public sealed record DigestGeneratedIntegrationEvent : IntegrationEvent
{
    public required Guid DigestId { get; init; }

    public required Guid RecipientUserId { get; init; }

    /// <summary><c>Daily</c>, <c>Weekly</c> or <c>Manual</c>.</summary>
    public required string Frequency { get; init; }

    /// <summary>Present when the digest covers one category rather than everything.</summary>
    public string? CategoryKey { get; init; }

    /// <summary>Present when the digest covers one country's knowledge directory.</summary>
    public string? CountryCode { get; init; }

    public required int ItemCount { get; init; }

    public required DateTimeOffset WindowStart { get; init; }

    public required DateTimeOffset WindowEnd { get; init; }

    public required DateTimeOffset GeneratedAt { get; init; }
}
