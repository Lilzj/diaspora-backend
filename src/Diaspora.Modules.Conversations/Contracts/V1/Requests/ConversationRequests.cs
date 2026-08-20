namespace Diaspora.Modules.Conversations.Contracts.V1.Requests;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// The bodies the client already posts, unchanged.
//
// Every field here appears in src/lib/conversation-hub.ts or src/store/conversation.ts today. Two
// are additive and optional — attachment, and the context's entityImageMediaId — so a client that
// sends neither behaves exactly as it does against its own mock handlers.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>The context snapshot a client supplies when opening a conversation.</summary>
public sealed record ConversationContextRequest
{
    /// <summary>A catalogue key. Refused if the catalogue does not hold it.</summary>
    public required string Type { get; init; }

    public required string EntityId { get; init; }

    public string? EntityTitle { get; init; }

    public string? EntitySubtitle { get; init; }

    /// <summary>
    /// A B5 asset identifier.
    /// <para>
    /// The client's field is <c>entityImageUrl</c>. A URL is not accepted: ADR-005 makes a media
    /// reference the only thing this platform stores, and a caller-supplied URL would be an
    /// unvalidated external address rendered inside the app. The mapping is in the compatibility
    /// report; a client that sends only <c>entityImageUrl</c> gets a conversation with no image
    /// rather than an error.
    /// </para>
    /// </summary>
    public Guid? EntityImageMediaId { get; init; }

    public string? EntityPrice { get; init; }

    public string? EntityStatus { get; init; }

    public string? EntityStatusLabel { get; init; }

    public string? DeepLink { get; init; }

    public string? CampaignSource { get; init; }

    public string? ReferralCode { get; init; }

    public string? AppVersion { get; init; }
}

/// <summary>
/// <c>POST /conversations</c>. Find-or-create, which is what the client's single entry point needs.
/// </summary>
public sealed record CreateConversationRequest
{
    public required Guid RecipientId { get; init; }

    public required ConversationContextRequest Context { get; init; }

    /// <summary>Sent with the conversation when present. The client's <c>initialMessage</c>.</summary>
    public string? InitialMessage { get; init; }
}

/// <summary>An attachment on an outgoing message.</summary>
public sealed record SendAttachmentRequest
{
    /// <summary>The client's own handle, minted before the upload finished. Echoed back.</summary>
    public required Guid AttachmentId { get; init; }

    /// <summary><c>image</c>, <c>document</c>, <c>voice</c>, <c>video</c>, <c>location</c>, <c>contact</c>.</summary>
    public required string Type { get; init; }

    /// <summary>The B5 asset. Required — this module stores no bytes.</summary>
    public required Guid MediaAssetId { get; init; }

    public Guid? ThumbnailMediaId { get; init; }

    public string? MimeType { get; init; }

    public long? SizeBytes { get; init; }

    public int? Width { get; init; }

    public int? Height { get; init; }

    public int? DurationSeconds { get; init; }

    public string? FileName { get; init; }

    public int? PageCount { get; init; }

    public string? Caption { get; init; }
}

/// <summary><c>POST /conversations/{id}/messages</c>.</summary>
public sealed record SendMessageRequest
{
    public string? Text { get; init; }

    public Guid? ReplyToMessageId { get; init; }

    /// <summary>
    /// The client's optimistic identifier.
    /// <para>
    /// Stored, not merely echoed: a client that retries after a timeout gets the message it already
    /// created rather than a second one. That is the only idempotency this endpoint has and it is
    /// the client that supplies the key, which is correct — only the client knows the two attempts
    /// were one press.
    /// </para>
    /// </summary>
    public string? LocalId { get; init; }

    /// <summary>Optional. Inferred from the attachment when absent.</summary>
    public string? Type { get; init; }

    public SendAttachmentRequest? Attachment { get; init; }
}

/// <summary><c>PUT /conversations/{id}/messages/{messageId}</c>.</summary>
public sealed record EditMessageRequest
{
    public required string Text { get; init; }
}

/// <summary><c>POST /conversations/{id}/read</c>.</summary>
public sealed record MarkConversationReadRequest
{
    /// <summary>Read through this message. Absent means through the latest.</summary>
    public Guid? ThroughMessageId { get; init; }
}

/// <summary>
/// <c>PUT /conversations/{id}/mute</c>.
/// <para>
/// <c>duration: null</c> is an unmute — that is what the client sends from its unmute path, and
/// refusing it would leave the client with no way to unmute at all.
/// </para>
/// </summary>
public sealed record MuteConversationRequest
{
    /// <summary><c>8h</c>, <c>24h</c>, <c>7d</c>, <c>forever</c>, or null to unmute.</summary>
    public string? Duration { get; init; }
}

/// <summary>
/// <c>PUT /conversations/{id}/pin</c>.
/// <para>
/// The client posts an empty body to pin and <c>{ unpin: true }</c> to unpin, through one route.
/// </para>
/// </summary>
public sealed record PinConversationRequest
{
    public bool Unpin { get; init; }
}

/// <summary><c>POST /conversations/block</c>.</summary>
public sealed record BlockUserRequest
{
    public required Guid UserId { get; init; }

    public string? Reason { get; init; }
}

/// <summary><c>POST /conversations/search</c>.</summary>
public sealed record SearchConversationsRequest
{
    public required string Query { get; init; }

    public int? Limit { get; init; }
}

/// <summary><c>POST /conversations/{id}/report</c>.</summary>
public sealed record ReportConversationRequest
{
    public string? Reason { get; init; }

    public Guid? MessageId { get; init; }
}
