namespace Diaspora.Modules.Notifications.Extensibility.Channels;

/// <summary>
/// A push payload, expressed without any provider's vocabulary.
/// <para>
/// No <c>collapse_key</c>, no <c>apns-priority</c>, no <c>data</c> versus <c>notification</c> split.
/// Those are one provider's spelling of concepts this record already carries, and an adapter
/// translates them. Putting them here would be the vendor lock-in the port exists to prevent.
/// </para>
/// </summary>
public sealed record PushMessage
{
    /// <summary>Tokens to deliver to. One message, many devices.</summary>
    public required IReadOnlyList<ChannelEndpoint> Endpoints { get; init; }

    public required string Title { get; init; }

    public required string Body { get; init; }

    /// <summary>The client route to open when the banner is tapped.</summary>
    public string? Route { get; init; }

    /// <summary>What the client badge should read after this arrives, when the caller knows.</summary>
    public int? BadgeCount { get; init; }

    /// <summary>
    /// Whether the message should wake a sleeping device.
    /// <para>
    /// True only for Critical. Every provider spells this differently and every one of them
    /// throttles it, which is the reason it is a single deliberate flag rather than a priority
    /// passed straight through.
    /// </para>
    /// </summary>
    public required bool IsTimeSensitive { get; init; }

    /// <summary>Correlation the client sends back on a tap, so a click can be attributed.</summary>
    public required Guid NotificationId { get; init; }

    /// <summary>The category, so a client can group banners the way the OS expects.</summary>
    public required string CategoryKey { get; init; }
}
