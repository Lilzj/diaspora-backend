namespace Diaspora.Modules.Notifications.Contracts.V1.Responses;

/// <summary>
/// One notification, in the shape the mobile client already renders.
/// <para>
/// <b>The first seven properties are the client's existing <c>NotificationItemData</c>, name for
/// name.</b> <c>id</c>, <c>title</c>, <c>body</c>, <c>category</c>, <c>isRead</c>, <c>createdAt</c>
/// and <c>targetRoute</c> are what the Zustand store and the <c>NotificationItem</c> component
/// consume today, so the screen renders against this response with no mapping layer at all.
/// Everything after them is additive and optional to the client.
/// </para>
/// <para>
/// <b><c>category</c> is the display label and <c>categoryKey</c> is the machine key.</b> The client
/// currently puts <c>category</c> straight on screen — "Marketplace", "Verification" — so that
/// property has to stay human-readable. A client that wants to translate the label itself switches
/// on <c>categoryKey</c> and falls back to <c>category</c> for a category that shipped after it.
/// </para>
/// <para>
/// <b><c>createdAt</c> is ISO-8601, where the mock store held a relative string.</b> That is the one
/// place the client adapts: a server cannot know what "10 mins ago" means by the time it is read,
/// and relative time is a rendering decision. The frontend addition ships a formatter beside the
/// hook.
/// </para>
/// </summary>
public sealed record NotificationResponse
{
    public required Guid Id { get; init; }

    public required string Title { get; init; }

    public required string Body { get; init; }

    /// <summary>The human-readable label. What the client renders on the card today.</summary>
    public required string Category { get; init; }

    /// <summary>The stable catalogue key. What a client should switch on.</summary>
    public required string CategoryKey { get; init; }

    public required bool IsRead { get; init; }

    /// <summary>ISO-8601. See the class summary.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>The client route to open. The client's existing property name.</summary>
    public string? TargetRoute { get; init; }

    // ── Additive ────────────────────────────────────────────────────────────────────────────

    /// <summary><c>Critical</c>, <c>High</c>, <c>Normal</c>, <c>Low</c>, <c>Silent</c>.</summary>
    public required string Priority { get; init; }

    /// <summary>The lifecycle state — <c>Delivered</c>, <c>Opened</c>, <c>Archived</c>.</summary>
    public required string Status { get; init; }

    public required bool IsArchived { get; init; }

    public required bool IsPinned { get; init; }

    public required bool IsStarred { get; init; }

    public DateTimeOffset? ReadAt { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>A B5 asset identifier, never a URL. ADR-005.</summary>
    public Guid? ImageMediaId { get; init; }

    /// <summary>Which module the fact came from. The brief's "sender".</summary>
    public required string SourceModule { get; init; }

    /// <summary>What the message is about — <c>job</c>, <c>listing</c>, <c>viewing</c>.</summary>
    public string? EntityType { get; init; }

    public string? EntityId { get; init; }

    public IReadOnlyList<NotificationActionResponse> Actions { get; init; } = [];
}
