using Diaspora.Modules.Conversations.Contracts.V1.Responses;
using Diaspora.Modules.Conversations.Domain.Blocks;
using Diaspora.Modules.Conversations.Domain.Conversations;
using Diaspora.Modules.Conversations.Domain.Enums;
using Diaspora.Modules.Conversations.Domain.Messages;
using Diaspora.Modules.Conversations.Domain.Policies;
using Diaspora.Modules.Conversations.Domain.ValueObjects;
using Diaspora.Modules.Conversations.Extensibility;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Conversations.Application.Mapping;

/// <summary>
/// Turns aggregates into the client's shapes.
/// <para>
/// <b>Every method takes a page, never a row, and that is the whole design of this class.</b> A
/// conversation response needs three things this module does not hold — display names, live entity
/// status, and whether a block stands — and each of them comes from a port. Mapping one conversation
/// at a time would ask each port once per row, which is the N+1 this phase is required not to ship:
/// a thirty-row inbox would be ninety round trips. So the caller builds a
/// <see cref="ConversationRenderContext"/> once for the whole page and every row reads from it.
/// </para>
/// <para>
/// The methods that need nothing external — a message, an attachment — are static and take no
/// context, so a handler that only has messages does not have to build one.
/// </para>
/// </summary>
public sealed class ConversationMapper
{
    private readonly IConversationParticipantDirectory _directory;
    private readonly IConversationContextResolver _contextResolver;
    private readonly IConversationBlockRepository _blocks;
    private readonly IClock _clock;

    public ConversationMapper(
        IConversationParticipantDirectory directory,
        IConversationContextResolver contextResolver,
        IConversationBlockRepository blocks,
        IClock clock)
    {
        _directory = directory;
        _contextResolver = contextResolver;
        _blocks = blocks;
        _clock = clock;
    }

    /// <summary>
    /// Resolves everything a page of conversations needs from outside this module, in three calls
    /// regardless of how many rows there are.
    /// </summary>
    public async Task<ConversationRenderContext> BuildContextAsync(
        UserId viewer,
        IReadOnlyCollection<Conversation> conversations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        ArgumentNullException.ThrowIfNull(conversations);

        var participantIds = conversations
            .SelectMany(conversation => conversation.Participants)
            .Select(participant => participant.UserId)
            .Append(viewer)
            .Distinct()
            .ToList();

        var counterparts = participantIds.Where(id => id != viewer).ToList();

        var references = conversations
            .Select(conversation => conversation.Context.ToReference())
            .DistinctBy(reference => reference.ToString())
            .ToList();

        var profiles = participantIds.Count == 0
            ? new Dictionary<Guid, ConversationParticipantProfile>()
            : await _directory.ResolveAsync(participantIds, cancellationToken);

        var statuses = references.Count == 0
            ? new Dictionary<string, ConversationContextStatus>()
            : await _contextResolver.ResolveAsync(references, cancellationToken);

        var blocked = counterparts.Count == 0
            ? new HashSet<Guid>()
            : await _blocks.FilterBlockedAsync(viewer, counterparts, cancellationToken);

        return new ConversationRenderContext(viewer, profiles, statuses, blocked, _clock.UtcNow);
    }

    /// <summary>One conversation, as this viewer sees it.</summary>
    public static ConversationResponse ToResponse(
        Conversation conversation,
        ConversationRenderContext context,
        ConversationMessage? lastMessage = null)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        ArgumentNullException.ThrowIfNull(context);

        var participant = conversation.ParticipantFor(context.Viewer);

        var isBlocked = conversation.ActiveParticipants
            .Any(other => other.UserId != context.Viewer
                          && context.BlockedUserIds.Contains(other.UserId.Value));

        var state = ConversationStatePolicy.Resolve(conversation, participant, isBlocked, context.Now);

        return new ConversationResponse
        {
            ConversationId = conversation.Id.Value,
            State = ConversationWireNames.ToWire(state),
            Context = ToContextResponse(conversation, context),
            Participants =
            [
                .. conversation.Participants.Select(row => ToParticipantResponse(row, context))
            ],
            LastMessage = lastMessage is null ? null : ToResponse(lastMessage),
            UnreadCount = participant?.UnreadCount ?? 0,
            IsPinned = participant?.IsPinned ?? false,

            // The stored flag, not the time-aware answer. The client renders a bell from `isMuted`
            // and reads `mutedUntil` to say until when; reporting false on an expired mute would
            // hide the fact that a mute is still recorded and about to be lifted.
            IsMuted = participant?.IsMuted ?? false,
            IsArchived = participant?.IsArchived ?? false,
            IsBlocked = isBlocked,
            MutedUntil = participant?.MutedUntil,
            CreatedAt = conversation.CreatedAt,
            UpdatedAt = conversation.UpdatedAt,
            PinOrder = participant?.PinOrder,
            CanSend = ConversationStatePolicy.CanSend(conversation, participant, isBlocked)
        };
    }

    /// <summary>One message. Needs nothing from outside the module.</summary>
    public static MessageResponse ToResponse(ConversationMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new MessageResponse
        {
            MessageId = message.Id.Value,
            ConversationId = message.ConversationId.Value,

            // Guid.Empty for a system message. The client keys its bubbles on senderId and compares
            // it to the current user; a null would break that comparison in TypeScript, where
            // `undefined === undefined` for two different system messages.
            SenderId = message.SenderId?.Value ?? Guid.Empty,
            Type = ConversationWireNames.ToWire(message.Kind),
            Text = message.Body,
            Attachment = message.Attachment is null ? null : ToResponse(message.Attachment, message.SentAt),
            ReplyTo = message.ReplyTo is null ? null : ToResponse(message.ReplyTo),
            IsEdited = message.IsEdited,
            EditedAt = message.EditedAt,
            IsDeleted = message.IsWithdrawn,
            DeletedAt = message.WithdrawnAt,
            Delivery = new MessageDeliveryResponse
            {
                Status = ConversationWireNames.ToWire(message.Status),
                SentAt = message.SentAt,
                DeliveredAt = message.DeliveredAt,
                ReadAt = message.ReadAt
            },
            CreatedAt = message.SentAt,
            LocalId = message.ClientMessageId
        };
    }

    /// <summary>A pending request, as the client's request inbox renders it.</summary>
    public static ConversationRequestResponse ToRequestResponse(
        Conversation conversation,
        ConversationRenderContext context,
        ConversationMessage? openingMessage)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        ArgumentNullException.ThrowIfNull(context);

        var initiator = conversation.Participants
            .FirstOrDefault(participant => participant.Role == ParticipantRole.Initiator);

        return new ConversationRequestResponse
        {
            // The conversation's own identifier. There is no separate request entity — see the
            // response record for why minting a second one would be a second thing to keep in step.
            RequestId = conversation.Id.Value,
            FromUser = initiator is null
                ? EmptyParticipant()
                : ToParticipantResponse(initiator, context),
            Context = ToContextResponse(conversation, context),
            PreviewMessage = openingMessage?.Body ?? string.Empty,
            CreatedAt = conversation.CreatedAt,
            CanAccept = true,
            CanDecline = true,
            CanBlock = true,
            CanReport = true
        };
    }

    public static BlockedUserResponse ToResponse(
        ConversationBlock block, ConversationRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(context);

        var profile = context.ProfileFor(block.BlockedUserId);

        return new BlockedUserResponse
        {
            UserId = block.BlockedUserId.Value,
            DisplayName = profile?.DisplayName,
            AvatarUrl = null,
            BlockedAt = block.BlockedAt,
            Reason = block.Reason
        };
    }

    public static MessageAttachmentResponse ToResponse(
        AttachmentReference attachment, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(attachment);

        return new MessageAttachmentResponse
        {
            AttachmentId = attachment.AttachmentId.Value,
            Type = ConversationWireNames.ToWire(attachment.Kind),

            // Always completed. A message exists only once its upload did — an in-flight upload is
            // the client's own optimistic state and the server has never seen it.
            UploadState = "completed",
            MediaAssetId = attachment.MediaAssetId,
            ThumbnailMediaId = attachment.ThumbnailMediaId,
            MimeType = attachment.MimeType,
            SizeBytes = attachment.SizeBytes,
            Width = attachment.Width,
            Height = attachment.Height,
            DurationSeconds = attachment.DurationSeconds,
            FileName = attachment.FileName,
            PageCount = attachment.PageCount,
            Caption = attachment.Caption,
            CreatedAt = createdAt
        };
    }

    private static ReplyReferenceResponse ToResponse(MessageReply reply) =>
        new()
        {
            MessageId = reply.MessageId.Value,
            SenderId = reply.SenderId.Value,

            // Null by design. The client resolves it from conversation.participants — the same
            // answer B13 reached for typing indicators, for the same reason.
            SenderName = null,
            PreviewText = reply.PreviewText,
            AttachmentType = reply.AttachmentKind is { } kind
                ? ConversationWireNames.ToWire(kind)
                : null
        };

    private static ConversationContextResponse ToContextResponse(
        Conversation conversation, ConversationRenderContext context)
    {
        var snapshot = conversation.Context;
        var status = context.StatusFor(snapshot.ToReference());

        return new ConversationContextResponse
        {
            Type = snapshot.Type.Key,
            EntityId = snapshot.EntityId,
            EntityTitle = snapshot.EntityTitle,
            EntitySubtitle = snapshot.EntitySubtitle,

            // Resolved, never stored. See ADR-005 and the compatibility report.
            EntityImageUrl = status?.ImageUrl,
            EntityImageMediaId = snapshot.EntityImageMediaId,
            EntityPrice = snapshot.EntityPrice,
            EntityStatus = snapshot.EntityStatus,
            EntityStatusLabel = snapshot.EntityStatusLabel,
            CurrentEntityStatus = status?.Status,
            CurrentEntityStatusNote = status?.Note,
            SnapshotAt = snapshot.SnapshotAt,
            DeepLink = snapshot.DeepLink,
            CampaignSource = conversation.Metadata.CampaignSource,
            ReferralCode = conversation.Metadata.ReferralCode,
            AppVersion = conversation.Metadata.AppVersion
        };
    }

    private static ConversationParticipantResponse ToParticipantResponse(
        ConversationParticipant participant, ConversationRenderContext context)
    {
        var profile = context.ProfileFor(participant.UserId);

        return new ConversationParticipantResponse
        {
            UserId = participant.UserId.Value,
            DisplayName = profile?.DisplayName,
            AvatarUrl = null,
            AvatarMediaId = profile?.AvatarMediaId,
            Trust = new ConversationTrustProfileResponse
            {
                IsVerified = profile?.IsVerified ?? false,
                VerificationLabel = profile?.VerificationLabel,
                Rating = profile?.Rating,
                ReviewCount = profile?.ReviewCount,
                ResponseTime = profile?.ResponseTime,
                ResponseRate = profile?.ResponseRate,
                MemberSince = profile?.MemberSince,
                ReputationLevel = profile?.ReputationLevel,
                TotalHelpfulVotes = profile?.TotalHelpfulVotes
            },
            IsCurrentUser = participant.UserId == context.Viewer,
            Role = ConversationWireNames.ToWire(participant.Role),
            LeftAt = participant.LeftAt
        };
    }

    private static ConversationParticipantResponse EmptyParticipant() =>
        new()
        {
            UserId = Guid.Empty,
            Trust = new ConversationTrustProfileResponse { IsVerified = false },
            IsCurrentUser = false,
            Role = ConversationWireNames.ToWire(ParticipantRole.Initiator)
        };
}

/// <summary>
/// Everything a page of conversations needs from outside this module, resolved once.
/// <para>
/// Built by <see cref="ConversationMapper.BuildContextAsync"/> and passed down. Holding it as a
/// record rather than four loose arguments is what makes it obvious at every call site that the
/// resolution already happened — a mapper that took an <c>IConversationParticipantDirectory</c>
/// directly would invite somebody to call it per row.
/// </para>
/// </summary>
public sealed record ConversationRenderContext(
    UserId Viewer,
    IReadOnlyDictionary<Guid, ConversationParticipantProfile> Profiles,
    IReadOnlyDictionary<string, ConversationContextStatus> Statuses,
    IReadOnlySet<Guid> BlockedUserIds,
    DateTimeOffset Now)
{
    public ConversationParticipantProfile? ProfileFor(UserId userId) =>
        userId is not null && Profiles.TryGetValue(userId.Value, out var profile) ? profile : null;

    public ConversationContextStatus? StatusFor(ConversationReference reference) =>
        reference is not null && Statuses.TryGetValue(reference.ToString(), out var status)
            ? status
            : null;
}
