using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Conversations.Application.Mapping;
using Diaspora.Modules.Conversations.Application.Services;
using Diaspora.Modules.Conversations.Contracts.V1.Requests;
using Diaspora.Modules.Conversations.Contracts.V1.Responses;
using Diaspora.Modules.Conversations.Domain.Conversations;
using Diaspora.Modules.Conversations.Domain.Errors;
using Diaspora.Modules.Conversations.Domain.Identifiers;
using Diaspora.Modules.Conversations.Domain.Messages;
using Diaspora.Modules.Conversations.Domain.Policies;
using Diaspora.Modules.Conversations.Domain.ValueObjects;
using Diaspora.Modules.Conversations.Extensibility;
using Diaspora.Modules.Conversations.Observability;
using Diaspora.Modules.Conversations.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Conversations.Application.Messages;

/// <summary>
/// The platform's hottest write path.
/// <para>
/// Two aggregates move together — the message is inserted and the conversation's counters advance —
/// and they must be one unit. A message that committed without its counters leaves an inbox that
/// never shows it; counters that committed without their message leave a badge no read can clear.
/// </para>
/// </summary>
[Authorize(Permission = ConversationPermissions.Participate)]
[Transactional(Reason =
    "Inserts the message and advances every participant's unread counter as one unit. Either half "
    + "alone leaves an inbox that disagrees with the thread.")]
public sealed record SendMessageCommand : ICommand<MessageResponse>
{
    public required Guid ConversationId { get; init; }

    public string? Text { get; init; }

    public Guid? ReplyToMessageId { get; init; }

    public string? LocalId { get; init; }

    public string? Type { get; init; }

    public SendAttachmentRequest? Attachment { get; init; }
}

public sealed class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageCommandValidator()
    {
        RuleFor(command => command.ConversationId).NotEmpty();

        RuleFor(command => command.Text)
            .MaximumLength(ConversationLimits.MaxMessageLength);

        // Emptiness is the domain's rule rather than a validator's, because "text or an attachment"
        // is a statement about a message and the domain is where a message is valid or not. The
        // validator catches shapes; the aggregate catches meaning.
        RuleFor(command => command.LocalId).MaximumLength(64);

        When(command => command.Attachment is not null, () =>
        {
            RuleFor(command => command.Attachment!.MediaAssetId)
                .NotEmpty()
                .WithMessage("An attachment must name a Media asset. This module stores no files.");

            RuleFor(command => command.Attachment!.AttachmentId).NotEmpty();
        });
    }
}

public sealed class SendMessageCommandHandler : ICommandHandler<SendMessageCommand, MessageResponse>
{
    private readonly IConversationRepository _conversations;
    private readonly IConversationMessageRepository _messages;
    private readonly ConversationAccessService _access;
    private readonly IConversationAttachmentValidator _attachments;
    private readonly IEnumerable<IConversationMessageInspector> _inspectors;
    private readonly ISubjectRateLimiter _rateLimiter;
    private readonly IOptionsMonitor<ConversationOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public SendMessageCommandHandler(
        IConversationRepository conversations,
        IConversationMessageRepository messages,
        ConversationAccessService access,
        IConversationAttachmentValidator attachments,
        IEnumerable<IConversationMessageInspector> inspectors,
        ISubjectRateLimiter rateLimiter,
        IOptionsMonitor<ConversationOptions> options,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _conversations = conversations;
        _messages = messages;
        _access = access;
        _attachments = attachments;
        _inspectors = inspectors;
        _rateLimiter = rateLimiter;
        _options = options;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<MessageResponse>> Handle(
        SendMessageCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var loaded = await _access.LoadForWriteAsync(command.ConversationId, cancellationToken);

        if (loaded.IsFailure)
        {
            if (loaded.Error.Code == ConversationErrorCodes.Blocked)
            {
                ConversationTelemetry.WritesRefusedByBlock.Add(1);
            }

            return Result.Failure<MessageResponse>(loaded.Error);
        }

        var conversation = loaded.Value;
        var sender = _access.RequireCaller().Value;
        var now = _clock.UtcNow;

        // Idempotency, keyed on what only the client knows: that two attempts were one press. Checked
        // before the rate limiter, so a client retrying after a timeout is not punished for the
        // network's failure.
        if (!string.IsNullOrWhiteSpace(command.LocalId))
        {
            var already = await _messages.FindByClientIdAsync(
                conversation.Id, command.LocalId, cancellationToken);

            if (already is not null)
            {
                ConversationTelemetry.MessagesDeduplicated.Add(1);

                return Result.Success(ConversationMapper.ToResponse(already));
            }
        }

        // The platform's shared per-subject limiter, so this counter is the same one every other
        // per-member limit uses rather than a second, disagreeing one.
        var lease = await _rateLimiter.AcquireAsync(
            $"conversations:send:{sender.Value:N}",
            _options.CurrentValue.MessagesPerMinute,
            TimeSpan.FromMinutes(1),
            cancellationToken);

        if (!lease.IsAllowed)
        {
            return Result.Failure<MessageResponse>(ConversationErrors.RateLimited);
        }

        var attachment = default(AttachmentReference);

        if (command.Attachment is { } supplied)
        {
            attachment = AttachmentReference.Create(
                ConversationAttachmentId.From(supplied.AttachmentId),
                ConversationWireNames.ParseAttachmentKind(supplied.Type)
                    ?? Domain.Enums.AttachmentKind.Document,
                supplied.MediaAssetId,
                supplied.ThumbnailMediaId,
                supplied.MimeType,
                supplied.SizeBytes,
                supplied.Width,
                supplied.Height,
                supplied.DurationSeconds,
                supplied.FileName,
                supplied.PageCount,
                supplied.Caption);

            var verdict = await _attachments.ValidateAsync(
                conversation, attachment, cancellationToken);

            if (!verdict.Allowed)
            {
                return Result.Failure<MessageResponse>(ConversationErrors.AttachmentsUnavailable);
            }
        }

        var reply = default(MessageReply);

        if (command.ReplyToMessageId is { } replyToId && replyToId != Guid.Empty)
        {
            var target = await _messages.GetInConversationAsync(
                conversation.Id, ConversationMessageId.From(replyToId), cancellationToken);

            // Checked against this conversation, not merely by identifier. A reply that could name a
            // message from another thread would let a caller pull a preview of somebody else's
            // conversation into one they can read.
            if (target is null)
            {
                return Result.Failure<MessageResponse>(ConversationErrors.ReplyTargetNotFound);
            }

            reply = MessageReply.Create(
                target.Id,
                target.SenderId ?? sender,
                target.Body,
                target.Attachment?.Kind);
        }

        var created = ConversationMessage.Send(
            ConversationMessageId.From(_guids.NewSequentialGuid()),
            conversation.Id,
            sender,
            command.Text,
            now,
            ConversationWireNames.ParseMessageKind(command.Type),
            attachment,
            reply,
            command.LocalId);

        if (created.IsFailure)
        {
            return Result.Failure<MessageResponse>(created.Error);
        }

        var message = created.Value;

        foreach (var inspector in _inspectors)
        {
            var verdict = await inspector.InspectAsync(conversation, message, cancellationToken);

            if (!verdict.Allowed)
            {
                return Result.Failure<MessageResponse>(Error.Validation(
                    ConversationErrorCodes.EmptyMessage,
                    verdict.Reason ?? "This message cannot be sent."));
            }
        }

        // The conversation decides who the recipients are — it is the only thing that knows who is
        // in it — and refuses the send if the sender may not write. Both halves inside the same
        // transaction as the insert below.
        var recorded = conversation.RecordMessage(message.Id, sender, now);

        if (recorded.IsFailure)
        {
            return Result.Failure<MessageResponse>(recorded.Error);
        }

        message.DeclareRecipients(recorded.Value);

        _messages.Add(message);
        _conversations.Update(conversation);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        ConversationTelemetry.MessagesSent.Add(
            1, new KeyValuePair<string, object?>("kind", ConversationWireNames.ToWire(message.Kind)));

        return Result.Success(ConversationMapper.ToResponse(message));
    }
}
