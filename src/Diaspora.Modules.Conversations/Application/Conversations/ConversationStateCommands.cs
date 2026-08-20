using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Conversations.Application.Mapping;
using Diaspora.Modules.Conversations.Application.Services;
using Diaspora.Modules.Conversations.Domain.Conversations;
using Diaspora.Modules.Conversations.Domain.Identifiers;
using Diaspora.Modules.Conversations.Domain.Messages;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.Conversations.Application.Conversations;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// Archive / unarchive / mute / pin — the four per-participant flags.
//
// All four take read access rather than write access. Filing a conversation is a decision about
// your own inbox, and refusing it because somebody blocked you would leave a blocked conversation
// permanently un-archivable — stuck at the top of the inbox of the person who most wants it gone.
// ════════════════════════════════════════════════════════════════════════════════════════════════

[Authorize(Permission = ConversationPermissions.Participate)]
[Transactional(Reason = "Moves one participant row and raises the event the client renders from.")]
public sealed record ArchiveConversationCommand : ICommand
{
    public required Guid ConversationId { get; init; }

    /// <summary>False is unarchive. One command, because they are one toggle to the client.</summary>
    public bool Archive { get; init; } = true;
}

public sealed class ArchiveConversationCommandValidator
    : AbstractValidator<ArchiveConversationCommand>
{
    public ArchiveConversationCommandValidator() =>
        RuleFor(command => command.ConversationId).NotEmpty();
}

public sealed class ArchiveConversationCommandHandler : ICommandHandler<ArchiveConversationCommand>
{
    private readonly IConversationRepository _conversations;
    private readonly ConversationAccessService _access;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public ArchiveConversationCommandHandler(
        IConversationRepository conversations, ConversationAccessService access,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _conversations = conversations;
        _access = access;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        ArchiveConversationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var loaded = await _access.LoadAsync(command.ConversationId, cancellationToken);

        if (loaded.IsFailure)
        {
            return Result.Failure(loaded.Error);
        }

        var caller = _access.RequireCaller().Value;
        var now = _clock.UtcNow;

        var result = command.Archive
            ? loaded.Value.Archive(caller, now)
            : loaded.Value.Unarchive(caller, now);

        if (result.IsFailure)
        {
            return result;
        }

        _conversations.Update(loaded.Value);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

// ════════════════════════════════════════════════════════════════════════════════════════════════

[Authorize(Permission = ConversationPermissions.Participate)]
[Transactional(Reason = "Moves one participant row and raises the event the client renders from.")]
public sealed record MuteConversationCommand : ICommand
{
    public required Guid ConversationId { get; init; }

    /// <summary>
    /// <c>8h</c>, <c>24h</c>, <c>7d</c>, <c>forever</c>, or null.
    /// <para>
    /// Null is an unmute. That is what the client sends from its unmute path — <c>{ duration: null }</c>
    /// through the same route — and refusing it would leave the client with no way to unmute at all.
    /// </para>
    /// </summary>
    public string? Duration { get; init; }
}

public sealed class MuteConversationCommandValidator : AbstractValidator<MuteConversationCommand>
{
    public MuteConversationCommandValidator() =>
        RuleFor(command => command.ConversationId).NotEmpty();
}

public sealed class MuteConversationCommandHandler : ICommandHandler<MuteConversationCommand>
{
    private readonly IConversationRepository _conversations;
    private readonly ConversationAccessService _access;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public MuteConversationCommandHandler(
        IConversationRepository conversations, ConversationAccessService access,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _conversations = conversations;
        _access = access;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        MuteConversationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var loaded = await _access.LoadAsync(command.ConversationId, cancellationToken);

        if (loaded.IsFailure)
        {
            return Result.Failure(loaded.Error);
        }

        var caller = _access.RequireCaller().Value;
        var now = _clock.UtcNow;
        var duration = ConversationWireNames.ParseMuteDuration(command.Duration);

        // Unrecognised is treated as absent, which is an unmute. The safe direction: a stray value
        // restores notifications rather than silencing somebody indefinitely.
        var result = duration is { } parsed
            ? loaded.Value.Mute(caller, ConversationWireNames.MutedUntil(parsed, now), now)
            : loaded.Value.Unmute(caller, now);

        if (result.IsFailure)
        {
            return result;
        }

        _conversations.Update(loaded.Value);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

// ════════════════════════════════════════════════════════════════════════════════════════════════

[Authorize(Permission = ConversationPermissions.Participate)]
[Transactional(Reason =
    "Counts the member's existing pins and writes the new one together. Counting outside the "
    + "transaction would let two devices each see four pins and create a sixth.")]
public sealed record PinConversationCommand : ICommand
{
    public required Guid ConversationId { get; init; }

    public bool Unpin { get; init; }
}

public sealed class PinConversationCommandValidator : AbstractValidator<PinConversationCommand>
{
    public PinConversationCommandValidator() =>
        RuleFor(command => command.ConversationId).NotEmpty();
}

public sealed class PinConversationCommandHandler : ICommandHandler<PinConversationCommand>
{
    private readonly IConversationRepository _conversations;
    private readonly ConversationAccessService _access;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public PinConversationCommandHandler(
        IConversationRepository conversations, ConversationAccessService access,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _conversations = conversations;
        _access = access;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        PinConversationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var loaded = await _access.LoadAsync(command.ConversationId, cancellationToken);

        if (loaded.IsFailure)
        {
            return Result.Failure(loaded.Error);
        }

        var caller = _access.RequireCaller().Value;
        var now = _clock.UtcNow;

        if (command.Unpin)
        {
            var unpinned = loaded.Value.Unpin(caller, now);

            if (unpinned.IsFailure)
            {
                return unpinned;
            }

            _conversations.Update(loaded.Value);

            return Result.Success();
        }

        // The limit spans conversations, so the aggregate cannot see it and the count is supplied.
        // The client enforces five locally and shows "Max 5 pins"; a server that permitted six would
        // let a second device create a state the first refuses to render.
        var pinned = await _conversations.CountPinnedAsync(caller, cancellationToken);

        var result = loaded.Value.Pin(caller, pinned, now);

        if (result.IsFailure)
        {
            return result;
        }

        _conversations.Update(loaded.Value);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

// ════════════════════════════════════════════════════════════════════════════════════════════════
// Mark read
// ════════════════════════════════════════════════════════════════════════════════════════════════

[Authorize(Permission = ConversationPermissions.Participate)]
[Transactional(Reason =
    "Clears the reader's unread counter and latches read timestamps onto the messages the receipt "
    + "covers. Split, the sender's ticks and the reader's badge would disagree.")]
public sealed record MarkConversationReadCommand : ICommand
{
    public required Guid ConversationId { get; init; }

    /// <summary>Read through this message. Absent means through the latest.</summary>
    public Guid? ThroughMessageId { get; init; }
}

public sealed class MarkConversationReadCommandValidator
    : AbstractValidator<MarkConversationReadCommand>
{
    public MarkConversationReadCommandValidator() =>
        RuleFor(command => command.ConversationId).NotEmpty();
}

public sealed class MarkConversationReadCommandHandler
    : ICommandHandler<MarkConversationReadCommand>
{
    private readonly IConversationRepository _conversations;
    private readonly IConversationMessageRepository _messages;
    private readonly ConversationAccessService _access;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public MarkConversationReadCommandHandler(
        IConversationRepository conversations,
        IConversationMessageRepository messages,
        ConversationAccessService access,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _conversations = conversations;
        _messages = messages;
        _access = access;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        MarkConversationReadCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var loaded = await _access.LoadAsync(command.ConversationId, cancellationToken);

        if (loaded.IsFailure)
        {
            return Result.Failure(loaded.Error);
        }

        var conversation = loaded.Value;
        var caller = _access.RequireCaller().Value;
        var now = _clock.UtcNow;

        var through = command.ThroughMessageId is { } id && id != Guid.Empty
            ? ConversationMessageId.From(id)
            : conversation.LastMessageId;

        var marked = conversation.MarkRead(caller, through, now);

        if (marked.IsFailure)
        {
            return Result.Failure(marked.Error);
        }

        // The durable half of the receipt. A relayed receipt is lost if the sender was offline, so
        // the rows have to carry it too — otherwise a sender who reconnects sees two ticks on a
        // message the other party read yesterday.
        if (through is not null)
        {
            var boundary = await _messages.GetInConversationAsync(
                conversation.Id, through, cancellationToken);

            if (boundary is not null)
            {
                var pending = await _messages.ListPendingReceiptsAsync(
                    conversation.Id, caller, boundary.SentAt, cancellationToken);

                foreach (var message in pending)
                {
                    message.MarkRead(now);
                    _messages.Update(message);
                }
            }
        }

        _conversations.Update(conversation);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
