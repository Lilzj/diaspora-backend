using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Administration;
using Diaspora.Application.Identity.Authorization;
using Diaspora.Application.Identity.Validation;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Application.Identity.Commands.Administration.AccountActions;

/// <summary>
/// A second member of staff confirms a proposed account action, and it is carried out (7 Nov 2026).
/// <para>
/// The confirmer must not be the proposer, must hold the permission the action itself needs —
/// suspending needs <c>identity:users:suspend</c>, closing and reopening <c>identity:users:disable</c>,
/// granting a role <c>identity:roles:assign</c> — and must be above the account in rank. The
/// command's own gate is only "is staff"; the rest depends on the proposal, so the handler checks it.
/// </para>
/// </summary>
[Auditable(Action = "identity.account-action.confirmed")]
[RequirePermission(IdentityPermissions.ReadAnyUser)]
public sealed record ConfirmAccountActionCommand : ICommand<AccountActionProposalResponse>
{
    public required Guid ProposalId { get; init; }

    public string? Note { get; init; }
}

/// <summary>
/// A second member of staff declines a proposed account action; nothing is done to the account. The
/// same people may decline as may confirm, and they must say why.
/// </summary>
[Auditable(Action = "identity.account-action.declined")]
[RequirePermission(IdentityPermissions.ReadAnyUser)]
public sealed record DeclineAccountActionCommand : ICommand<AccountActionProposalResponse>
{
    public required Guid ProposalId { get; init; }

    public required string Note { get; init; }
}

/// <summary>
/// A member of staff takes up, as their own proposal, a suspension the platform raised after a
/// member's third strike (7 Nov 2026), writing the note the member will read. A second member of
/// staff then confirms or declines it. Taking it up needs what proposing a suspension needs.
/// </summary>
[Auditable(Action = "identity.account-action.adopted")]
[RequirePermission(IdentityPermissions.ReadAnyUser)]
public sealed record AdoptAccountActionCommand : ICommand<AccountActionProposalResponse>
{
    public required Guid ProposalId { get; init; }

    /// <summary>Why, in the member of staff's words. The member reads it if it is confirmed.</summary>
    public required string Note { get; init; }
}

/// <summary>The proposer takes back an account action nobody has decided yet.</summary>
[Auditable(Action = "identity.account-action.withdrawn")]
[RequirePermission(IdentityPermissions.ReadAnyUser)]
public sealed record WithdrawAccountActionCommand : ICommand<AccountActionProposalResponse>
{
    public required Guid ProposalId { get; init; }

    public string? Note { get; init; }
}

public sealed class ConfirmAccountActionCommandValidator : IdentityValidator<ConfirmAccountActionCommand>
{
    public ConfirmAccountActionCommandValidator(ILocalizedMessageProvider messages) : base(messages)
    {
        RuleFor(command => command.Note)
            .MaximumLength(1000).WithMessage(Message(IdentityMessageKeys.NotesLength));
    }
}

public sealed class DeclineAccountActionCommandValidator : IdentityValidator<DeclineAccountActionCommand>
{
    public DeclineAccountActionCommandValidator(ILocalizedMessageProvider messages) : base(messages)
    {
        RuleFor(command => command.Note)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.NoteRequired))
            .MaximumLength(1000).WithMessage(Message(IdentityMessageKeys.NotesLength));
    }
}

public sealed class AdoptAccountActionCommandValidator : IdentityValidator<AdoptAccountActionCommand>
{
    public AdoptAccountActionCommandValidator(ILocalizedMessageProvider messages) : base(messages)
    {
        RuleFor(command => (string?)command.Note).MustBeStaffNote(Message);
    }
}

public sealed class WithdrawAccountActionCommandValidator : IdentityValidator<WithdrawAccountActionCommand>
{
    public WithdrawAccountActionCommandValidator(ILocalizedMessageProvider messages) : base(messages)
    {
        RuleFor(command => command.Note)
            .MaximumLength(1000).WithMessage(Message(IdentityMessageKeys.NotesLength));
    }
}

public sealed class ConfirmAccountActionCommandHandler
    : ICommandHandler<ConfirmAccountActionCommand, AccountActionProposalResponse>
{
    private readonly AccountActionProposals _proposals;

    public ConfirmAccountActionCommandHandler(AccountActionProposals proposals)
    {
        _proposals = proposals;
    }

    public Task<Result<AccountActionProposalResponse>> Handle(
        ConfirmAccountActionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _proposals.ConfirmAsync(request.ProposalId, request.Note, cancellationToken);
    }
}

public sealed class DeclineAccountActionCommandHandler
    : ICommandHandler<DeclineAccountActionCommand, AccountActionProposalResponse>
{
    private readonly AccountActionProposals _proposals;

    public DeclineAccountActionCommandHandler(AccountActionProposals proposals)
    {
        _proposals = proposals;
    }

    public Task<Result<AccountActionProposalResponse>> Handle(
        DeclineAccountActionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _proposals.DeclineAsync(request.ProposalId, request.Note, cancellationToken);
    }
}

public sealed class AdoptAccountActionCommandHandler
    : ICommandHandler<AdoptAccountActionCommand, AccountActionProposalResponse>
{
    private readonly AccountActionProposals _proposals;

    public AdoptAccountActionCommandHandler(AccountActionProposals proposals)
    {
        _proposals = proposals;
    }

    public Task<Result<AccountActionProposalResponse>> Handle(
        AdoptAccountActionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _proposals.AdoptAsync(request.ProposalId, request.Note, cancellationToken);
    }
}

public sealed class WithdrawAccountActionCommandHandler
    : ICommandHandler<WithdrawAccountActionCommand, AccountActionProposalResponse>
{
    private readonly AccountActionProposals _proposals;

    public WithdrawAccountActionCommandHandler(AccountActionProposals proposals)
    {
        _proposals = proposals;
    }

    public Task<Result<AccountActionProposalResponse>> Handle(
        WithdrawAccountActionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _proposals.WithdrawAsync(request.ProposalId, request.Note, cancellationToken);
    }
}
