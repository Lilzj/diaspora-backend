using Diaspora.Application.Identity.Abstractions;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Users;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Administration;

/// <summary>The member of staff acting, and the account they act on, both loaded.</summary>
public sealed record StaffActors(User Actor, User Target);

/// <summary>
/// Who may act on whose account.
/// <para>
/// <b>The owner's rule (7 Nov 2026).</b> A member of staff acts on an account only when their rank
/// is strictly above the account's — member &lt; Moderator &lt; Admin &lt; SuperAdmin — and never on
/// their own. Until then a Moderator could suspend an Admin, and anybody could act on themselves.
/// It applies to suspending, closing, reopening, reinstating, signing out and changing a role, and
/// to the second member of staff who confirms a proposal as much as to the first.
/// </para>
/// <para>
/// <b>The caller's rank is read from their account, not their token.</b> A token carries the role
/// the caller had when it was issued; the account carries the role they have.
/// </para>
/// </summary>
public sealed class StaffAuthority
{
    private readonly IUserRepository _users;
    private readonly ICurrentIdentityContext _currentIdentity;
    private readonly IPermissionService _permissions;

    public StaffAuthority(
        IUserRepository users,
        ICurrentIdentityContext currentIdentity,
        IPermissionService permissions)
    {
        _users = users;
        _currentIdentity = currentIdentity;
        _permissions = permissions;
    }

    /// <summary>The caller's account id, if the request has one.</summary>
    public UserId? CallerId => _currentIdentity.UserId;

    /// <summary>
    /// Loads the caller and the account and applies the rule: an unknown account is not found, the
    /// caller's own account and an account at or above their rank are refused.
    /// </summary>
    public async Task<Result<StaffActors>> ResolveAsync(
        UserId targetUserId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(targetUserId);

        if (_currentIdentity.UserId is not { } actorId)
        {
            return Result.Failure<StaffActors>(IdentityAdministrationErrors.ActorUnknown);
        }

        var target = await _users.GetByIdAsync(targetUserId, cancellationToken);
        if (target is null)
        {
            return Result.Failure<StaffActors>(UserErrors.NotFound);
        }

        if (actorId == target.Id)
        {
            return Result.Failure<StaffActors>(IdentityAdministrationErrors.CannotActOnOwnAccount);
        }

        var actor = await _users.GetByIdAsync(actorId, cancellationToken);
        if (actor is null)
        {
            return Result.Failure<StaffActors>(IdentityAdministrationErrors.ActorUnknown);
        }

        var allowed = MayActOn(actor, target);

        return allowed.IsFailure
            ? Result.Failure<StaffActors>(allowed.Error)
            : Result.Success(new StaffActors(actor, target));
    }

    /// <summary>The rule itself, for two accounts already loaded.</summary>
    public static Result MayActOn(User actor, User target)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(target);

        if (actor.Id == target.Id)
        {
            return Result.Failure(IdentityAdministrationErrors.CannotActOnOwnAccount);
        }

        return actor.Role.Outranks(target.Role)
            ? Result.Success()
            : Result.Failure(IdentityAdministrationErrors.TargetNotBelowYourRank(target.Role.Name));
    }

    /// <summary>Whether the caller holds a permission, as the request's claims say.</summary>
    public Task<bool> HoldsAsync(string permission, CancellationToken cancellationToken) =>
        _permissions.HasPermissionAsync(permission, cancellationToken);
}
