using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Administration;
using Diaspora.Application.Identity.Mapping;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Users;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Diaspora.Application.Identity.Commands.Administration.AssignRole;

/// <summary>
/// Changes a member's role, and then signs them out.
/// <para>
/// <b>The sign-out is not tidiness, it is the point.</b> Permissions travel inside the
/// access token as claims, so a member demoted from Moderator keeps every moderator
/// permission until their current token expires — they could work the queue for the rest of
/// the hour. Revoking their sessions forces a fresh token, and the fresh token carries the
/// role they now have. A promotion is revoked for the same reason in reverse: the new
/// permissions only exist once a new token is issued.
/// </para>
/// <para>
/// <b>What this refuses.</b> Nobody may change their own role — an admin cannot promote
/// themselves to SuperAdmin, and cannot lock themselves out by accident either. Nobody acts on an
/// account at or above their own rank (7 Nov 2026). Only a SuperAdmin may remove SuperAdmin. And
/// granting Admin or SuperAdmin is not done here at all: it is a proposal a second member of staff
/// confirms.
/// </para>
/// </summary>
public sealed class AssignRoleCommandHandler : ICommandHandler<AssignRoleCommand, AdminMemberResponse>
{
    private readonly StaffAuthority _authority;
    private readonly AccountActionExecutor _executor;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<AssignRoleCommandHandler> _logger;

    public AssignRoleCommandHandler(
        StaffAuthority authority,
        AccountActionExecutor executor,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<AssignRoleCommandHandler> logger)
    {
        _authority = authority;
        _executor = executor;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<AdminMemberResponse>> Handle(
        AssignRoleCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (RoleGrants.Parse(request.Role) is not { } role)
        {
            return Result.Failure<AdminMemberResponse>(IdentityAdministrationErrors.UnknownRole(request.Role));
        }

        var targetId = UserId.From(request.UserId);

        if (_authority.CallerId is { } callerId && callerId == targetId)
        {
            return Result.Failure<AdminMemberResponse>(IdentityAdministrationErrors.CannotChangeOwnRole);
        }

        if (RoleGrants.NeedsSecondStaff(role))
        {
            return Result.Failure<AdminMemberResponse>(IdentityAdministrationErrors.RoleGrantNeedsSecondStaff);
        }

        var resolved = await _authority.ResolveAsync(targetId, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result.Failure<AdminMemberResponse>(resolved.Error);
        }

        var (actor, user) = resolved.Value;

        // Removing the highest rung is the one thing an Admin cannot do: a platform where Admin can
        // unmake SuperAdmin has no top. (The rank rule already refuses it; this says why.)
        if (user.Role == UserRole.SuperAdmin && actor.Role != UserRole.SuperAdmin)
        {
            return Result.Failure<AdminMemberResponse>(IdentityAdministrationErrors.SuperAdminOnly);
        }

        if (user.Role == role)
        {
            // Already there. Answering with the member rather than an error keeps a retried
            // request idempotent, and nobody is signed out for a change that did not happen.
            return Result.Success(AdminMemberMapper.ToResponse(user));
        }

        var previous = user.Role;
        var now = _clock.UtcNow;

        var revoked = await _executor.ChangeRoleAsync(
            user, role, request.Reason!.Trim(), new StaffAttribution(actor.Id), now, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Role for user {UserId} changed from {PreviousRole} to {NewRole} by {ChangedBy} ({ChangedByRole}); {Revoked} sessions revoked.",
            user.Id.Value,
            previous.Name,
            role.Name,
            actor.Id.Value,
            _currentUser.Role,
            revoked);

        return Result.Success(AdminMemberMapper.ToResponse(user));
    }
}
