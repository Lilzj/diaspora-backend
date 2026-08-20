using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Conversations.Application.Mapping;
using Diaspora.Modules.Conversations.Application.Services;
using Diaspora.Modules.Conversations.Contracts.V1.Responses;
using Diaspora.Modules.Conversations.Domain.Conversations;
using Diaspora.Modules.Conversations.Domain.Errors;
using Diaspora.Modules.Conversations.Domain.Messages;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.Conversations.Application.Requests;

/// <summary>
/// The recipient of a message request accepts it.
/// <para>
/// A request is a conversation in <c>PendingRequest</c> status, so accepting is a status change
/// rather than a creation. The client posts what it calls a <c>requestId</c>; it is the conversation
/// identifier — see <c>ConversationRequestResponse</c> for why there is no second one.
/// </para>
/// </summary>
[Authorize(Permission = ConversationPermissions.Participate)]
[Transactional(Reason = "Moves the conversation to Active and raises the event both parties render.")]
public sealed record AcceptConversationRequestCommand : ICommand<ConversationResponse>
{
    public required Guid RequestId { get; init; }
}

public sealed class AcceptConversationRequestCommandValidator
    : AbstractValidator<AcceptConversationRequestCommand>
{
    public AcceptConversationRequestCommandValidator() =>
        RuleFor(command => command.RequestId).NotEmpty();
}

public sealed class AcceptConversationRequestCommandHandler
    : ICommandHandler<AcceptConversationRequestCommand, ConversationResponse>
{
    private readonly IConversationRepository _conversations;
    private readonly IConversationMessageRepository _messages;
    private readonly ConversationAccessService _access;
    private readonly ConversationMapper _mapper;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public AcceptConversationRequestCommandHandler(
        IConversationRepository conversations,
        IConversationMessageRepository messages,
        ConversationAccessService access,
        ConversationMapper mapper,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _conversations = conversations;
        _messages = messages;
        _access = access;
        _mapper = mapper;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ConversationResponse>> Handle(
        AcceptConversationRequestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var loaded = await _access.LoadAsync(command.RequestId, cancellationToken);

        if (loaded.IsFailure)
        {
            // The request-shaped error rather than the conversation-shaped one, because that is what
            // the caller asked about. Both are 404 and neither says whether it exists.
            return Result.Failure<ConversationResponse>(ConversationErrors.RequestNotFound);
        }

        var conversation = loaded.Value;
        var caller = _access.RequireCaller().Value;

        // The aggregate refuses anybody but the recipient. An initiator who could accept their own
        // request would bypass the whole mechanism with one extra call.
        var accepted = conversation.AcceptRequest(caller, _clock.UtcNow);

        if (accepted.IsFailure)
        {
            return Result.Failure<ConversationResponse>(accepted.Error);
        }

        _conversations.Update(conversation);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var context = await _mapper.BuildContextAsync(caller, [conversation], cancellationToken);

        var last = conversation.LastMessageId is null
            ? null
            : await _messages.GetAsync(conversation.LastMessageId, cancellationToken);

        return Result.Success(ConversationMapper.ToResponse(conversation, context, last));
    }
}

// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// The recipient declines.
/// <para>
/// <b>Closes the conversation rather than deleting it, and does not tell the initiator it was
/// declined.</b> The initiator sees a conversation that has gone quiet, which is the same thing they
/// would see if the recipient simply never replied — and that ambiguity is the protection. A
/// "declined" signal would tell somebody persistent that they had been noticed.
/// </para>
/// </summary>
[Authorize(Permission = ConversationPermissions.Participate)]
[Transactional(Reason = "Closes the conversation and raises its closure event as one unit.")]
public sealed record DeclineConversationRequestCommand : ICommand
{
    public required Guid RequestId { get; init; }
}

public sealed class DeclineConversationRequestCommandValidator
    : AbstractValidator<DeclineConversationRequestCommand>
{
    public DeclineConversationRequestCommandValidator() =>
        RuleFor(command => command.RequestId).NotEmpty();
}

public sealed class DeclineConversationRequestCommandHandler
    : ICommandHandler<DeclineConversationRequestCommand>
{
    private readonly IConversationRepository _conversations;
    private readonly ConversationAccessService _access;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public DeclineConversationRequestCommandHandler(
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
        DeclineConversationRequestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var loaded = await _access.LoadAsync(command.RequestId, cancellationToken);

        if (loaded.IsFailure)
        {
            return Result.Failure(ConversationErrors.RequestNotFound);
        }

        var conversation = loaded.Value;

        if (!conversation.IsPendingRequest)
        {
            return Result.Failure(ConversationErrors.RequestNotFound);
        }

        var closed = conversation.Close("declined", _clock.UtcNow);

        if (closed.IsFailure)
        {
            return closed;
        }

        _conversations.Update(conversation);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
