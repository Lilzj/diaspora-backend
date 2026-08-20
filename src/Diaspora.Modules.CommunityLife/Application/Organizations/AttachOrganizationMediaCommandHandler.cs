using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Organizations;

/// <summary>
/// Attaches an uploaded asset to an organization page.
/// <para>
/// <b>The claim goes to Media after the save.</b> An asset that is attached here but unclaimed there
/// is a page with a picture the sweep will eventually reclaim; a save rolled back because Media was
/// briefly unavailable is an administrator's work lost. The first is recoverable and the second is
/// not.
/// </para>
/// <para>
/// <b>Verification evidence cannot be attached through this endpoint.</b> It reaches the aggregate
/// only through the verification submission, which is what keeps a registration certificate out of
/// a public gallery.
/// </para>
/// </summary>
public sealed class AttachOrganizationMediaCommandHandler
    : ICommandHandler<AttachOrganizationMediaCommand, OrganizationResponse>
{
    private readonly IOrganizationRepository _organizations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly CommunityLifeMediaClaimer _media;
    private readonly OrganizationReadContext _read;
    private readonly IClock _clock;

    public AttachOrganizationMediaCommandHandler(
        IOrganizationRepository organizations,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        CommunityLifeMediaClaimer media,
        OrganizationReadContext read,
        IClock clock)
    {
        _organizations = organizations;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _media = media;
        _read = read;
        _clock = clock;
    }

    public async Task<Result<OrganizationResponse>> Handle(
        AttachOrganizationMediaCommand command, CancellationToken cancellationToken)
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

        var role = CommunityLifeSharedMapper.ParseEnum<CommunityLifeMediaRole>(
            command.Request.Role) ?? CommunityLifeMediaRole.Gallery;

        if (role == CommunityLifeMediaRole.VerificationEvidence)
        {
            return Result.Failure<OrganizationResponse>(CommunityLifeErrors.MediaInvalid);
        }

        var attached = organization.AttachMedia(
            command.Request.MediaId, role, command.Request.Caption, _clock.UtcNow);

        if (attached.IsFailure)
        {
            return Result.Failure<OrganizationResponse>(attached.Error);
        }

        _organizations.Update(organization);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _media.ClaimForOrganizationAsync(
            organization.Id.Value,
            command.Request.MediaId,
            role.ToString(),
            cancellationToken: cancellationToken);

        return Result.Success(await _read.ToResponseAsync(organization, cancellationToken));
    }
}
