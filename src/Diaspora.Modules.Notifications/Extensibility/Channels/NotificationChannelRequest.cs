using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Extensibility.Channels;

/// <summary>
/// Everything a channel is given, and deliberately nothing more.
/// <para>
/// <b>No aggregate, no repository, no preference.</b> A channel receives finished words, a
/// recipient, a link and the endpoints to use. It cannot query the member's settings, cannot mutate
/// the notification, and cannot decide not to send — which is what makes a third-party channel
/// implementation safe to accept.
/// </para>
/// </summary>
public sealed record NotificationChannelRequest
{
    public required NotificationId NotificationId { get; init; }

    public required UserId RecipientUserId { get; init; }

    public required string CategoryKey { get; init; }

    public required NotificationPriority Priority { get; init; }

    public required string TemplateKey { get; init; }

    /// <summary>Already rendered, already translated, already truncated for this channel.</summary>
    public required string Title { get; init; }

    public required string Body { get; init; }

    /// <summary>The BCP-47 tag the words are in, for a transport that needs to declare it.</summary>
    public required string Language { get; init; }

    public required ContentFormat Format { get; init; }

    /// <summary>The client route to open. Already substituted.</summary>
    public string? Route { get; init; }

    /// <summary>A B5 asset identifier, never a URL. ADR-005.</summary>
    public Guid? ImageMediaId { get; init; }

    /// <summary>The actions the card offers, in render order.</summary>
    public IReadOnlyList<ChannelAction> Actions { get; init; } = [];

    /// <summary>
    /// Where to send it, resolved by the dispatcher.
    /// <para>
    /// Empty for the in-app channel, which addresses the inbox itself. A channel that needs an
    /// endpoint and receives none reports <c>NoEndpoint</c> rather than throwing.
    /// </para>
    /// </summary>
    public IReadOnlyList<ChannelEndpoint> Endpoints { get; init; } = [];

    /// <summary>One-based. A channel may use it to vary wording on a retry; none currently does.</summary>
    public required int Attempt { get; init; }
}
