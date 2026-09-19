using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Mapping;
using Diaspora.Application.Identity.Models;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Security;
using Diaspora.Domain.Identity.Sessions;
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
/// <b>Two things this refuses.</b> Nobody may change their own role — an admin cannot
/// promote themselves to SuperAdmin, and cannot lock themselves out by accident either. And
/// only a SuperAdmin may grant or remove SuperAdmin, so the highest rung is not reachable
/// from the one below it.
/// </para>
/// </summary>
public sealed class AssignRoleCommandHandler : ICommandHandler<AssignRoleCommand, AdminMemberResponse>
{
    private readonly IUserRepository _users;
    private readonly ISessionRevocationService _revocation;
    private readonly ISecurityEventWriter _securityEvents;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentIdentityContext _currentIdentity;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<AssignRoleCommandHandler> _logger;

    public AssignRoleCommandHandler(
        IUserRepository users,
        ISessionRevocationService revocation,
        ISecurityEventWriter securityEvents,
        ICurrentUser currentUser,
        ICurrentIdentityContext currentIdentity,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<AssignRoleCommandHandler> logger)
    {
        _users = users;
        _revocation = revocation;
        _securityEvents = securityEvents;
        _currentUser = currentUser;
        _currentIdentity = currentIdentity;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<AdminMemberResponse>> Handle(
        AssignRoleCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var role = UserRole.All.FirstOrDefault(
            candidate => string.Equals(candidate.Name, request.Role, StringComparison.OrdinalIgnoreCase));

        if (role is null)
        {
            return Result.Failure<AdminMemberResponse>(IdentityAdministrationErrors.UnknownRole(request.Role));
        }

        var targetId = UserId.From(request.UserId);

        if (_currentIdentity.UserId is { } callerId && callerId == targetId)
        {
            return Result.Failure<AdminMemberResponse>(IdentityAdministrationErrors.CannotChangeOwnRole);
        }

        var user = await _users.GetByIdAsync(targetId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<AdminMemberResponse>(UserErrors.NotFound);
        }

        // Granting or removing the highest rung is the one thing an Admin cannot do, in
        // either direction: a platform where Admin can appoint SuperAdmin has no top.
        var touchesSuperAdmin = role == UserRole.SuperAdmin || user.Role == UserRole.SuperAdmin;

        if (touchesSuperAdmin
            && !string.Equals(_currentUser.Role, UserRole.SuperAdmin.Name, StringComparison.Ordinal))
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

        user.AssignRole(role);
        _users.Update(user);

        var revoked = await _revocation.RevokeAllAsync(
            user.Id, SessionRevocationReason.AccountStateChanged, now, cancellationToken);

        await _securityEvents.WriteAsync(new SecurityEventDraft
        {
            UserId = user.Id,
            Type = SecurityEventType.RoleAssigned,
            Severity = SecurityEventSeverity.Warning,
            Description = $"Role changed from {previous.Name} to {role.Name}"
                + (string.IsNullOrWhiteSpace(request.Reason) ? "." : $": {request.Reason}"),
            Metadata = new Dictionary<string, string>
            {
                ["previousRole"] = previous.Name,
                ["newRole"] = role.Name,
                ["sessionsRevoked"] = revoked.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["changedBy"] = _currentIdentity.UserId?.Value.ToString() ?? "unknown"
            }
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Role for user {UserId} changed from {PreviousRole} to {NewRole} by {ChangedBy}; {Revoked} sessions revoked.",
            user.Id,
            previous.Name,
            role.Name,
            _currentIdentity.UserId,
            revoked);

        return Result.Success(AdminMemberMapper.ToResponse(user));
    }
}
