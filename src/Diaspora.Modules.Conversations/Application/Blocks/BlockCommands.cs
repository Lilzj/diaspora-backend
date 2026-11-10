using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Conversations.Application.Services;
using Diaspora.Modules.Conversations.Domain.Blocks;
using Diaspora.Modules.Conversations.Domain.Errors;
using Diaspora.Modules.Conversations.Domain.Identifiers;
using Diaspora.Modules.Conversations.Domain.Policies;
using Diaspora.Modules.Conversations.Observability;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.Conversations.Application.Blocks;

/// <summary>
/// One member stops hearing from another.
/// <para>
/// <b>Directional and silent.</b> Only the blocker can undo it, and the blocked member is never
/// told — a block that announced itself would be an invitation to argue about it, which is what
/// somebody blocking is trying to end.
/// </para>
/// </summary>
[RequirePermission(ConversationPermissions.Participate)]
[Transactional(Reason = "Writes the block and raises the event the realtime and inbox paths read.")]
public sealed record BlockUserCommand : ICommand
{
    public required Guid UserId { get; init; }

    public string? Reason { get; init; }
}

public sealed class BlockUserCommandValidator : AbstractValidator<BlockUserCommand>
{
    public BlockUserCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.Reason).MaximumLength(ConversationLimits.MaxBlockReasonLength);
    }
}

public sealed class BlockUserCommandHandler : ICommandHandler<BlockUserCommand>
{
    private readonly IConversationBlockRepository _blocks;
    private readonly ConversationAccessService _access;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public BlockUserCommandHandler(
        IConversationBlockRepository blocks,
        ConversationAccessService access,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _blocks = blocks;
        _access = access;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(BlockUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        var blocker = caller.Value;
        var blocked = UserId.From(command.UserId);

        if (blocker == blocked)
        {
            return Result.Failure(ConversationErrors.SelfBlock);
        }

        // Idempotent. The client blocks from three surfaces — the conversation menu, the request
        // card, the profile — and a second press must not be a conflict the member has to read.
        var existing = await _blocks.FindAsync(blocker, blocked, cancellationToken);

        if (existing is not null)
        {
            return Result.Success();
        }

        var created = ConversationBlock.Create(
            ConversationBlockId.From(_guids.NewSequentialGuid()),
            blocker,
            blocked,
            command.Reason,
            _clock.UtcNow);

        if (created.IsFailure)
        {
            return Result.Failure(created.Error);
        }

        _blocks.Add(created.Value);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        ConversationTelemetry.Blocked.Add(1);

        return Result.Success();
    }
}

// ════════════════════════════════════════════════════════════════════════════════════════════════

[RequirePermission(ConversationPermissions.Participate)]
[Transactional(Reason = "Removes the block and raises the event that re-enables the conversation.")]
public sealed record UnblockUserCommand : ICommand
{
    public required Guid UserId { get; init; }
}

public sealed class UnblockUserCommandValidator : AbstractValidator<UnblockUserCommand>
{
    public UnblockUserCommandValidator() => RuleFor(command => command.UserId).NotEmpty();
}

public sealed class UnblockUserCommandHandler : ICommandHandler<UnblockUserCommand>
{
    private readonly IConversationBlockRepository _blocks;
    private readonly ConversationAccessService _access;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public UnblockUserCommandHandler(
        IConversationBlockRepository blocks, ConversationAccessService access,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _blocks = blocks;
        _access = access;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UnblockUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        // Only the blocker's own row. Looking it up directionally is what stops a blocked member
        // lifting a block that was imposed on them — the failure that would make blocking useless
        // to the person who needed it.
        var block = await _blocks.FindAsync(
            caller.Value, UserId.From(command.UserId), cancellationToken);

        if (block is null)
        {
            // Idempotent, and it also refuses to disclose. A caller who is not the blocker gets the
            // same success as one whose block was already lifted.
            return Result.Success();
        }

        block.AnnounceRemoval(_clock.UtcNow);

        _blocks.Remove(block);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
