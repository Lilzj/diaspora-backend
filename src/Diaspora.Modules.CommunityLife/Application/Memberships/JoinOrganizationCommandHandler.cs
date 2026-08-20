using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Memberships;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.CommunityLife.Application.Memberships;

/// <summary>
/// Asks to join an organization, or joins it outright.
/// <para>
/// <b>Which of the two happens is the policy evaluator's decision, never the caller's.</b> An open
/// association admits immediately; one that vets queues the request; a closed one refuses. Letting
/// the request carry a status would let anybody admit themselves to a body that screens its members
/// — which for a support group or a political association is the whole point of screening.
/// </para>
/// <para>
/// <b>A membership that already exists is re-opened rather than duplicated.</b> Somebody who left a
/// church two years ago and comes back is the same person with the same history, and a second row
/// would split their attendance record in half. A <em>removal</em> is the exception the evaluator
/// refuses outright: only the organization can undo it.
/// </para>
/// </summary>
public sealed class JoinOrganizationCommandHandler
    : ICommandHandler<JoinOrganizationCommand, MembershipResponse>
{
    private readonly IOrganizationRepository _organizations;
    private readonly IOrganizationMembershipRepository _memberships;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly CommunityLifeMembershipPolicyEvaluator _policy;
    private readonly IOptions<CommunityLifeOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public JoinOrganizationCommandHandler(
        IOrganizationRepository organizations,
        IOrganizationMembershipRepository memberships,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        CommunityLifeMembershipPolicyEvaluator policy,
        IOptions<CommunityLifeOptions> options,
        IGuidGenerator guids,
        IClock clock)
    {
        _organizations = organizations;
        _memberships = memberships;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _policy = policy;
        _options = options;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<MembershipResponse>> Handle(
        JoinOrganizationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<MembershipResponse>(caller.Error);
        }

        var organization = await _organizations.GetAsync(
            OrganizationId.From(command.OrganizationId), cancellationToken);

        if (organization is null)
        {
            return Result.Failure<MembershipResponse>(CommunityLifeErrors.OrganizationNotFound);
        }

        if (!organization.AcceptsMembership)
        {
            return Result.Failure<MembershipResponse>(
                CommunityLifeErrors.MembershipClosed);
        }

        var existing = await _memberships.FindAsync(
            organization.Id, caller.Value, cancellationToken);

        // Asking twice is not an error. A client that retried after a timeout gets the membership
        // it already has rather than a conflict it cannot act on.
        if (existing?.IsCurrent == true || existing?.IsPending == true)
        {
            return Result.Success(OrganizationMapper.ToResponse(existing));
        }

        // Checked before the evaluator so the caller is told the real reason. A removal is the
        // organization's decision about this person and only the organization can undo it.
        if (existing?.Status == MembershipStatus.Removed)
        {
            return Result.Failure<MembershipResponse>(CommunityLifeErrors.MembershipRemoved);
        }

        var guard = await GuardAsync(caller.Value, cancellationToken);

        if (guard.IsFailure)
        {
            return Result.Failure<MembershipResponse>(guard.Error);
        }

        var now = _clock.UtcNow;

        var decision = await _policy.DecideAsync(
            organization,
            caller.Value,
            existing,
            command.Request.Message,
            now,
            cancellationToken);

        if (decision.Status == MembershipStatus.Rejected)
        {
            return Result.Failure<MembershipResponse>(
                string.IsNullOrWhiteSpace(decision.Reason)
                    ? CommunityLifeErrors.MembershipRequirementsNotMet
                    : CommunityLifeErrors.MembershipRequirementsNotMet with
                    {
                        Description = decision.Reason
                    });
        }

        var membership = existing is null
            ? OpenNew(organization, caller.Value, decision, command.Request.Message, now)
            : ReopenExisting(existing, decision, now);

        if (membership.IsFailure)
        {
            return Result.Failure<MembershipResponse>(membership.Error);
        }

        if (existing is null)
        {
            await _memberships.AddAsync(membership.Value, cancellationToken);
        }
        else
        {
            _memberships.Update(membership.Value);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(OrganizationMapper.ToResponse(membership.Value));
    }

    /// <summary>
    /// Caps how many requests one person can have outstanding.
    /// <para>
    /// Not a cap on memberships — belonging to twenty bodies is ordinary in a diaspora — but on
    /// requests waiting on somebody else's attention, which is the shape a nuisance takes.
    /// </para>
    /// </summary>
    private async Task<Result> GuardAsync(UserId caller, CancellationToken cancellationToken)
    {
        var page = await _memberships.SearchAsync(
            new MembershipQueryFilter
            {
                MemberUserId = caller,
                Statuses = [MembershipStatus.Requested],
                Limit = _options.Value.MaxPendingMembershipRequests + 1
            },
            cancellationToken);

        return page.Items.Count >= _options.Value.MaxPendingMembershipRequests
            ? Result.Failure(CommunityLifeErrors.TooManyMembershipRequests)
            : Result.Success();
    }

    private Result<OrganizationMembership> OpenNew(
        Organization organization,
        UserId caller,
        Extensibility.Membership.MembershipDecision decision,
        string? message,
        DateTimeOffset now) =>
        OrganizationMembership.Open(
            OrganizationMembershipId.From(_guids.NewSequentialGuid()),
            organization.Id,
            caller,
            decision.Status,
            decision.Role,
            _guids.NewSequentialGuid(),
            now,
            message,
            invitedBy: null,
            organization.MembershipPolicy.RenewalMonths);

    /// <summary>
    /// Puts a lapsed or departed membership back where the policy says it belongs.
    /// <para>
    /// Through the ordinary transition, so the history gains an entry and the counters settle from
    /// the one path that moves them.
    /// </para>
    /// </summary>
    private Result<OrganizationMembership> ReopenExisting(
        OrganizationMembership existing,
        Extensibility.Membership.MembershipDecision decision,
        DateTimeOffset now)
    {
        var moved = existing.MoveTo(
            decision.Status, _guids.NewSequentialGuid(), now);

        return moved.IsFailure
            ? Result.Failure<OrganizationMembership>(moved.Error)
            : Result.Success(existing);
    }
}
