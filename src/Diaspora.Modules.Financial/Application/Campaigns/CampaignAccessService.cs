using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Extensibility.Campaigns;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Campaigns;

/// <summary>
/// Answers "may this caller run this campaign", once.
/// <para>
/// <b>Two rules, and the second is somebody else's to answer.</b> A personal campaign belongs to the
/// member who created it, and the verified bar was already cleared by the permission on the command.
/// An organization campaign additionally needs <c>canManageCampaigns</c> inside that body — a
/// capability decided by the module that owns the role edge and asked for through a seam, never
/// reimplemented here. Two places that can both decide who runs an organization's fundraising is one
/// place too many, and the second is the one that forgets a rule.
/// </para>
/// <para>
/// <b>The organizer is not automatically the manager of an organization campaign.</b> Somebody who
/// created a campaign for a body and then lost the capability inside it stops being able to drive
/// it, which is what losing the capability means. The check is made on every write rather than
/// captured at creation.
/// </para>
/// </summary>
public sealed class CampaignAccessService
{
    private readonly FinancialAuthorizationService _authorization;
    private readonly ICampaignOrganizerAuthority _organizations;

    public CampaignAccessService(
        FinancialAuthorizationService authorization, ICampaignOrganizerAuthority organizations)
    {
        _authorization = authorization;
        _organizations = organizations;
    }

    public Result<Guid> RequireCaller() => _authorization.RequireUserId();

    public Guid? CallerOrNull() => _authorization.UserIdOrNull();

    /// <summary>Whether the caller may create a campaign on this body's behalf.</summary>
    public async Task<Result> RequireOrganizerAsync(
        Guid? organizationId, Guid callerUserId, CancellationToken cancellationToken)
    {
        if (organizationId is not { } body)
        {
            // A personal campaign. The verified bar is the permission on the command, already met.
            return Result.Success();
        }

        var permitted =
            await _organizations.CanManageCampaignsAsync(body, callerUserId, cancellationToken);

        return permitted
            ? Result.Success()
            : Result.Failure(Error.Forbidden(
                FinancialErrorCodes.CampaignOrganizerForbidden,
                "You do not manage fundraising for that organisation."));
    }

    /// <summary>
    /// Whether the caller may drive an existing campaign.
    /// <para>
    /// Personal: the member who created it. Organization: anybody who currently holds the
    /// capability, which is what makes a campaign survive its creator leaving the body.
    /// </para>
    /// </summary>
    public async Task<bool> CanManageAsync(
        Campaign campaign, Guid? callerUserId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        if (callerUserId is not { } caller)
        {
            return false;
        }

        if (campaign.OrganizationId is { } body)
        {
            return await _organizations.CanManageCampaignsAsync(body, caller, cancellationToken);
        }

        return campaign.IsOrganizedBy(caller);
    }

    /// <summary>The body's display name for the organizer label, or null.</summary>
    public Task<string?> DescribeOrganizationAsync(
        Guid organizationId, CancellationToken cancellationToken) =>
        _organizations.DescribeAsync(organizationId, cancellationToken);
}
