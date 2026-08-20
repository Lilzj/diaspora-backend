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

/// <summary>Puts an organization on the front of the directory, or takes it off. Staff only.</summary>
public sealed class SetOrganizationFeaturedCommandHandler
    : ICommandHandler<SetOrganizationFeaturedCommand, OrganizationResponse>
{
    private readonly IOrganizationRepository _organizations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly OrganizationReadContext _read;
    private readonly IClock _clock;

    public SetOrganizationFeaturedCommandHandler(
        IOrganizationRepository organizations,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        OrganizationReadContext read,
        IClock clock)
    {
        _organizations = organizations;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _read = read;
        _clock = clock;
    }

    public async Task<Result<OrganizationResponse>> Handle(
        SetOrganizationFeaturedCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var staff = _authorization.RequireStaff();

        if (staff.IsFailure)
        {
            return Result.Failure<OrganizationResponse>(staff.Error);
        }

        var organization = await _organizations.GetAsync(
            OrganizationId.From(command.OrganizationId), cancellationToken);

        if (organization is null)
        {
            return Result.Failure<OrganizationResponse>(
                CommunityLifeErrors.OrganizationNotFound);
        }

        organization.SetFeatured(command.Request.IsFeatured, _clock.UtcNow);

        _organizations.Update(organization);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await _read.ToResponseAsync(organization, cancellationToken));
    }
}
