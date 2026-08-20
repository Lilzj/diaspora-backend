namespace Diaspora.Modules.Conversations.Contracts.V1.Responses;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// Every record in this file is the client's shape, field for field.
//
// The names come from src/types/conversation.ts in the React Native app and are not backend
// choices: conversationId rather than id, messageId rather than id, delivery rather than status,
// isPinned/isMuted/isArchived/isBlocked as four booleans rather than one state. Renaming any of them
// to match this platform's usual conventions would buy consistency inside the backend and cost the
// client a mapper it would then keep forever — which is exactly the trade B13 refused when it
// renamed RealtimeNotification toward the client rather than away from it.
//
// Three additive fields appear here that the client does not yet read, all of them recorded in the
// frontend compatibility report: entityImageMediaId (because ADR-005 forbids storing URLs),
// avatarMediaId (same reason) and canSend (because the client currently infers it from `state` and
// cannot see the pending-request rule). Appending is safe under B13.1's append-don't-insert rule.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>The frozen snapshot of what a conversation is about.</summary>
public sealed record ConversationContextResponse
{
    /// <summary>The catalogue key — <c>MarketplaceListing</c>, <c>HousingListing</c>, and so on.</summary>
    public required string Type { get; init; }

    public required string EntityId { get; init; }

    public required string EntityTitle { get; init; }

    public string? EntitySubtitle { get; init; }

    /// <summary>
    /// Resolved by <c>IConversationContextResolver</c> when a deployment registers one; null
    /// otherwise. The stored value is <see cref="EntityImageMediaId"/>.
    /// </summary>
    public string? EntityImageUrl { get; init; }

    /// <summary>Additive. The B5 asset the snapshot actually holds. See ADR-005.</summary>
    public Guid? EntityImageMediaId { get; init; }

    public string? EntityPrice { get; init; }

    public string? EntityStatus { get; init; }

    public string? EntityStatusLabel { get; init; }

    /// <summary>Live status, never stored. Null unless a context resolver is registered.</summary>
    public string? CurrentEntityStatus { get; init; }

    public string? CurrentEntityStatusNote { get; init; }

    public required DateTimeOffset SnapshotAt { get; init; }

    public string? DeepLink { get; init; }

    public string? CampaignSource { get; init; }

    public string? ReferralCode { get; init; }

    public string? AppVersion { get; init; }
}

/// <summary>
/// What the platform can say about a participant's standing.
/// <para>
/// Every field is null unless a participant directory is registered. The shape is the client's
/// <c>ConversationTrustProfile</c> so that registering one changes nothing on the client.
/// </para>
/// </summary>
public sealed record ConversationTrustProfileResponse
{
    public required bool IsVerified { get; init; }

    public string? VerificationLabel { get; init; }

    public decimal? Rating { get; init; }

    public int? ReviewCount { get; init; }

    public string? ResponseTime { get; init; }

    public int? ResponseRate { get; init; }

    public string? MemberSince { get; init; }

    public string? ReputationLevel { get; init; }

    public int? TotalHelpfulVotes { get; init; }
}

/// <summary>Somebody in a conversation.</summary>
public sealed record ConversationParticipantResponse
{
    public required Guid UserId { get; init; }

    /// <summary>Null unless a participant directory is registered. See the seam.</summary>
    public string? DisplayName { get; init; }

    public string? AvatarUrl { get; init; }

    /// <summary>Additive. The B5 avatar asset, when a directory supplies one.</summary>
    public Guid? AvatarMediaId { get; init; }

    public required ConversationTrustProfileResponse Trust { get; init; }

    /// <summary>Whether this participant is the caller. Computed per request, never stored.</summary>
    public required bool IsCurrentUser { get; init; }

    /// <summary>Additive. <c>Initiator</c>, <c>Recipient</c> or <c>Observer</c>.</summary>
    public required string Role { get; init; }

    /// <summary>Additive. Present when they have left; the client renders past authors from it.</summary>
    public DateTimeOffset? LeftAt { get; init; }
}

/// <summary>How far a message has got. The client's <c>MessageDeliveryReceipt</c>.</summary>
public sealed record MessageDeliveryResponse
{
    /// <summary>
    /// One of <c>sent</c>, <c>delivered</c>, <c>read</c>, <c>failed</c> — lower case, as the client
    /// spells them. The three client-only states (<c>queued</c>, <c>sending</c>) are never produced
    /// here; a server that claimed to know a message was "sending" would be claiming to observe the
    /// client's own outbox.
    /// </summary>
    public required string Status { get; init; }

    public DateTimeOffset? SentAt { get; init; }

    public DateTimeOffset? DeliveredAt { get; init; }

    public DateTimeOffset? ReadAt { get; init; }
}

/// <summary>An attachment, as the client's <c>MessageAttachment</c> union renders it.</summary>
public sealed record MessageAttachmentResponse
{
    public required Guid AttachmentId { get; init; }

    /// <summary><c>image</c>, <c>document</c>, <c>voice</c>, <c>video</c>, <c>location</c>, <c>contact</c>.</summary>
    public required string Type { get; init; }

    /// <summary>Always <c>completed</c> from the server: an incomplete upload has no message.</summary>
    public required string UploadState { get; init; }

    /// <summary>Additive and load-bearing. The B5 asset; the client fetches bytes through Media.</summary>
    public required Guid MediaAssetId { get; init; }

    public Guid? ThumbnailMediaId { get; init; }

    /// <summary>Populated only where a deployment resolves Media URLs. Null by default.</summary>
    public string? Url { get; init; }

    public string? ThumbnailUrl { get; init; }

    public string? MimeType { get; init; }

    public long? SizeBytes { get; init; }

    public int? Width { get; init; }

    public int? Height { get; init; }

    public int? DurationSeconds { get; init; }

    public string? FileName { get; init; }

    public int? PageCount { get; init; }

    public string? Caption { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>What a message is answering.</summary>
public sealed record ReplyReferenceResponse
{
    public required Guid MessageId { get; init; }

    public required Guid SenderId { get; init; }

    /// <summary>
    /// Null. The client resolves it from <c>Conversation.participants</c>, which is the same answer
    /// B13 reached for typing indicators — this module holds no display names.
    /// </summary>
    public string? SenderName { get; init; }

    public required string PreviewText { get; init; }

    public string? AttachmentType { get; init; }
}

/// <summary>One message.</summary>
public sealed record MessageResponse
{
    public required Guid MessageId { get; init; }

    public required Guid ConversationId { get; init; }

    /// <summary>Empty for a system message — the client renders those without an author.</summary>
    public required Guid SenderId { get; init; }

    public string? SenderName { get; init; }

    public string? SenderAvatarUrl { get; init; }

    /// <summary><c>text</c>, <c>image</c>, <c>voice</c>, <c>document</c>, <c>system</c>, <c>verification</c>, …</summary>
    public required string Type { get; init; }

    /// <summary>Null once withdrawn. The client renders "This message was deleted".</summary>
    public string? Text { get; init; }

    public MessageAttachmentResponse? Attachment { get; init; }

    public ReplyReferenceResponse? ReplyTo { get; init; }

    public bool IsEdited { get; init; }

    public DateTimeOffset? EditedAt { get; init; }

    public bool IsDeleted { get; init; }

    public DateTimeOffset? DeletedAt { get; init; }

    public required MessageDeliveryResponse Delivery { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>The client's optimistic handle, echoed so it can replace its own bubble.</summary>
    public string? LocalId { get; init; }
}

/// <summary>A conversation, as one viewer sees it.</summary>
public sealed record ConversationResponse
{
    public required Guid ConversationId { get; init; }

    /// <summary>
    /// The client's single-field state, computed for this viewer by
    /// <c>ConversationStatePolicy</c> — see it for why storage does not hold this.
    /// </summary>
    public required string State { get; init; }

    public required ConversationContextResponse Context { get; init; }

    public required IReadOnlyList<ConversationParticipantResponse> Participants { get; init; }

    public MessageResponse? LastMessage { get; init; }

    public required int UnreadCount { get; init; }

    public required bool IsPinned { get; init; }

    public required bool IsMuted { get; init; }

    public required bool IsArchived { get; init; }

    public required bool IsBlocked { get; init; }

    public DateTimeOffset? MutedUntil { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public int? PinOrder { get; init; }

    /// <summary>
    /// Additive. Whether this viewer may send right now.
    /// <para>
    /// The client currently disables its composer on <c>state === 'Blocked' || 'Closed'</c>, which
    /// misses the case where an initiator has already used their one message into an unaccepted
    /// request. Sending it explicitly means the composer is right without the client reimplementing
    /// the rule.
    /// </para>
    /// </summary>
    public required bool CanSend { get; init; }
}

/// <summary>
/// A conversation with its first page of history.
/// <para>
/// The client's <c>loadMessages</c> reads <c>data</c> and <c>messages</c> from one response, so the
/// detail endpoint returns both — a second round trip to render the screen it just opened would be a
/// round trip on the platform's most-opened screen.
/// </para>
/// </summary>
public sealed record ConversationDetailResponse
{
    public required ConversationResponse Conversation { get; init; }

    public required IReadOnlyList<MessageResponse> Messages { get; init; }

    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }
}

/// <summary>One page of the inbox.</summary>
public sealed record ConversationPageResponse
{
    public required IReadOnlyList<ConversationResponse> Items { get; init; }

    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }

    /// <summary>The member's total unread across the inbox, so the tab badge needs no second call.</summary>
    public required int TotalUnread { get; init; }

    /// <summary>
    /// Named when a requested filter could not be applied — <c>verifiedOnly</c> with no participant
    /// directory registered. Silently ignoring it would show unverified contacts under a "Verified
    /// only" heading.
    /// </summary>
    public IReadOnlyList<string> UnappliedFilters { get; init; } = [];
}

/// <summary>One page of message history.</summary>
public sealed record MessagePageResponse
{
    public required IReadOnlyList<MessageResponse> Items { get; init; }

    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }
}

/// <summary>A pending request, as the client's <c>ConversationRequest</c>.</summary>
public sealed record ConversationRequestResponse
{
    /// <summary>
    /// The conversation's own identifier.
    /// <para>
    /// The client calls this <c>requestId</c> and posts it to <c>/requests/{id}/accept</c>. There is
    /// no separate request entity: a request <em>is</em> a conversation in
    /// <c>PendingRequest</c> status, so minting a second identifier would create a second thing to
    /// keep in step for no gain. The mapping is recorded in the compatibility report.
    /// </para>
    /// </summary>
    public required Guid RequestId { get; init; }

    public required ConversationParticipantResponse FromUser { get; init; }

    public required ConversationContextResponse Context { get; init; }

    public required string PreviewMessage { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required bool CanAccept { get; init; }

    public required bool CanDecline { get; init; }

    public required bool CanBlock { get; init; }

    public required bool CanReport { get; init; }
}

/// <summary>Somebody the caller has blocked.</summary>
public sealed record BlockedUserResponse
{
    public required Guid UserId { get; init; }

    public string? DisplayName { get; init; }

    public string? AvatarUrl { get; init; }

    public required DateTimeOffset BlockedAt { get; init; }

    public string? Reason { get; init; }
}

/// <summary>The attachment gallery for one conversation.</summary>
public sealed record ConversationMediaResponse
{
    public required IReadOnlyList<MessageAttachmentResponse> Images { get; init; }

    public required IReadOnlyList<MessageAttachmentResponse> Documents { get; init; }

    public required IReadOnlyList<MessageAttachmentResponse> Voice { get; init; }
}

/// <summary>A search match.</summary>
public sealed record ConversationSearchResultResponse
{
    public required Guid ConversationId { get; init; }

    public required string ConversationTitle { get; init; }

    public required string ContextType { get; init; }

    public required string MatchType { get; init; }

    public MessageResponse? Message { get; init; }

    public string? HighlightedText { get; init; }

    public required DateTimeOffset MatchedAt { get; init; }
}

/// <summary>What the search endpoint returns.</summary>
public sealed record ConversationSearchResponse
{
    public required IReadOnlyList<ConversationSearchResultResponse> Results { get; init; }

    /// <summary>Which engine answered. <c>ef</c> for the shipped one.</summary>
    public required string Provider { get; init; }
}

/// <summary>One context type, for the client's filter chips.</summary>
public sealed record ConversationContextTypeResponse
{
    public required string Key { get; init; }

    public required string DisplayName { get; init; }

    public string? Description { get; init; }

    public string? OwningModule { get; init; }

    public required bool RequiresAcceptance { get; init; }

    public required bool AllowsAttachments { get; init; }

    public required int Position { get; init; }
}

/// <summary>The badge, without loading a page of the inbox.</summary>
public sealed record ConversationUnreadCountResponse
{
    public required int Total { get; init; }

    public required DateTimeOffset At { get; init; }
}
