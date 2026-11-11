using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Models;
using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Security;
using Diaspora.Platform.Operations.Privacy;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Commands.Privacy;

/// <summary>Everything the platform holds that the member gave it.</summary>
// PrivacyController is [Authorize] at the class, and the export is of the caller's own data,
// scoped from the token.
[RequireAuthentication(
    "The caller's own data, scoped from the token. A permission here would gate a member out of " +
    "their own export.")]
public sealed record ExportMyDataQuery : IQuery<PersonalDataExport>;

/// <summary>What erasing this account would do. Changes nothing.</summary>
// Same gate and same scoping as DeleteMyAccountCommand.
[RequireAuthentication("What erasing the caller's own account would do. Reads nothing but their own data.")]
public sealed record PreviewMyAccountDeletionQuery : IQuery<AccountErasureReceipt>;

/// <summary>
/// Erases the caller's account. Irreversible.
/// </summary>
/// <param name="CurrentPassword">
/// Re-authentication, and not ceremony. This is the only irreversible action a member can take on
/// their own account, and a bearer token left on an unlocked phone is enough to take it. Asking for
/// the password means the person pressing the button is the person who owns the account, which is a
/// question a valid token cannot answer on its own.
/// </param>
/// <param name="Confirmation">
/// The member must type the confirmation phrase. Belt and braces on a destructive action, and it is
/// what stops a mis-tapped button from ending an account.
/// </param>
// PrivacyController is [Authorize] at the class. The caller may only ever delete themselves,
// which the handler enforces from the token rather than from the request.
[RequireAuthentication(
    "Erasing the caller's own account. The identity comes from the token, so there is nobody else " +
    "it could erase.")]
public sealed record DeleteMyAccountCommand(
    string CurrentPassword,
    string Confirmation) : ICommand<AccountErasureReceipt>;

/// <summary>Assembles the caller's export.</summary>
public sealed class ExportMyDataQueryHandler
    : IQueryHandler<ExportMyDataQuery, PersonalDataExport>
{
    private readonly IPersonalDataExportService _export;
    private readonly ICurrentIdentityContext _currentIdentity;

    public ExportMyDataQueryHandler(
        IPersonalDataExportService export, ICurrentIdentityContext currentIdentity)
    {
        _export = export;
        _currentIdentity = currentIdentity;
    }

    public async Task<Result<PersonalDataExport>> Handle(
        ExportMyDataQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentIdentity.UserId;

        return userId is null
            ? Result.Failure<PersonalDataExport>(UserErrors.InvalidCredentials)
            : Result.Success(await _export.ExportAsync(userId.Value, cancellationToken));
    }
}

/// <summary>Shows the member what erasure would do, before it does it.</summary>
public sealed class PreviewMyAccountDeletionQueryHandler
    : IQueryHandler<PreviewMyAccountDeletionQuery, AccountErasureReceipt>
{
    private readonly IAccountErasureService _erasure;
    private readonly ICurrentIdentityContext _currentIdentity;

    public PreviewMyAccountDeletionQueryHandler(
        IAccountErasureService erasure, ICurrentIdentityContext currentIdentity)
    {
        _erasure = erasure;
        _currentIdentity = currentIdentity;
    }

    public async Task<Result<AccountErasureReceipt>> Handle(
        PreviewMyAccountDeletionQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentIdentity.UserId;

        return userId is null
            ? Result.Failure<AccountErasureReceipt>(UserErrors.InvalidCredentials)
            : Result.Success(await _erasure.PreviewAsync(userId.Value, cancellationToken));
    }
}

/// <summary>
/// Erases the caller's account across every module that can be asked.
/// <para>
/// <b>One save, so the audit record and the erasure commit together or not at all.</b> The first
/// draft of this handler saved twice — the security event, then the erasure — on the reasoning that
/// the trail should survive a failed erasure. <c>DSP4002</c> refused it, and the analyzer was right:
/// a failure between two saves leaves half an irreversible operation committed, which on this
/// particular operation means a member who is partly erased and has no record saying so.
/// </para>
/// <para>
/// The reasoning it replaced was also wrong on its own terms. <see cref="IAccountErasureService"/>
/// does not throw when a module fails — it collects the failure and reports it on the receipt — so
/// there was no scenario in which the second save was skipped and the first needed to have already
/// happened. A single save is both simpler and the behaviour that was actually wanted.
/// </para>
/// </summary>
public sealed class DeleteMyAccountCommandHandler
    : ICommandHandler<DeleteMyAccountCommand, AccountErasureReceipt>
{
    /// <summary>What the member has to type. Compared case-insensitively; nothing else is accepted.</summary>
    public const string RequiredConfirmation = "DELETE";

    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAccountErasureService _erasure;
    private readonly ICurrentIdentityContext _currentIdentity;
    private readonly ISecurityEventWriter _securityEvents;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteMyAccountCommandHandler(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IAccountErasureService erasure,
        ICurrentIdentityContext currentIdentity,
        ISecurityEventWriter securityEvents,
        IUnitOfWork unitOfWork)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _erasure = erasure;
        _currentIdentity = currentIdentity;
        _securityEvents = securityEvents;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AccountErasureReceipt>> Handle(
        DeleteMyAccountCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentIdentity.UserId;
        if (userId is null)
        {
            return Result.Failure<AccountErasureReceipt>(UserErrors.InvalidCredentials);
        }

        if (!string.Equals(request.Confirmation?.Trim(), RequiredConfirmation, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<AccountErasureReceipt>(Error.Validation(
                "Identity.Account.DeletionNotConfirmed",
                $"Type {RequiredConfirmation} to confirm that this account should be erased."));
        }

        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<AccountErasureReceipt>(UserErrors.NotFound);
        }

        // The same coarse refusal the sign-in paths use. A wrong password here must not distinguish
        // itself from any other credential failure.
        if (_passwordHasher.Verify(user.PasswordHash, request.CurrentPassword ?? string.Empty)
            == PasswordVerificationOutcome.Failed)
        {
            await _securityEvents.WriteAsync(new SecurityEventDraft
            {
                UserId = userId,
                // The nearest existing type. There is no "suspicious activity" member on this enum
                // and inventing one for a single call site would put a value in a catalogue every
                // other consumer already switches on.
                Type = SecurityEventType.LoginFailed,
                Severity = SecurityEventSeverity.Warning,
                Description = "An account erasure was attempted with an incorrect password.",
            }, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Failure<AccountErasureReceipt>(UserErrors.InvalidCredentials);
        }

        await _securityEvents.WriteAsync(new SecurityEventDraft
        {
            UserId = userId,
            Type = SecurityEventType.AccountDisabled,
            Severity = SecurityEventSeverity.Critical,
            Description = "The member requested erasure of their account and all associated data.",
        }, cancellationToken);

        var receipt = await _erasure.EraseAsync(userId.Value, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(receipt);
    }
}
