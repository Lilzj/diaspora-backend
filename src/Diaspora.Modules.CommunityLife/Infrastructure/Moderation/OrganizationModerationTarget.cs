using Diaspora.Contracts.Moderation.V1;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Infrastructure.Moderation;

/// <summary>
/// An organisation page, as a moderation case sees it (7 Nov 2026).
/// <para>
/// <b>The only way staff change a member's organisation now.</b> A decision is made on the Trust
/// case and carried out here, in the same transaction, with the real moderator and their note.
/// </para>
/// <para>
/// A hide pending review is the page's Hidden state: off discovery, still editable by its
/// administrators, who can fix it and ask for a review. A removal is Suspended, which also moves the
/// lifecycle to Suspended so the body's events stop appearing, and which only staff lift. <b>Neither
/// archives or deletes the page</b>, so its members, its events and what it earned are untouched by
/// the decision itself.
/// </para>
/// <para>
/// The owner is the organisation's Owner member, not the member who created it: see
/// <see cref="CommunityLifeContentOwners"/>.
/// </para>
/// </summary>
public sealed class OrganizationModerationTarget : IModerationTarget
{
    private readonly IOrganizationRepository _organizations;
    private readonly CommunityLifeContentOwners _owners;
    private readonly IClock _clock;

    public OrganizationModerationTarget(
        IOrganizationRepository organizations, CommunityLifeContentOwners owners, IClock clock)
    {
        _organizations = organizations;
        _owners = owners;
        _clock = clock;
    }

    public string ContentType => ModerationContentTypes.Organization;

    public string Area => ModerationAreas.Events;

    public async Task<ModerationTargetSnapshot?> DescribeAsync(
        Guid contentId, CancellationToken cancellationToken = default)
    {
        if (contentId == Guid.Empty)
        {
            return null;
        }

        var organization = await _organizations.GetIncludingDeletedAsync(
            OrganizationId.From(contentId), cancellationToken);

        if (organization is null)
        {
            return null;
        }

        var owner = await _owners.OfOrganizationAsync(organization, cancellationToken);

        return new ModerationTargetSnapshot(
            organization.Id.Value,
            owner.Value,
            organization.Name.Value,
            organization.ModerationState.ToString());
    }

    public async Task<Result<ModerationTargetChange>> ApplyAsync(
        Guid contentId,
        ModerationOutcome outcome,
        ModerationActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);

        // Past the delete filter, as the moderation route it replaces was: a case about a page its
        // owners have since deleted must still be decidable.
        var organization = await _organizations.GetIncludingDeletedAsync(
            OrganizationId.From(contentId), cancellationToken);

        if (organization is null)
        {
            return Result.Failure<ModerationTargetChange>(CommunityLifeErrors.OrganizationNotFound);
        }

        var before = organization.ModerationState;
        var now = _clock.UtcNow;
        UserId? moderator = actor.ModeratorUserId is { } id ? UserId.From(id) : null;

        switch (outcome)
        {
            case ModerationOutcome.HidePendingReview:
                // A removal that stands is not softened into a hide by a later case, and a hidden
                // page over a suspended lifecycle is a state nothing else in the module produces.
                if (before != CommunityLifeModerationState.Suspended)
                {
                    organization.Hide(actor.Note, moderator, now);
                }

                break;

            case ModerationOutcome.Remove:
                organization.Suspend(actor.Note, moderator, now);
                break;

            case ModerationOutcome.Restore:
                organization.Clear(moderator, now);
                break;

            case ModerationOutcome.NoAction:
                // Looked at and found to be fine: the automatic flag comes off, nothing stronger.
                if (before == CommunityLifeModerationState.Flagged)
                {
                    organization.Clear(moderator, now);
                }

                break;

            default:
                return Result.Failure<ModerationTargetChange>(CommunityLifeErrors.ModerationActionInvalid);
        }

        return Result.Success(new ModerationTargetChange(
            before.ToString(), organization.ModerationState.ToString()));
    }
}
