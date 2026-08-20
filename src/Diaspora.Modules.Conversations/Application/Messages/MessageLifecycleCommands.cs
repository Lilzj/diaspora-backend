using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Conversations.Application.Mapping;
using Diaspora.Modules.Conversations.Application.Services;
using Diaspora.Modules.Conversations.Contracts.V1.Responses;
using Diaspora.Modules.Conversations.Domain.Conversations;
using Diaspora.Modules.Conversations.Domain.Errors;
using Diaspora.Modules.Conversations.Domain.Identifiers;
using Diaspora.Modules.Conversations.Domain.Messages;
using Diaspora.Modules.Conversations.Domain.Policies;
using Diaspora.Modules.Conversations.Observability;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.Conversations.Application.Messages;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// Edit
// ════════════════════════════════════════════════════════════════════════════════════════════════

[Authorize(Permission = ConversationPermissions.Participate)]
[Transactional(Reason = "One aggregate, but the edit must not commit without its domain event.")]
public sealed record EditMessageCommand : ICommand<MessageResponse>
{
    public required Guid ConversationId { get; init; }

    public required Guid MessageId { get; init; }

    public required string Text { get; init; }
}

public sealed class EditMessageCommandValidator : AbstractValidator<EditMessageCommand>
{
    public EditMessageCommandValidator()
    {
        RuleFor(command => command.ConversationId).NotEmpty();
        RuleFor(command => command.MessageId).NotEmpty();
        RuleFor(command => command.Text).NotEmpty().MaximumLength(ConversationLimits.MaxMessageLength);
    }
}

public sealed class EditMessageCommandHandler : ICommandHandler<EditMessageCommand, MessageResponse>
{
    private readonly IConversationMessageRepository _messages;
    private readonly ConversationAccessService _access;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public EditMessageCommandHandler(
        IConversationMessageRepository messages, ConversationAccessService access,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _messages = messages;
        _access = access;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<MessageResponse>> Handle(
        EditMessageCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // The conversation is loaded first even though the message is what changes: it is the
        // conversation that decides whether the caller may see any of this. Loading the message by
        // identifier and checking afterwards would put somebody else's message in memory first.
        var loaded = await _access.LoadForWriteAsync(command.ConversationId, cancellationToken);

        if (loaded.IsFailure)
        {
            return Result.Failure<MessageResponse>(loaded.Error);
        }

        var caller = _access.RequireCaller().Value;

        var message = await _messages.GetInConversationAsync(
            loaded.Value.Id, ConversationMessageId.From(command.MessageId), cancellationToken);

        if (message is null)
        {
            return Result.Failure<MessageResponse>(ConversationErrors.MessageNotFound);
        }

        var edited = message.Edit(caller, command.Text, _clock.UtcNow);

        if (edited.IsFailure)
        {
            return Result.Failure<MessageResponse>(edited.Error);
        }

        _messages.Update(message);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        ConversationTelemetry.MessagesEdited.Add(1);

        return Result.Success(ConversationMapper.ToResponse(message));
    }
}

// ════════════════════════════════════════════════════════════════════════════════════════════════
// Delete
// ════════════════════════════════════════════════════════════════════════════════════════════════

[Authorize(Permission = ConversationPermissions.Participate)]
[Transactional(Reason =
    "Withdraws the message and un-counts it from every recipient who had not read it. Either half "
    + "alone leaves a badge counting a message that no longer exists.")]
public sealed record DeleteMessageCommand : ICommand<MessageResponse>
{
    public required Guid ConversationId { get; init; }

    public required Guid MessageId { get; init; }
}

public sealed class DeleteMessageCommandValidator : AbstractValidator<DeleteMessageCommand>
{
    public DeleteMessageCommandValidator()
    {
        RuleFor(command => command.ConversationId).NotEmpty();
        RuleFor(command => command.MessageId).NotEmpty();
    }
}

public sealed class DeleteMessageCommandHandler
    : ICommandHandler<DeleteMessageCommand, MessageResponse>
{
    private readonly IConversationRepository _conversations;
    private readonly IConversationMessageRepository _messages;
    private readonly ConversationAccessService _access;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteMessageCommandHandler(
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

    public async Task<Result<MessageResponse>> Handle(
        DeleteMessageCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Read access, not write access: a member who was blocked after sending something must still
        // be able to withdraw it. Refusing would leave them permanently unable to take back their
        // own words because somebody else made a decision afterwards.
        var loaded = await _access.LoadAsync(command.ConversationId, cancellationToken);

        if (loaded.IsFailure)
        {
            return Result.Failure<MessageResponse>(loaded.Error);
        }

        var conversation = loaded.Value;
        var caller = _access.RequireCaller().Value;

        var message = await _messages.GetInConversationAsync(
            conversation.Id, ConversationMessageId.From(command.MessageId), cancellationToken);

        if (message is null)
        {
            return Result.Failure<MessageResponse>(ConversationErrors.MessageNotFound);
        }

        var now = _clock.UtcNow;
        var deleted = message.Delete(caller, now);

        if (deleted.IsFailure)
        {
            return Result.Failure<MessageResponse>(deleted.Error);
        }

        conversation.WithdrawMessage(caller, message.Id, now);

        _messages.Update(message);
        _conversations.Update(conversation);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        ConversationTelemetry.MessagesDeleted.Add(1);

        return Result.Success(ConversationMapper.ToResponse(message));
    }
}
