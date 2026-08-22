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
/// Replaces the guides an organization points its readers at.
/// <para>
/// <b>This closes a path that was built and never connected.</b> Every piece of it already existed:
/// <see cref="Organization.SetGuideReferences"/> with its limit and its de-duplication,
/// <see cref="OrganizationGuideReference"/>, the <c>community_life_organization_guides</c> table, the
/// persistence configuration, and <see cref="CommunityLifeGuideResolver.ForOrganizationAsync"/>.
/// Neither the domain method nor the resolver method had a single caller anywhere in the solution,
/// and no request contract carried a guide identifier for an organization — so
/// <c>OrganizationResponse.Guides</c> was a field that could never be non-empty for any organization
/// on the platform, and the client's Guides tab could only ever render its empty state.
/// </para>
/// <para>
/// The two sibling aggregates were wired: <c>CreateEventCommandHandler</c> resolves and sets guides
/// on an event, and <c>PublishHousingListingCommandHandler</c> does the same for a listing.
/// Organizations were the one of the three left unconnected.
/// </para>
/// <para>
/// A separate <c>PUT</c> rather than a field on create or update, following <c>SetLeaders</c> and
/// <c>SetOperatingHours</c>: an organization's guide list is edited over its life, by somebody who
/// may not be the person who filled in its description.
/// </para>
/// <para>
/// <b>Unknown and unpublished guides are dropped rather than refused</b>, which is the resolver's
/// rule and is deliberate. The consequence is that a 200 does not mean every identifier was
/// attached, so the response returns the organization with its resolved list and a caller that cares
/// should compare.
/// </para>
/// </summary>
public sealed class SetOrganizationGuidesCommandHandler
    : ICommandHandler<SetOrganizationGuidesCommand, OrganizationResponse>
{
    private readonly IOrganizationRepository _organizations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly CommunityLifeGuideResolver _guides;
    private readonly OrganizationReadContext _read;

    public SetOrganizationGuidesCommandHandler(
        IOrganizationRepository organizations,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        CommunityLifeGuideResolver guides,
        OrganizationReadContext read)
    {
        _organizations = organizations;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _guides = guides;
        _read = read;
    }

    public async Task<Result<OrganizationResponse>> Handle(
        SetOrganizationGuidesCommand command, CancellationToken cancellationToken)
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

        var references = await _guides.ForOrganizationAsync(
            command.Request.GuideIds, cancellationToken);

        var applied = organization.SetGuideReferences(references);

        if (applied.IsFailure)
        {
            return Result.Failure<OrganizationResponse>(applied.Error);
        }

        _organizations.Update(organization);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await _read.ToResponseAsync(organization, cancellationToken));
    }
}
