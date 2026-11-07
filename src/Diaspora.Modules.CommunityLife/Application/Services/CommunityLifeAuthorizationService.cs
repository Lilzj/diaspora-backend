using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Memberships;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Domain.Series;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Services;

/// <summary>
/// Answers the second authorisation question: may this caller do this <em>here</em>?
/// <para>
/// <b>The permission catalogue answers the first — "may this person publish events at all" — and it
/// cannot answer this one.</b> Whether the caller may publish under <em>this</em> organization is a
/// fact about a membership row, and no token can carry it. Every write handler in the module comes
/// through here.
/// </para>
/// <para>
/// <b>The membership role is the module's real authorisation model.</b> A volunteer may work a door
/// and nothing else; an organizer may run events; an administrator may manage the page and its
/// members; an owner may hand the organization on. Each is a comparison against a minimum, which is
/// why <c>MembershipRole</c> is ordered and why its numbering leaves gaps.
/// </para>
/// <para>
/// <b>Staff never pass a write check.</b> A moderator or an administrator does not edit, publish,
/// reschedule, cancel, archive or delete anybody's event, series or organization, check people in,
/// or admit, invite, promote or remove its members through the owner's routes: there are no support
/// edits on a member's behalf. Staff act on somebody else's subject only through the moderation
/// surface — <c>/moderation</c>, <c>/reports</c> and <c>/featured</c> — which asks
/// <see cref="RequireStaff"/> and never comes through the write checks below. A member of staff who
/// genuinely belongs to an organization is what they are there: a member with a role, no more.
/// </para>
/// <para>
/// <b>What staff keep is sight.</b> The read checks still let staff see a hidden, private or
/// unpublished subject, because a moderator who cannot see what was reported cannot review it; and
/// the management-read checks let them read what an organizer reads about it. Those are reads
/// only, and no write handler may use them.
/// </para>
/// <para>
/// <b>There is deliberately no staff bypass on an attendee list.</b> Reading who is coming to a
/// named event — an immigration advice session, a support meeting, a political association's AGM —
/// is a different act from moderating a public page, and it should require a deliberate support tool
/// rather than fall out of a role check in a general-purpose helper. B9, B10 and B11 draw the same
/// line; here it protects who a member associates with.
/// </para>
/// </summary>
public sealed class CommunityLifeAuthorizationService
{
    private static readonly HashSet<string> StaffRoles =
        new(StringComparer.OrdinalIgnoreCase) { "Moderator", "Admin", "SuperAdmin" };

    private readonly ICurrentUser _currentUser;
    private readonly IOrganizationMembershipRepository _memberships;

    public CommunityLifeAuthorizationService(
        ICurrentUser currentUser, IOrganizationMembershipRepository memberships)
    {
        _currentUser = currentUser;
        _memberships = memberships;
    }

    /// <summary>The caller, or null when the request is anonymous.</summary>
    public UserId? CallerId => _currentUser.UserId;

    public bool IsAuthenticated => _currentUser.UserId is not null;

    /// <summary>
    /// Whether the caller holds a platform staff role. Decides what a caller may <em>see</em> and
    /// whether they may use the moderation surface; never whether a subject is theirs.
    /// </summary>
    public bool IsStaff => _currentUser.Role is { } role && StaffRoles.Contains(role);

    /// <summary>The caller, or a failure. The first line of every write handler.</summary>
    public Result<UserId> RequireCaller() =>
        _currentUser.UserId is { } userId
            ? Result.Success(userId)
            : Result.Failure<UserId>(CommunityLifeErrors.NotAuthenticated);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Organization scope
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>The caller's current membership in an organization, or null.</summary>
    public async Task<OrganizationMembership?> FindMembershipAsync(
        OrganizationId organizationId, CancellationToken cancellationToken = default)
    {
        if (_currentUser.UserId is not { } userId)
        {
            return null;
        }

        return await _memberships.FindCurrentAsync(organizationId, userId, cancellationToken);
    }

    /// <summary>
    /// Requires the caller to hold at least a given role in an organization.
    /// <para>
    /// Staff are held to the same rule as everybody else: their membership decides, and without one
    /// they are refused. The refusal is a forbidden that names the role rather than a not-found,
    /// because an organization page the caller can plainly see is not a secret and pretending
    /// otherwise would leave them unable to learn that joining is the answer.
    /// </para>
    /// </summary>
    public async Task<Result<UserId>> RequireRoleAsync(
        OrganizationId organizationId,
        MembershipRole minimumRole,
        CancellationToken cancellationToken = default)
    {
        var caller = RequireCaller();

        if (caller.IsFailure)
        {
            return caller;
        }

        var membership = await _memberships.FindCurrentAsync(
            organizationId, caller.Value, cancellationToken);

        if (membership is null)
        {
            return Result.Failure<UserId>(CommunityLifeErrors.NotOrganizationMember);
        }

        return membership.HasAtLeast(minimumRole)
            ? caller
            : Result.Failure<UserId>(
                minimumRole >= MembershipRole.Administrator
                    ? CommunityLifeErrors.NotOrganizationAdministrator
                    : CommunityLifeErrors.InsufficientMembershipRole);
    }

    /// <summary>
    /// Whether the caller may write to an organization page.
    /// <para>
    /// The creator counts even without a membership row, which matters for exactly one moment: the
    /// instant between a draft being created and its owner membership committing. Without it a
    /// failed second write in the create transaction would leave a page nobody could edit.
    /// </para>
    /// </summary>
    public async Task<Result<UserId>> RequireOrganizationWriteAsync(
        Organization organization, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(organization);

        var caller = RequireCaller();

        if (caller.IsFailure)
        {
            return caller;
        }

        if (organization.CreatedByUserId == caller.Value)
        {
            return caller;
        }

        return await RequireRoleAsync(
            organization.Id, MembershipRole.Administrator, cancellationToken);
    }

    /// <summary>
    /// Whether the caller may read what an organization's administrators read: a page that is not
    /// public, and its dashboard. Its administrators may, and so may staff reviewing it.
    /// <para>
    /// <b>A read, and only a read.</b> No write handler may use this; a write goes through
    /// <see cref="RequireOrganizationWriteAsync"/>, which staff never pass.
    /// </para>
    /// </summary>
    public async Task<Result<UserId>> RequireOrganizationManagementReadAsync(
        Organization organization, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(organization);

        var caller = RequireCaller();

        if (caller.IsFailure)
        {
            return caller;
        }

        return IsStaff
            ? caller
            : await RequireOrganizationWriteAsync(organization, cancellationToken);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Event scope
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Whether the caller may write to an event.
    /// <para>
    /// The organizer always may. Where an organization is behind the event, so may anybody it has
    /// made an organizer — which is the whole point of the role, and the reason a committee does not
    /// have to share one login.
    /// </para>
    /// <para>
    /// Refused with a not-found rather than a forbidden, because a draft event the caller cannot
    /// manage should not be confirmed to exist.
    /// </para>
    /// </summary>
    public async Task<Result<UserId>> RequireEventWriteAsync(
        CommunityEvent communityEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(communityEvent);

        var caller = RequireCaller();

        if (caller.IsFailure)
        {
            return caller;
        }

        if (communityEvent.OrganizerUserId == caller.Value)
        {
            return caller;
        }

        if (communityEvent.OrganizationId is not { } organizationId)
        {
            return Result.Failure<UserId>(CommunityLifeErrors.NotEventOrganizer);
        }

        var membership = await _memberships.FindCurrentAsync(
            organizationId, caller.Value, cancellationToken);

        return membership?.CanOrganize == true
            ? caller
            : Result.Failure<UserId>(CommunityLifeErrors.NotEventOrganizer);
    }

    /// <summary>
    /// Whether the caller may read what an event's organizers read about it — its analytics. Its
    /// organizers may, and so may staff reviewing it.
    /// <para>
    /// <b>A read, and only a read.</b> No write handler may use this; a write goes through
    /// <see cref="RequireEventWriteAsync"/>, which staff never pass. The attendee list does not use
    /// it either — see the class summary.
    /// </para>
    /// </summary>
    public async Task<Result<UserId>> RequireEventManagementReadAsync(
        CommunityEvent communityEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(communityEvent);

        var caller = RequireCaller();

        if (caller.IsFailure)
        {
            return caller;
        }

        return IsStaff
            ? caller
            : await RequireEventWriteAsync(communityEvent, cancellationToken);
    }

    /// <summary>
    /// Whether the caller may take attendance at an event.
    /// <para>
    /// Deliberately wider than the write check: a volunteer may work a door without being able to
    /// edit anything. That is the whole reason <c>Volunteer</c> sits between <c>Member</c> and
    /// <c>Moderator</c> in the role order.
    /// </para>
    /// </summary>
    public async Task<Result<UserId>> RequireCheckInAsync(
        CommunityEvent communityEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(communityEvent);

        var caller = RequireCaller();

        if (caller.IsFailure)
        {
            return caller;
        }

        if (communityEvent.OrganizerUserId == caller.Value)
        {
            return caller;
        }

        if (communityEvent.OrganizationId is not { } organizationId)
        {
            return Result.Failure<UserId>(CommunityLifeErrors.NotEventOrganizer);
        }

        var membership = await _memberships.FindCurrentAsync(
            organizationId, caller.Value, cancellationToken);

        return membership?.CanTakeAttendance == true
            ? caller
            : Result.Failure<UserId>(CommunityLifeErrors.NotEventOrganizer);
    }

    /// <summary>
    /// Whether the caller may read an event at all.
    /// <para>
    /// Public and unlisted events resolve for anybody holding the link. A private one resolves only
    /// for somebody who may write to it. A members-only one resolves for the organizing
    /// organization's members and is refused to everybody else with a <em>forbidden</em> — the one
    /// place in the module that is not a not-found, because the event's existence is not the secret
    /// and a member told it does not exist would never learn that joining is how they get in.
    /// </para>
    /// </summary>
    public async Task<Result> RequireEventReadAsync(
        CommunityEvent communityEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(communityEvent);

        if (IsStaff)
        {
            return Result.Success();
        }

        if (communityEvent.ModerationState is CommunityLifeModerationState.Hidden
            or CommunityLifeModerationState.Suspended)
        {
            var owner = await RequireEventWriteAsync(communityEvent, cancellationToken);

            return owner.IsSuccess
                ? Result.Success()
                : Result.Failure(CommunityLifeErrors.EventNotFound);
        }

        switch (communityEvent.Visibility)
        {
            case EventVisibility.Public:
            case EventVisibility.Unlisted:
                return Result.Success();

            case EventVisibility.MembersOnly:
            {
                if (communityEvent.OrganizationId is not { } organizationId)
                {
                    return Result.Failure(CommunityLifeErrors.EventNotFound);
                }

                if (_currentUser.UserId is not { } userId)
                {
                    return Result.Failure(CommunityLifeErrors.EventNotMemberVisible);
                }

                var membership = await _memberships.FindCurrentAsync(
                    organizationId, userId, cancellationToken);

                return membership is not null
                    ? Result.Success()
                    : Result.Failure(CommunityLifeErrors.EventNotMemberVisible);
            }

            default:
            {
                var owner = await RequireEventWriteAsync(communityEvent, cancellationToken);

                return owner.IsSuccess
                    ? Result.Success()
                    : Result.Failure(CommunityLifeErrors.EventNotFound);
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Series scope
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Whether the caller may write to a repeating event.
    /// <para>
    /// The same rule as an event's, and deliberately the same rule rather than a similar one:
    /// somebody a church has made an organizer may run its weekly service exactly as they may run a
    /// one-off. A separate series role would mean a committee had to be granted two things to do one
    /// job, and would drift from the event check the first time either changed.
    /// </para>
    /// <para>
    /// Refused with a not-found, because a draft series the caller cannot manage should not be
    /// confirmed to exist.
    /// </para>
    /// </summary>
    public async Task<Result<UserId>> RequireSeriesWriteAsync(
        EventSeries series, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(series);

        var caller = RequireCaller();

        if (caller.IsFailure)
        {
            return caller;
        }

        if (series.OrganizerUserId == caller.Value)
        {
            return caller;
        }

        if (series.OrganizationId is not { } organizationId)
        {
            return Result.Failure<UserId>(CommunityLifeErrors.EventSeriesNotFound);
        }

        var membership = await _memberships.FindCurrentAsync(
            organizationId, caller.Value, cancellationToken);

        return membership?.CanOrganize == true
            ? caller
            : Result.Failure<UserId>(CommunityLifeErrors.EventSeriesNotFound);
    }

    /// <summary>
    /// Whether the caller may read a repeating event as its organizers do — every occurrence,
    /// including drafts, cancellations and the past. Its organizers may, and so may staff
    /// reviewing it.
    /// <para>
    /// <b>A read, and only a read.</b> No write handler may use this; a write goes through
    /// <see cref="RequireSeriesWriteAsync"/>, which staff never pass.
    /// </para>
    /// </summary>
    public async Task<bool> CanReadSeriesAsOrganizerAsync(
        EventSeries series, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(series);

        if (IsStaff)
        {
            return true;
        }

        var write = await RequireSeriesWriteAsync(series, cancellationToken);

        return write.IsSuccess;
    }

    /// <summary>
    /// Whether the caller may read a repeating event.
    /// <para>
    /// Mirrors the event read exactly, including the one deliberate departure: a members-only series
    /// is a forbidden rather than a not-found, because the body running it is plainly visible and a
    /// member told the pattern does not exist would never learn that joining is how they get in.
    /// </para>
    /// </summary>
    public async Task<Result> RequireSeriesReadAsync(
        EventSeries series, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(series);

        if (IsStaff)
        {
            return Result.Success();
        }

        switch (series.Visibility)
        {
            case EventVisibility.Public:
            case EventVisibility.Unlisted:
                return Result.Success();

            case EventVisibility.MembersOnly:
            {
                if (series.OrganizationId is not { } organizationId)
                {
                    return Result.Failure(CommunityLifeErrors.EventSeriesNotFound);
                }

                if (_currentUser.UserId is not { } userId)
                {
                    return Result.Failure(CommunityLifeErrors.EventNotMemberVisible);
                }

                var membership = await _memberships.FindCurrentAsync(
                    organizationId, userId, cancellationToken);

                return membership is not null
                    ? Result.Success()
                    : Result.Failure(CommunityLifeErrors.EventNotMemberVisible);
            }

            default:
            {
                var owner = await RequireSeriesWriteAsync(series, cancellationToken);

                return owner.IsSuccess
                    ? Result.Success()
                    : Result.Failure(CommunityLifeErrors.EventSeriesNotFound);
            }
        }
    }

    /// <summary>
    /// The organizations the caller currently belongs to.
    /// <para>
    /// Resolved once per search and passed into the query filter, so a members-only event is
    /// visible to the right people without a join into the membership table on every row.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<OrganizationId>> CallerOrganizationsAsync(
        CancellationToken cancellationToken = default)
    {
        if (_currentUser.UserId is not { } userId)
        {
            return [];
        }

        var memberships = await _memberships.FindCurrentForMemberAsync(
            userId, limit: 100, cancellationToken);

        return [.. memberships.Select(membership => membership.OrganizationId)];
    }

    /// <summary>Requires the caller to hold a staff role. The moderation surface's first line.</summary>
    public Result<UserId> RequireStaff()
    {
        var caller = RequireCaller();

        if (caller.IsFailure)
        {
            return caller;
        }

        return IsStaff ? caller : Result.Failure<UserId>(CommunityLifeErrors.NotModerator);
    }
}
