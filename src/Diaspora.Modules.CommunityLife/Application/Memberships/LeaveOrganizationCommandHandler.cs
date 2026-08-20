using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Memberships;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Memberships;

/// <summary>
/// The caller walks away from an organization.
/// <para>
/// Refused for the owner, by the aggregate: a body with no owner is one nobody can publish, edit or
/// hand on. The owner transfers first, which is a deliberate act with its own audit entry.
/// </para>
/// </summary>
public sealed class LeaveOrganizationCommandHandler
    : ICommandHandler<LeaveOrganizationCommand>
{
    private readonly IOrganizationMembershipRepository _memberships;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public LeaveOrganizationCommandHandler(
        IOrganizationMembershipRepository memberships,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock)
    {
        _memberships = memberships;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result> Handle(
        LeaveOrganizationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        var membership = await _memberships.FindCurrentAsync(
            OrganizationId.From(command.OrganizationId), caller.Value, cancellationToken);

        if (membership is null)
        {
            return Result.Failure(CommunityLifeErrors.MembershipNotFound);
        }

        var left = membership.Leave(_guids.NewSequentialGuid(), _clock.UtcNow);

        if (left.IsFailure)
        {
            return left;
        }

        _memberships.Update(membership);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
