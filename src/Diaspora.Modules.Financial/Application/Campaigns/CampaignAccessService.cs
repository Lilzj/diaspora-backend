using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Extensibility.Campaigns;
using Diaspora.SharedKernel.Results;

using Diaspora.Modules.Financial.Options;
using Microsoft.Extensions.Options;

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
/// <para>
/// <b>Staff never pass the owner check.</b> Holding <c>financial:giving:manage</c> makes nobody the
/// organiser of a campaign, and the organiser's routes — edit, submit, pause, resume, close, cancel,
/// updates, milestones, fund usage — answer staff exactly as they answer any other member who does
/// not run it. Staff act on a campaign through two doors only: the review decision on a submitted
/// campaign (<see cref="IsReviewer"/>), and a decision on a Trust moderation case, which suspends
/// or restores a live one through <c>FundraiserModerationTarget</c>.
/// </para>
/// </summary>
public sealed class CampaignAccessService
{
    private readonly FinancialAuthorizationService _authorization;
    private readonly ICampaignOrganizerAuthority _organizations;
    private readonly FinancialOptions _options;

    public CampaignAccessService(
        FinancialAuthorizationService authorization,
        ICampaignOrganizerAuthority organizations,
        IOptions<FinancialOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _authorization = authorization;
        _organizations = organizations;
        _options = options.Value;
    }

    /// <summary>
    /// Whether the people who run a campaign may publish or reject their own submission.
    /// <para>
    /// <b>The review policy, answered once and served.</b> Publication is the review decision: where
    /// a deployment reviews campaigns it belongs to staff, and an organizer who could publish their
    /// own submission would make the review a formality. Where a deployment does <em>not</em> review
    /// them, the organizer publishes their own.
    /// </para>
    /// <para>
    /// This exists because the answer has to reach the client. `AvailableTransitions` listed
    /// <c>Published</c> for anybody who could manage the campaign, so it promised a move the
    /// transition handler then refused — and the app, which hardcoded its own idea of the lifecycle
    /// instead of reading the field, offered no publish action at all even where one was legal. A
    /// capability the platform has and the client denies, and a capability the platform refuses and
    /// the client offers, are the same defect pointing in two directions.
    /// </para>
    /// <para>
    /// <b>Holding the staff permission no longer changes the answer.</b> Transitions are listed only
    /// for somebody who runs the campaign, and a member of staff who runs one is its organiser, not
    /// its reviewer: under review they submit and wait like anybody else.
    /// </para>
    /// </summary>
    public bool CanDecideReview() => !_options.RequireCampaignReview;

    /// <summary>
    /// Whether this caller makes the review decision on campaigns other people submit.
    /// <para>
    /// True only where the deployment reviews campaigns and the caller holds the giving-management
    /// permission. It opens exactly two moves — a submitted campaign to published or to rejected —
    /// and never on a campaign the caller runs themselves.
    /// </para>
    /// </summary>
    public bool IsReviewer() =>
        _options.RequireCampaignReview
        && _authorization.Has(FinancialPermissions.GivingManage);

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
