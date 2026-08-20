using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Organizations;

/// <summary>
/// Removes an asset from an organization page.
/// <para>
/// The reference goes; the asset itself is Media's to keep or reclaim. B5's rule that this module
/// holds references rather than URLs is what makes that separation possible.
/// </para>
/// </summary>
public sealed class DetachOrganizationMediaCommandHandler
    : ICommandHandler<DetachOrganizationMediaCommand, OrganizationResponse>
{
    private readonly IOrganizationRepository _organizations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly OrganizationReadContext _read;

    public DetachOrganizationMediaCommandHandler(
        IOrganizationRepository organizations,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        OrganizationReadContext read)
    {
        _organizations = organizations;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _read = read;
    }

    public async Task<Result<OrganizationResponse>> Handle(
        DetachOrganizationMediaCommand command, CancellationToken cancellationToken)
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

        var detached = organization.DetachMedia(command.MediaId);

        if (detached.IsFailure)
        {
            return Result.Failure<OrganizationResponse>(detached.Error);
        }

        _organizations.Update(organization);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await _read.ToResponseAsync(organization, cancellationToken));
    }
}
