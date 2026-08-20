using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
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
/// Brings an archived or deleted organization back, as a draft.
/// <para>
/// <b>Loaded through <c>GetIncludingDeletedAsync</c>, and that is the whole reason this handler is
/// not three lines.</b> The global delete filter hides exactly the row a restore exists to find, so
/// an ordinary read returns null, the handler answers "not found", and an undelete that can never
/// undelete anything ships without failing a single test. The named member makes reaching past the
/// filter a visible decision.
/// </para>
/// <para>
/// <b>Both journeys land in draft.</b> Undeleting restores the row; un-archiving moves the
/// lifecycle. A page that has been away wants its contact details and its leadership looked at
/// before members are pointed at it again, so neither route republishes.
/// </para>
/// </summary>
public sealed class RestoreOrganizationCommandHandler
    : ICommandHandler<RestoreOrganizationCommand, OrganizationResponse>
{
    private readonly IOrganizationRepository _organizations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly OrganizationReadContext _read;
    private readonly IClock _clock;

    public RestoreOrganizationCommandHandler(
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
        RestoreOrganizationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var organization = await _organizations.GetIncludingDeletedAsync(
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

        if (organization.IsDeleted)
        {
            organization.Restore();
        }

        // A deleted row was archived on the way out, so this runs for both journeys and is what
        // actually returns the page to somewhere its administrators can work on it.
        if (organization.Status == OrganizationStatus.Archived)
        {
            var restored = organization.RestoreFromArchive(_clock.UtcNow, caller.Value);

            if (restored.IsFailure)
            {
                return Result.Failure<OrganizationResponse>(restored.Error);
            }
        }

        _organizations.Update(organization);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await _read.ToResponseAsync(organization, cancellationToken));
    }
}
