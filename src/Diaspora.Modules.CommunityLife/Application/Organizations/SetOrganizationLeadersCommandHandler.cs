using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Organizations;

/// <summary>
/// Replaces the leadership list shown on an organization page.
/// <para>
/// <b>A leader is a name on a page, not a membership.</b> A pastor, a chairwoman or a treasurer is
/// often somebody who has never signed into the platform, and requiring an account before they could
/// be named would leave most bodies unable to say who runs them. The optional member identifier
/// links the two where both exist.
/// </para>
/// </summary>
public sealed class SetOrganizationLeadersCommandHandler
    : ICommandHandler<SetOrganizationLeadersCommand, OrganizationResponse>
{
    private readonly IOrganizationRepository _organizations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly OrganizationReadContext _read;
    private readonly IGuidGenerator _guids;

    public SetOrganizationLeadersCommandHandler(
        IOrganizationRepository organizations,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        OrganizationReadContext read,
        IGuidGenerator guids)
    {
        _organizations = organizations;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _read = read;
        _guids = guids;
    }

    public async Task<Result<OrganizationResponse>> Handle(
        SetOrganizationLeadersCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var organization = await _organizations.GetAsync(
            OrganizationId.From(command.OrganizationId), cancellationToken);

        if (organization is null)
        {
            return Result.Failure<OrganizationResponse>(
                CommunityLifeErrors.OrganizationNotFound);
        }

        var caller = await _authorization.RequireOrganizationWriteAsync(
            organization, cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<OrganizationResponse>(caller.Error);
        }

        var leaders = command.Request.Leaders
            .Select((leader, index) => OrganizationLeader.Create(
                _guids.NewSequentialGuid(),
                leader.Name,
                leader.Role,
                leader.MemberUserId,
                leader.PhotoMediaId,
                index))
            .ToList();

        var applied = organization.SetLeaders(leaders, command.Request.BaseRevision);

        if (applied.IsFailure)
        {
            return Result.Failure<OrganizationResponse>(applied.Error);
        }

        _organizations.Update(organization);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await _read.ToResponseAsync(organization, cancellationToken));
    }
}
