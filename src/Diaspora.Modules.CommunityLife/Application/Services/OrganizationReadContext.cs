using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Organizations;

namespace Diaspora.Modules.CommunityLife.Application.Services;

/// <summary>
/// Assembles everything an organization response needs, once, for whichever handler asked.
/// <para>
/// <b>Rendering one body means answering three questions that live in three places:</b> what its
/// type is called, whether the caller is a member of it, and whether they may administer it. Every
/// handler that returns an organization needs all three, and written out at each call site the
/// administrator check is the one that would eventually be written wrong — the consequence being an
/// edit button offered to somebody the write path will refuse.
/// </para>
/// <para>
/// <b>There is deliberately no card equivalent here.</b> An organization summary is a pure
/// projection of the aggregate — it names no type and shows no viewer state — so a directory maps
/// straight through <c>OrganizationMapper.ToSummary</c> and issues no queries at all. Routing it
/// through a service that took a cancellation token would invite somebody to add a per-row lookup
/// to it later, which is how a twenty-row page becomes twenty-one round trips.
/// </para>
/// </summary>
public sealed class OrganizationReadContext
{
    private readonly CommunityLifeCatalogResolver _catalog;
    private readonly CommunityLifeAuthorizationService _authorization;

    public OrganizationReadContext(
        CommunityLifeCatalogResolver catalog, CommunityLifeAuthorizationService authorization)
    {
        _catalog = catalog;
        _authorization = authorization;
    }

    /// <summary>The full detail response for one organization.</summary>
    public async Task<OrganizationResponse> ToResponseAsync(
        Organization organization, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(organization);

        var type = await _catalog.RequireTypeAsync(
            organization.OrganizationTypeKey, allowRetired: true, cancellationToken);

        var membership = await _authorization.FindMembershipAsync(
            organization.Id, cancellationToken);

        var isAdministrator = _authorization.IsStaff
            || organization.CreatedByUserId == _authorization.CallerId
            || membership?.HasAtLeast(MembershipRole.Administrator) == true;

        return OrganizationMapper.ToResponse(
            organization,
            type.IsSuccess ? type.Value : null,
            membership,
            isAdministrator);
    }
}
