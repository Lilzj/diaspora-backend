using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Organizations;

/// <summary>
/// Soft-deletes an organization page.
/// <para>
/// <b>Archived first, then deleted.</b> The archival is what raises the event consumers de-index on;
/// deleting alone would hide the row locally while every search index the platform runs went on
/// showing it. The two together are one transaction, so there is no window in which a page is gone
/// here and present everywhere else.
/// </para>
/// </summary>
public sealed class DeleteOrganizationCommandHandler
    : ICommandHandler<DeleteOrganizationCommand>
{
    private readonly IOrganizationRepository _organizations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly IClock _clock;

    public DeleteOrganizationCommandHandler(
        IOrganizationRepository organizations,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        IClock clock)
    {
        _organizations = organizations;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result> Handle(
        DeleteOrganizationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var organization = await _organizations.GetAsync(
            OrganizationId.From(command.OrganizationId), cancellationToken);

        if (organization is null)
        {
            return Result.Failure(CommunityLifeErrors.OrganizationNotFound);
        }

        var caller = await _authorization.RequireOrganizationWriteAsync(
            organization, cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        var now = _clock.UtcNow;

        // Refused only where the lifecycle itself refuses it — a published body has to be archived
        // deliberately, and the aggregate says so rather than this handler guessing.
        var archived = organization.Archive(now, caller.Value, reason: null);

        if (archived.IsFailure)
        {
            return archived;
        }

        organization.MarkDeleted(now, caller.Value.Value.ToString());

        _organizations.Update(organization);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
