using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Application.Mapping;
using Diaspora.Modules.CommunityProgramme.Application.Services;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Requests;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;
using Diaspora.Modules.CommunityProgramme.Domain.Capabilities;
using Diaspora.Modules.CommunityProgramme.Domain.Enums;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Memberships;
using Diaspora.Modules.CommunityProgramme.Domain.Policies;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.CommunityProgramme.Application.Memberships;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// COMMUNITY-LIFE-GAP-002 — membership application and lifecycle.
//
// Seven routes over one aggregate and fourteen states. The transition table is the server's and the
// client's copy is a rendering hint: when the two disagree the client has gone stale, and the 409 is
// the correct outcome rather than something to be prevented by making the client cleverer.
//
// Two properties the register asks for explicitly, and both are here rather than in the aggregate:
//
//   The application and the renewal are idempotent. A retried submission returns the SAME membership
//   rather than creating a second, and the guarantee is B15's — a uniquely indexed key column looked
//   up before the write — rather than a check that two concurrent requests would both pass.
//
//   The member roll and the decision route refuse with 403 rather than filtering. A hidden button is
//   a convenience; the refusal is the control.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Applies for a membership.
/// <para>
/// <b>Where the application opens depends on the programme's policy, not on the client.</b> A
/// programme that reviews applications opens at <c>Pending</c>; one that does not opens at
/// <c>Approved</c>, and — when the tier is free — moves straight on to <c>Active</c>. A paid tier
/// stops at <c>Approved</c> and waits for B15, because §18 forbids this module concluding "paid,
/// therefore a member" and there is nothing here that could.
/// </para>
/// </summary>
[Authorize(Permission = CommunityProgrammePermissions.Participate)]
[Transactional(Reason =
    "Opens the membership, writes its first history entry and raises the status event as one unit. "
    + "A membership that committed without its history would have no record of how it began, which "
    + "is the one thing a readmission decision is made from.")]
public sealed record SubmitMembershipApplicationCommand : ICommand<ProgrammeMembershipResponse>
{
    public required Guid OrganizationId { get; init; }

    public required SubmitMembershipApplicationRequest Request { get; init; }

    /// <summary>The <c>Idempotency-Key</c> header, when the client sent one.</summary>
    public string? IdempotencyKeyHeader { get; init; }
}

public sealed class SubmitMembershipApplicationCommandValidator
    : AbstractValidator<SubmitMembershipApplicationCommand>
{
    public SubmitMembershipApplicationCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.Request).NotNull();
        RuleFor(command => command.Request.TierId).NotEmpty();
    }
}

public sealed class SubmitMembershipApplicationCommandHandler
    : ICommandHandler<SubmitMembershipApplicationCommand, ProgrammeMembershipResponse>
{
    private const string Operation = "membership-application";

    private readonly IMembershipProgrammeRepository _programmes;
    private readonly IProgrammeMembershipRepository _memberships;
    private readonly CommunityProgrammeAccessService _access;
    private readonly MembershipEligibilityEvaluator _eligibility;
    private readonly ProgrammeIdempotency _commit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public SubmitMembershipApplicationCommandHandler(
        IMembershipProgrammeRepository programmes,
        IProgrammeMembershipRepository memberships,
        CommunityProgrammeAccessService access,
        MembershipEligibilityEvaluator eligibility,
        ProgrammeIdempotency commit,
        IUnitOfWork unitOfWork,
        IGuidGenerator guids,
        IClock clock)
    {
        _programmes = programmes;
        _memberships = memberships;
        _access = access;
        _eligibility = eligibility;
        _commit = commit;
        _unitOfWork = unitOfWork;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<ProgrammeMembershipResponse>> Handle(
        SubmitMembershipApplicationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<ProgrammeMembershipResponse>(caller.Error);
        }

        var key = ProgrammeIdempotency.Normalise(
            Operation, command.IdempotencyKeyHeader, command.Request.IdempotencyKey);

        // B15's pattern: look the key up first, and return the record it already made. A retried
        // submission must return the *same* membership rather than creating a second.
        if (key is not null)
        {
            var replayed = await _memberships.FindByApplicationIdempotencyKeyAsync(
                key, cancellationToken);

            if (replayed is not null)
            {
                return await RenderAsync(replayed, cancellationToken);
            }
        }

        var programme = await _programmes.FindByOrganizationAsync(
            command.OrganizationId, cancellationToken);

        if (programme is null)
        {
            return Result.Failure<ProgrammeMembershipResponse>(
                CommunityProgrammeErrors.ProgrammeNotFound);
        }

        if (!programme.IsOpen)
        {
            return Result.Failure<ProgrammeMembershipResponse>(
                CommunityProgrammeErrors.ProgrammeClosed);
        }

        var tier = programme.FindTier(MembershipTierId.From(command.Request.TierId));

        if (tier is null)
        {
            return Result.Failure<ProgrammeMembershipResponse>(
                CommunityProgrammeErrors.TierNotFound);
        }

        if (tier.IsFull)
        {
            return Result.Failure<ProgrammeMembershipResponse>(CommunityProgrammeErrors.TierFull);
        }

        var existing = await _memberships.FindCurrentAsync(
            command.OrganizationId, caller.Value, cancellationToken);

        // A terminal membership does not block a fresh one — rejoining is a new record, which is what
        // keeps the first membership's history readable. A live one does.
        if (existing is not null
            && !ProgrammeMembershipTransitions.AllowsReapplication(existing.Status))
        {
            return Result.Failure<ProgrammeMembershipResponse>(
                CommunityProgrammeErrors.MembershipAlreadyExists);
        }

        var organizationMembership = await _access.FindOrganizationMembershipAsync(
            command.OrganizationId, caller.Value.Value, cancellationToken);

        if (!_eligibility.AllMet(tier, organizationMembership is not null))
        {
            return Result.Failure<ProgrammeMembershipResponse>(
                CommunityProgrammeErrors.RequirementsNotMet);
        }

        if (existing is not null)
        {
            // A second live row would violate the unique index on (organization, member). Rejoining
            // reuses the row and starts a fresh chapter in its history — which is also why Expired
            // and Cancelled are not the end of the record.
            return await ReopenAsync(existing, programme, tier, key, cancellationToken);
        }

        var now = _clock.UtcNow;

        var opening = programme.RequiresApproval
            ? ProgrammeMembershipStatus.Pending
            : ProgrammeMembershipStatus.Approved;

        var applied = ProgrammeMembership.Apply(
            ProgrammeMembershipId.From(_guids.NewSequentialGuid()),
            command.OrganizationId,
            tier.Id,
            caller.Value,
            opening,
            _guids.NewSequentialGuid(),
            now,
            key);

        if (applied.IsFailure)
        {
            return Result.Failure<ProgrammeMembershipResponse>(applied.Error);
        }

        var membership = applied.Value;

        // A free tier on a programme that does not review applications activates immediately. A
        // priced one stops at Approved and waits for B15 — this module never concludes that money
        // moved.
        if (opening == ProgrammeMembershipStatus.Approved && tier.Price is null)
        {
            var activated = membership.MoveTo(
                ProgrammeMembershipStatus.Active,
                _guids.NewSequentialGuid(),
                now,
                MembershipActor.System,
                renewalMonths: tier.RenewalMonths);

            if (activated.IsFailure)
            {
                return Result.Failure<ProgrammeMembershipResponse>(activated.Error);
            }
        }

        _memberships.Add(membership);

        var committed = await ProgrammeIdempotency.GuardAsync(
            key,
            token => _unitOfWork.SaveChangesAsync(token),
            token => _memberships.FindByApplicationIdempotencyKeyAsync(key!, token),
            cancellationToken);

        if (committed.IsFailure)
        {
            return Result.Failure<ProgrammeMembershipResponse>(committed.Error);
        }

        return await RenderAsync(committed.Value.Replayed ?? membership, cancellationToken);
    }

    /// <summary>
    /// Rejoining, on the row that already exists.
    /// <para>
    /// The unique index on (organization, member) is what makes "you already hold one of these" a
    /// database fact, and a second row would violate it. So a member coming back moves the same row
    /// out of its terminal state, keeps its whole history — which is what a readmission decision is
    /// made from — and may change tier on the way in.
    /// </para>
    /// </summary>
    private async Task<Result<ProgrammeMembershipResponse>> ReopenAsync(
        ProgrammeMembership membership,
        MembershipProgramme programme,
        MembershipTier tier,
        string? key,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        var opening = programme.RequiresApproval
            ? ProgrammeMembershipStatus.Pending
            : ProgrammeMembershipStatus.Approved;

        var reopened = membership.Rejoin(
            tier.Id, opening, _guids.NewSequentialGuid(), now, key);

        if (reopened.IsFailure)
        {
            return Result.Failure<ProgrammeMembershipResponse>(reopened.Error);
        }

        if (opening == ProgrammeMembershipStatus.Approved && tier.Price is null)
        {
            var activated = membership.MoveTo(
                ProgrammeMembershipStatus.Active,
                _guids.NewSequentialGuid(),
                now,
                MembershipActor.System,
                renewalMonths: tier.RenewalMonths);

            if (activated.IsFailure)
            {
                return Result.Failure<ProgrammeMembershipResponse>(activated.Error);
            }
        }

        var committed = await ProgrammeIdempotency.GuardAsync(
            key,
            token => _unitOfWork.SaveChangesAsync(token),
            token => key is null
                ? Task.FromResult<ProgrammeMembership?>(null)
                : _memberships.FindByApplicationIdempotencyKeyAsync(key, token),
            cancellationToken);

        if (committed.IsFailure)
        {
            return Result.Failure<ProgrammeMembershipResponse>(committed.Error);
        }

        return await RenderAsync(committed.Value.Replayed ?? membership, cancellationToken);
    }

    private async Task<Result<ProgrammeMembershipResponse>> RenderAsync(
        ProgrammeMembership membership, CancellationToken cancellationToken)
    {
        var programme = await _programmes.FindByOrganizationAsync(
            membership.OrganizationId, cancellationToken);

        var organization = await _access.FindOrganizationAsync(
            membership.OrganizationId, cancellationToken);

        return Result.Success(CommunityProgrammeMapper.ToResponse(
            membership, programme?.FindTier(membership.TierId), organization));
    }
}

/// <summary>Every membership the caller holds, terminal ones included.</summary>
[Authorize(Permission = CommunityProgrammePermissions.Read)]
public sealed record ListMyMembershipsQuery : IQuery<IReadOnlyList<ProgrammeMembershipResponse>>;

public sealed class ListMyMembershipsQueryHandler
    : IQueryHandler<ListMyMembershipsQuery, IReadOnlyList<ProgrammeMembershipResponse>>
{
    private readonly IProgrammeMembershipRepository _memberships;
    private readonly IMembershipProgrammeRepository _programmes;
    private readonly CommunityProgrammeAccessService _access;

    public ListMyMembershipsQueryHandler(
        IProgrammeMembershipRepository memberships,
        IMembershipProgrammeRepository programmes,
        CommunityProgrammeAccessService access)
    {
        _memberships = memberships;
        _programmes = programmes;
        _access = access;
    }

    public async Task<Result<IReadOnlyList<ProgrammeMembershipResponse>>> Handle(
        ListMyMembershipsQuery query, CancellationToken cancellationToken)
    {
        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<ProgrammeMembershipResponse>>(caller.Error);
        }

        var memberships = await _memberships.FindForMemberAsync(
            caller.Value, 100, cancellationToken);

        if (memberships.Count == 0)
        {
            return Result.Success<IReadOnlyList<ProgrammeMembershipResponse>>([]);
        }

        var organizationIds = memberships
            .Select(membership => membership.OrganizationId)
            .Distinct()
            .ToList();

        // Two batched reads rather than two per membership. A member of six organizations would
        // otherwise cost twelve round trips to render a list.
        var programmes = await _programmes.FindManyByOrganizationAsync(
            organizationIds, cancellationToken);

        var organizations = await _access.FindOrganizationsAsync(
            organizationIds, cancellationToken);

        return Result.Success<IReadOnlyList<ProgrammeMembershipResponse>>(
        [
            .. memberships.Select(membership => CommunityProgrammeMapper.ToResponse(
                membership,
                programmes.TryGetValue(membership.OrganizationId, out var programme)
                    ? programme.FindTier(membership.TierId)
                    : null,
                organizations.GetValueOrDefault(membership.OrganizationId)))
        ]);
    }
}

/// <summary>
/// One membership, with its history.
/// <para>
/// The caller's own, or one belonging to an organization they may manage members of. A third party
/// gets the same answer as somebody asking about a membership that does not exist.
/// </para>
/// </summary>
[Authorize(Permission = CommunityProgrammePermissions.Read)]
public sealed record GetMembershipQuery : IQuery<ProgrammeMembershipResponse>
{
    public required Guid MembershipId { get; init; }
}

public sealed class GetMembershipQueryHandler
    : IQueryHandler<GetMembershipQuery, ProgrammeMembershipResponse>
{
    private readonly IProgrammeMembershipRepository _memberships;
    private readonly IMembershipProgrammeRepository _programmes;
    private readonly CommunityProgrammeAccessService _access;

    public GetMembershipQueryHandler(
        IProgrammeMembershipRepository memberships,
        IMembershipProgrammeRepository programmes,
        CommunityProgrammeAccessService access)
    {
        _memberships = memberships;
        _programmes = programmes;
        _access = access;
    }

    public async Task<Result<ProgrammeMembershipResponse>> Handle(
        GetMembershipQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<ProgrammeMembershipResponse>(caller.Error);
        }

        var membership = await _memberships.GetAsync(
            ProgrammeMembershipId.From(query.MembershipId), cancellationToken);

        if (membership is null)
        {
            return Result.Failure<ProgrammeMembershipResponse>(
                CommunityProgrammeErrors.MembershipNotFound);
        }

        if (membership.MemberUserId != caller.Value
            && !await _access.HoldsAsync(
                membership.OrganizationId, OrganizationCapability.ManageMembers, cancellationToken))
        {
            // Not-found rather than forbidden: telling a caller "this membership exists but is not
            // yours" tells them a person belongs to an organization.
            return Result.Failure<ProgrammeMembershipResponse>(
                CommunityProgrammeErrors.MembershipNotFound);
        }

        var programme = await _programmes.FindByOrganizationAsync(
            membership.OrganizationId, cancellationToken);

        var organization = await _access.FindOrganizationAsync(
            membership.OrganizationId, cancellationToken);

        return Result.Success(CommunityProgrammeMapper.ToResponse(
            membership, programme?.FindTier(membership.TierId), organization));
    }
}

/// <summary>The member ends their own membership.</summary>
[Authorize(Permission = CommunityProgrammePermissions.Participate)]
[Transactional(Reason =
    "Moves the membership and appends its history entry as one unit. A cancellation recorded "
    + "without its history entry would leave the member unable to see when or why it ended.")]
public sealed record CancelMembershipCommand : ICommand<ProgrammeMembershipResponse>
{
    public required Guid MembershipId { get; init; }

    public CancelMembershipRequest? Request { get; init; }
}

public sealed class CancelMembershipCommandHandler
    : ICommandHandler<CancelMembershipCommand, ProgrammeMembershipResponse>
{
    private readonly IProgrammeMembershipRepository _memberships;
    private readonly IMembershipProgrammeRepository _programmes;
    private readonly CommunityProgrammeAccessService _access;
    private readonly ProgrammeIdempotency _commit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CancelMembershipCommandHandler(
        IProgrammeMembershipRepository memberships,
        IMembershipProgrammeRepository programmes,
        CommunityProgrammeAccessService access,
        ProgrammeIdempotency commit,
        IUnitOfWork unitOfWork,
        IGuidGenerator guids,
        IClock clock)
    {
        _memberships = memberships;
        _programmes = programmes;
        _access = access;
        _commit = commit;
        _unitOfWork = unitOfWork;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<ProgrammeMembershipResponse>> Handle(
        CancelMembershipCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<ProgrammeMembershipResponse>(caller.Error);
        }

        var membership = await _memberships.GetAsync(
            ProgrammeMembershipId.From(command.MembershipId), cancellationToken);

        if (membership is null)
        {
            return Result.Failure<ProgrammeMembershipResponse>(
                CommunityProgrammeErrors.MembershipNotFound);
        }

        if (membership.MemberUserId != caller.Value)
        {
            // Cancelling is the member's own act. An organization ending somebody's membership is a
            // removal, and it goes through the decision route with its own capability.
            return Result.Failure<ProgrammeMembershipResponse>(
                CommunityProgrammeErrors.MembershipNotFound);
        }

        var moved = membership.MoveTo(
            ProgrammeMembershipStatus.Cancelled,
            _guids.NewSequentialGuid(),
            _clock.UtcNow,
            MembershipActor.Member,
            caller.Value,
            command.Request?.Reason);

        if (moved.IsFailure)
        {
            return Result.Failure<ProgrammeMembershipResponse>(moved.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var programme = await _programmes.FindByOrganizationAsync(
            membership.OrganizationId, cancellationToken);

        var organization = await _access.FindOrganizationAsync(
            membership.OrganizationId, cancellationToken);

        return Result.Success(CommunityProgrammeMapper.ToResponse(
            membership, programme?.FindTier(membership.TierId), organization));
    }
}

/// <summary>
/// The member renews their own membership.
/// <para>
/// <b>Idempotent, and on its own key.</b> A member who applies and later renews sends two different
/// keys, which is why the aggregate carries two columns: one column would make the renewal look like
/// a replayed application and return the membership unchanged.
/// </para>
/// <para>
/// <b>No money moves here.</b> Renewing a priced tier extends the window and B15 takes the payment
/// through its own routes; binding the two is COMMUNITY-LIFE-GAP-003 and belongs to B15.1.
/// </para>
/// </summary>
[Authorize(Permission = CommunityProgrammePermissions.Participate)]
[Transactional(Reason =
    "Records the renewal key and extends the window as one unit. A renewal that extended the "
    + "membership without recording its key would be renewed again by the client's ordinary retry.")]
public sealed record RenewMembershipCommand : ICommand<ProgrammeMembershipResponse>
{
    public required Guid MembershipId { get; init; }

    public RenewMembershipRequest? Request { get; init; }

    public string? IdempotencyKeyHeader { get; init; }
}

public sealed class RenewMembershipCommandHandler
    : ICommandHandler<RenewMembershipCommand, ProgrammeMembershipResponse>
{
    private const string Operation = "membership-renewal";

    private readonly IProgrammeMembershipRepository _memberships;
    private readonly IMembershipProgrammeRepository _programmes;
    private readonly CommunityProgrammeAccessService _access;
    private readonly ProgrammeIdempotency _commit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public RenewMembershipCommandHandler(
        IProgrammeMembershipRepository memberships,
        IMembershipProgrammeRepository programmes,
        CommunityProgrammeAccessService access,
        ProgrammeIdempotency commit,
        IUnitOfWork unitOfWork,
        IGuidGenerator guids,
        IClock clock)
    {
        _memberships = memberships;
        _programmes = programmes;
        _access = access;
        _commit = commit;
        _unitOfWork = unitOfWork;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<ProgrammeMembershipResponse>> Handle(
        RenewMembershipCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<ProgrammeMembershipResponse>(caller.Error);
        }

        var key = ProgrammeIdempotency.Normalise(
            Operation, command.IdempotencyKeyHeader, command.Request?.IdempotencyKey);

        if (key is not null)
        {
            var replayed = await _memberships.FindByRenewalIdempotencyKeyAsync(
                key, cancellationToken);

            if (replayed is not null)
            {
                return await RenderAsync(replayed, cancellationToken);
            }
        }

        var membership = await _memberships.GetAsync(
            ProgrammeMembershipId.From(command.MembershipId), cancellationToken);

        if (membership is null)
        {
            return Result.Failure<ProgrammeMembershipResponse>(
                CommunityProgrammeErrors.MembershipNotFound);
        }

        if (membership.MemberUserId != caller.Value)
        {
            return Result.Failure<ProgrammeMembershipResponse>(
                CommunityProgrammeErrors.MembershipNotFound);
        }

        var programme = await _programmes.FindByOrganizationAsync(
            membership.OrganizationId, cancellationToken);

        var tier = programme?.FindTier(membership.TierId);

        var renewed = membership.Renew(
            _guids.NewSequentialGuid(), _clock.UtcNow, tier?.RenewalMonths, key);

        if (renewed.IsFailure)
        {
            return Result.Failure<ProgrammeMembershipResponse>(renewed.Error);
        }

        var committed = await ProgrammeIdempotency.GuardAsync(
            key,
            token => _unitOfWork.SaveChangesAsync(token),
            token => _memberships.FindByRenewalIdempotencyKeyAsync(key!, token),
            cancellationToken);

        if (committed.IsFailure)
        {
            return Result.Failure<ProgrammeMembershipResponse>(committed.Error);
        }

        return await RenderAsync(committed.Value.Replayed ?? membership, cancellationToken);
    }

    private async Task<Result<ProgrammeMembershipResponse>> RenderAsync(
        ProgrammeMembership membership, CancellationToken cancellationToken)
    {
        var programme = await _programmes.FindByOrganizationAsync(
            membership.OrganizationId, cancellationToken);

        var organization = await _access.FindOrganizationAsync(
            membership.OrganizationId, cancellationToken);

        return Result.Success(CommunityProgrammeMapper.ToResponse(
            membership, programme?.FindTier(membership.TierId), organization));
    }
}
