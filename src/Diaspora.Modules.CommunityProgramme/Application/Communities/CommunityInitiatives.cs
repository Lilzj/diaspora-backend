using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Application.Services;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Requests;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityProgramme.Application.Communities;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// A community's initiatives — FRONTEND-PHASE13-GAP-019, 21 Oct 2026.
//
// **Who reads.** As the community's notices: a public or unlisted community's initiatives are
// anybody's; a private or invite-only community's are its current members' (and staff), and anybody
// else gets the same not-found as the community itself.
//
// **Who starts one.** `CanManageInitiatives`, which the capability set already computed and the app
// already reads: the owner, administrators and ambassadors, in a community that is not archived. No
// staff bypass — running a community's projects is the community's own act.
//
// **Who changes one.** The same capability, or one of its organisers while they are still a current
// member. Organisers may revise it, move its status and post updates; only the capability changes who
// organises it, so an organiser cannot hand the initiative to somebody the community did not choose.
//
// **Who takes part.** Any current member, while it is Planned or Active. A participant who leaves the
// community keeps their row and stops being counted or listed — the roll decides, every read.
//
// **Who is named.** Organisers, participants and update authors are each named only as their own
// membership allows: Public to anybody who may read the community, CommunityOnly to its current
// members, Private to nobody else. The same line `/roles` draws (d07a707).
//
// **No money.** Nothing here takes, records or links a payment. Fundraising is a campaign's.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>A community's initiatives, newest first, optionally of one status.</summary>
[AllowAnonymous(
    "Public discovery. The route already says AllowAnonymous and this is the same statement at " +
    "the other end of the pipeline: a listing nobody can see without signing up is a listing " +
    "nobody answers.")]
public sealed record ListCommunityInitiativesQuery : IQuery<IReadOnlyList<CommunityInitiativeResponse>>
{
    public required string IdOrSlug { get; init; }

    public string? Status { get; init; }
}

public sealed class ListCommunityInitiativesQueryHandler
    : IQueryHandler<ListCommunityInitiativesQuery, IReadOnlyList<CommunityInitiativeResponse>>
{
    /// <summary>A community's projects, not an archive. A hundred is every one anybody reads.</summary>
    private const int Limit = 100;

    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly ICommunityInitiativeRepository _initiatives;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IMemberProfileService _profiles;

    public ListCommunityInitiativesQueryHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        ICommunityInitiativeRepository initiatives,
        CommunityProgrammeAccessService access,
        IMemberProfileService profiles)
    {
        _communities = communities;
        _memberships = memberships;
        _initiatives = initiatives;
        _access = access;
        _profiles = profiles;
    }

    public async Task<Result<IReadOnlyList<CommunityInitiativeResponse>>> Handle(
        ListCommunityInitiativesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var status = CommunityInitiativeAccess.ParseStatus(query.Status);

        if (status.IsFailure)
        {
            return Result.Failure<IReadOnlyList<CommunityInitiativeResponse>>(status.Error);
        }

        var readable = await CommunityNoticeAccess.ResolveReaderAsync(
            query.IdOrSlug, _communities, _memberships, _access, cancellationToken);

        if (readable is null)
        {
            return Result.Failure<IReadOnlyList<CommunityInitiativeResponse>>(CommunityErrors.NotFound);
        }

        var initiatives = await _initiatives.ListAsync(
            readable.Community.Id, status.Value, Limit, cancellationToken);

        return Result.Success(await CommunityInitiativeAccess.ComposeAsync(
            readable, initiatives, _initiatives, _profiles, _access, cancellationToken));
    }
}

/// <summary>One initiative.</summary>
[AllowAnonymous(
    "Public discovery. The route already says AllowAnonymous and this is the same statement at " +
    "the other end of the pipeline: a listing nobody can see without signing up is a listing " +
    "nobody answers.")]
public sealed record GetCommunityInitiativeQuery : IQuery<CommunityInitiativeResponse>
{
    public required string IdOrSlug { get; init; }

    public required Guid InitiativeId { get; init; }
}

public sealed class GetCommunityInitiativeQueryHandler
    : IQueryHandler<GetCommunityInitiativeQuery, CommunityInitiativeResponse>
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly ICommunityInitiativeRepository _initiatives;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IMemberProfileService _profiles;

    public GetCommunityInitiativeQueryHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        ICommunityInitiativeRepository initiatives,
        CommunityProgrammeAccessService access,
        IMemberProfileService profiles)
    {
        _communities = communities;
        _memberships = memberships;
        _initiatives = initiatives;
        _access = access;
        _profiles = profiles;
    }

    public async Task<Result<CommunityInitiativeResponse>> Handle(
        GetCommunityInitiativeQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var target = await CommunityInitiativeAccess.ResolveAsync(
            query.IdOrSlug, query.InitiativeId, _communities, _memberships, _initiatives, _access,
            cancellationToken);

        if (target.IsFailure)
        {
            return Result.Failure<CommunityInitiativeResponse>(target.Error);
        }

        var (readable, initiative) = target.Value;

        return Result.Success(await CommunityInitiativeAccess.ComposeOneAsync(
            readable, initiative, _initiatives, _profiles, _access, cancellationToken));
    }
}

/// <summary>Start an initiative. Needs canManageInitiatives.</summary>
[RequirePermission(CommunityProgrammePermissions.Participate)]
public sealed record CreateCommunityInitiativeCommand : ICommand<CommunityInitiativeResponse>
{
    public required string IdOrSlug { get; init; }

    public required CreateCommunityInitiativeRequest Request { get; init; }
}

public sealed class CreateCommunityInitiativeCommandHandler
    : ICommandHandler<CreateCommunityInitiativeCommand, CommunityInitiativeResponse>
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly ICommunityInitiativeRepository _initiatives;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IMemberProfileService _profiles;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCommunityInitiativeCommandHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        ICommunityInitiativeRepository initiatives,
        CommunityProgrammeAccessService access,
        IMemberProfileService profiles,
        IUnitOfWork unitOfWork)
    {
        _communities = communities;
        _memberships = memberships;
        _initiatives = initiatives;
        _access = access;
        _profiles = profiles;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CommunityInitiativeResponse>> Handle(
        CreateCommunityInitiativeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CommunityInitiativeResponse>(caller.Error);
        }

        var readable = await CommunityNoticeAccess.ResolveReaderAsync(
            command.IdOrSlug, _communities, _memberships, _access, cancellationToken);

        if (readable is null)
        {
            return Result.Failure<CommunityInitiativeResponse>(CommunityErrors.NotFound);
        }

        // No staff bypass: see the file header.
        if (!CommunityCapabilitySet.For(readable.Community, readable.Membership).CanManageInitiatives)
        {
            return Result.Failure<CommunityInitiativeResponse>(CommunityInitiativeErrors.CannotCreate);
        }

        var status = CommunityInitiativeAccess.ParseStatus(command.Request.Status);

        if (status.IsFailure)
        {
            return Result.Failure<CommunityInitiativeResponse>(status.Error);
        }

        IReadOnlyCollection<Guid> requested = command.Request.OrganiserIds is { Count: > 0 } named
            ? named
            : [caller.Value.Value];

        var organisers = await CommunityInitiativeAccess.ValidateOrganisersAsync(
            readable, requested, _initiatives, cancellationToken);

        if (organisers.IsFailure)
        {
            return Result.Failure<CommunityInitiativeResponse>(organisers.Error);
        }

        var created = CommunityInitiative.Create(
            CommunityInitiativeId.New(),
            readable.Community.Id,
            caller.Value,
            command.Request.Title,
            command.Request.Description,
            command.Request.TargetDate,
            status.Value ?? CommunityInitiativeStatus.Planned,
            organisers.Value,
            DateTimeOffset.UtcNow);

        if (created.IsFailure)
        {
            return Result.Failure<CommunityInitiativeResponse>(created.Error);
        }

        _initiatives.Add(created.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await CommunityInitiativeAccess.ComposeOneAsync(
            readable, created.Value, _initiatives, _profiles, _access, cancellationToken));
    }
}

/// <summary>Change an initiative. Its organisers, or anybody with canManageInitiatives.</summary>
[RequirePermission(CommunityProgrammePermissions.Participate)]
public sealed record UpdateCommunityInitiativeCommand : ICommand<CommunityInitiativeResponse>
{
    public required string IdOrSlug { get; init; }

    public required Guid InitiativeId { get; init; }

    public required UpdateCommunityInitiativeRequest Request { get; init; }
}

public sealed class UpdateCommunityInitiativeCommandHandler
    : ICommandHandler<UpdateCommunityInitiativeCommand, CommunityInitiativeResponse>
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly ICommunityInitiativeRepository _initiatives;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IMemberProfileService _profiles;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCommunityInitiativeCommandHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        ICommunityInitiativeRepository initiatives,
        CommunityProgrammeAccessService access,
        IMemberProfileService profiles,
        IUnitOfWork unitOfWork)
    {
        _communities = communities;
        _memberships = memberships;
        _initiatives = initiatives;
        _access = access;
        _profiles = profiles;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CommunityInitiativeResponse>> Handle(
        UpdateCommunityInitiativeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CommunityInitiativeResponse>(caller.Error);
        }

        var target = await CommunityInitiativeAccess.ResolveAsync(
            command.IdOrSlug, command.InitiativeId, _communities, _memberships, _initiatives, _access,
            cancellationToken);

        if (target.IsFailure)
        {
            return Result.Failure<CommunityInitiativeResponse>(target.Error);
        }

        var (readable, initiative) = target.Value;
        var request = command.Request;

        if (!CommunityInitiativeAccess.HasAuthority(initiative, readable, caller.Value))
        {
            return Result.Failure<CommunityInitiativeResponse>(CommunityInitiativeErrors.CannotChange);
        }

        var status = CommunityInitiativeAccess.ParseStatus(request.Status);

        if (status.IsFailure)
        {
            return Result.Failure<CommunityInitiativeResponse>(status.Error);
        }

        var now = DateTimeOffset.UtcNow;

        if (request.OrganiserIds is not null)
        {
            if (!CommunityCapabilitySet.For(readable.Community, readable.Membership).CanManageInitiatives)
            {
                return Result.Failure<CommunityInitiativeResponse>(
                    CommunityInitiativeErrors.CannotChangeOrganisers);
            }

            if (initiative.IsClosed)
            {
                return Result.Failure<CommunityInitiativeResponse>(CommunityInitiativeErrors.Closed);
            }

            var organisers = await CommunityInitiativeAccess.ValidateOrganisersAsync(
                readable, request.OrganiserIds, _initiatives, cancellationToken);

            if (organisers.IsFailure)
            {
                return Result.Failure<CommunityInitiativeResponse>(organisers.Error);
            }

            var replaced = initiative.ReplaceOrganisers(organisers.Value);

            if (replaced.IsFailure)
            {
                return Result.Failure<CommunityInitiativeResponse>(replaced.Error);
            }
        }

        var describesSomething = request.Title is not null
            || request.Description is not null
            || request.TargetDate is not null
            || request.ClearTargetDate == true;

        // Text before status, so "correct the description and mark it completed" is one request.
        if (describesSomething)
        {
            var revised = initiative.Revise(
                request.Title, request.Description, request.TargetDate, request.ClearTargetDate == true, now);

            if (revised.IsFailure)
            {
                return Result.Failure<CommunityInitiativeResponse>(revised.Error);
            }
        }

        if (status.Value is { } next)
        {
            var moved = initiative.ChangeStatus(next, now);

            if (moved.IsFailure)
            {
                return Result.Failure<CommunityInitiativeResponse>(moved.Error);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await CommunityInitiativeAccess.ComposeOneAsync(
            readable, initiative, _initiatives, _profiles, _access, cancellationToken));
    }
}

/// <summary>Take part in an initiative. Any current member, while it is Planned or Active.</summary>
[RequirePermission(CommunityProgrammePermissions.Participate)]
public sealed record JoinCommunityInitiativeCommand : ICommand<CommunityInitiativeResponse>
{
    public required string IdOrSlug { get; init; }

    public required Guid InitiativeId { get; init; }
}

public sealed class JoinCommunityInitiativeCommandHandler
    : ICommandHandler<JoinCommunityInitiativeCommand, CommunityInitiativeResponse>
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly ICommunityInitiativeRepository _initiatives;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IMemberProfileService _profiles;
    private readonly IUnitOfWork _unitOfWork;

    public JoinCommunityInitiativeCommandHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        ICommunityInitiativeRepository initiatives,
        CommunityProgrammeAccessService access,
        IMemberProfileService profiles,
        IUnitOfWork unitOfWork)
    {
        _communities = communities;
        _memberships = memberships;
        _initiatives = initiatives;
        _access = access;
        _profiles = profiles;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CommunityInitiativeResponse>> Handle(
        JoinCommunityInitiativeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CommunityInitiativeResponse>(caller.Error);
        }

        var target = await CommunityInitiativeAccess.ResolveAsync(
            command.IdOrSlug, command.InitiativeId, _communities, _memberships, _initiatives, _access,
            cancellationToken);

        if (target.IsFailure)
        {
            return Result.Failure<CommunityInitiativeResponse>(target.Error);
        }

        var (readable, initiative) = target.Value;

        if (!CommunityInitiativeAccess.IsActingMember(readable))
        {
            return Result.Failure<CommunityInitiativeResponse>(CommunityInitiativeErrors.JoinMembersOnly);
        }

        // Joining twice is joining once: the answer is the same either way.
        var existing = await _initiatives.FindParticipantAsync(initiative.Id, caller.Value, cancellationToken);

        if (existing is null)
        {
            var joined = CommunityInitiativeParticipant.Join(initiative, caller.Value, DateTimeOffset.UtcNow);

            if (joined.IsFailure)
            {
                return Result.Failure<CommunityInitiativeResponse>(joined.Error);
            }

            _initiatives.AddParticipant(joined.Value);

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception) when (ProgrammeIdempotency.IsUniqueViolation(exception))
            {
                // Two joins raced: both found no row, and the unique index refused the second
                // insert. The other request's row is the member's participation, so this is a
                // repeat join and answers as one. The refused row is let go so nothing later in
                // this scope tries to write it again; the response below reads the stored state.
                _initiatives.ForgetParticipant(joined.Value);
            }
        }

        return Result.Success(await CommunityInitiativeAccess.ComposeOneAsync(
            readable, initiative, _initiatives, _profiles, _access, cancellationToken));
    }
}

/// <summary>Stop taking part. Always allowed; leaving something you are not in changes nothing.</summary>
[RequirePermission(CommunityProgrammePermissions.Participate)]
public sealed record LeaveCommunityInitiativeCommand : ICommand<CommunityInitiativeResponse>
{
    public required string IdOrSlug { get; init; }

    public required Guid InitiativeId { get; init; }
}

public sealed class LeaveCommunityInitiativeCommandHandler
    : ICommandHandler<LeaveCommunityInitiativeCommand, CommunityInitiativeResponse>
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly ICommunityInitiativeRepository _initiatives;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IMemberProfileService _profiles;
    private readonly IUnitOfWork _unitOfWork;

    public LeaveCommunityInitiativeCommandHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        ICommunityInitiativeRepository initiatives,
        CommunityProgrammeAccessService access,
        IMemberProfileService profiles,
        IUnitOfWork unitOfWork)
    {
        _communities = communities;
        _memberships = memberships;
        _initiatives = initiatives;
        _access = access;
        _profiles = profiles;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CommunityInitiativeResponse>> Handle(
        LeaveCommunityInitiativeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CommunityInitiativeResponse>(caller.Error);
        }

        var target = await CommunityInitiativeAccess.ResolveAsync(
            command.IdOrSlug, command.InitiativeId, _communities, _memberships, _initiatives, _access,
            cancellationToken);

        if (target.IsFailure)
        {
            return Result.Failure<CommunityInitiativeResponse>(target.Error);
        }

        var (readable, initiative) = target.Value;

        var existing = await _initiatives.FindParticipantAsync(initiative.Id, caller.Value, cancellationToken);

        if (existing is not null)
        {
            _initiatives.RemoveParticipant(existing);

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (ConcurrencyConflictException)
            {
                // Two leaves raced: both found the row, and the second delete matched nothing.
                // The member is out either way, which is what a repeat leave answers.
                _initiatives.ForgetParticipant(existing);
            }
        }

        return Result.Success(await CommunityInitiativeAccess.ComposeOneAsync(
            readable, initiative, _initiatives, _profiles, _access, cancellationToken));
    }
}

/// <summary>Who is taking part, as each chose to be seen. The count on the initiative is everybody.</summary>
[AllowAnonymous(
    "Public discovery. The route already says AllowAnonymous and this is the same statement at " +
    "the other end of the pipeline: a listing nobody can see without signing up is a listing " +
    "nobody answers.")]
public sealed record ListCommunityInitiativeParticipantsQuery
    : IQuery<IReadOnlyList<CommunityInitiativeParticipantResponse>>
{
    public required string IdOrSlug { get; init; }

    public required Guid InitiativeId { get; init; }
}

public sealed class ListCommunityInitiativeParticipantsQueryHandler
    : IQueryHandler<ListCommunityInitiativeParticipantsQuery, IReadOnlyList<CommunityInitiativeParticipantResponse>>
{
    private const int Limit = 200;

    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly ICommunityInitiativeRepository _initiatives;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IMemberProfileService _profiles;

    public ListCommunityInitiativeParticipantsQueryHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        ICommunityInitiativeRepository initiatives,
        CommunityProgrammeAccessService access,
        IMemberProfileService profiles)
    {
        _communities = communities;
        _memberships = memberships;
        _initiatives = initiatives;
        _access = access;
        _profiles = profiles;
    }

    public async Task<Result<IReadOnlyList<CommunityInitiativeParticipantResponse>>> Handle(
        ListCommunityInitiativeParticipantsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var target = await CommunityInitiativeAccess.ResolveAsync(
            query.IdOrSlug, query.InitiativeId, _communities, _memberships, _initiatives, _access,
            cancellationToken);

        if (target.IsFailure)
        {
            return Result.Failure<IReadOnlyList<CommunityInitiativeParticipantResponse>>(target.Error);
        }

        var (readable, initiative) = target.Value;

        // Staff get no wider view, as with the roll and the office holders.
        var participants = await _initiatives.ListVisibleParticipantsAsync(
            readable.Community.Id,
            initiative.Id,
            includeCommunityOnly: readable.Membership?.IsCurrentMember == true,
            _access.CallerId,
            Limit,
            cancellationToken);

        var names = participants.Count == 0
            ? new Dictionary<Guid, MemberProfile>()
            : await _profiles.ResolveAsync([.. participants.Select(p => p.MemberId)], cancellationToken);

        return Result.Success<IReadOnlyList<CommunityInitiativeParticipantResponse>>(
        [
            .. participants
                .Where(participant => names.ContainsKey(participant.MemberId.Value))
                .Select(participant => new CommunityInitiativeParticipantResponse
                {
                    UserId = participant.MemberId.Value,
                    DisplayName = names[participant.MemberId.Value].DisplayName,
                    JoinedAt = participant.JoinedAt
                })
        ]);
    }
}

/// <summary>An initiative's progress updates, newest first.</summary>
[AllowAnonymous(
    "Public discovery. The route already says AllowAnonymous and this is the same statement at " +
    "the other end of the pipeline: a listing nobody can see without signing up is a listing " +
    "nobody answers.")]
public sealed record ListCommunityInitiativeUpdatesQuery
    : IQuery<IReadOnlyList<CommunityInitiativeUpdateResponse>>
{
    public required string IdOrSlug { get; init; }

    public required Guid InitiativeId { get; init; }
}

public sealed class ListCommunityInitiativeUpdatesQueryHandler
    : IQueryHandler<ListCommunityInitiativeUpdatesQuery, IReadOnlyList<CommunityInitiativeUpdateResponse>>
{
    private const int Limit = 100;

    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly ICommunityInitiativeRepository _initiatives;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IMemberProfileService _profiles;

    public ListCommunityInitiativeUpdatesQueryHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        ICommunityInitiativeRepository initiatives,
        CommunityProgrammeAccessService access,
        IMemberProfileService profiles)
    {
        _communities = communities;
        _memberships = memberships;
        _initiatives = initiatives;
        _access = access;
        _profiles = profiles;
    }

    public async Task<Result<IReadOnlyList<CommunityInitiativeUpdateResponse>>> Handle(
        ListCommunityInitiativeUpdatesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var target = await CommunityInitiativeAccess.ResolveAsync(
            query.IdOrSlug, query.InitiativeId, _communities, _memberships, _initiatives, _access,
            cancellationToken);

        if (target.IsFailure)
        {
            return Result.Failure<IReadOnlyList<CommunityInitiativeUpdateResponse>>(target.Error);
        }

        var (readable, initiative) = target.Value;

        var updates = await _initiatives.ListUpdatesAsync(initiative.Id, Limit, cancellationToken);

        var names = await CommunityInitiativeAccess.NameableAsync(
            readable, [.. updates.Select(update => update.AuthorId)], _initiatives, _profiles, _access,
            cancellationToken);

        return Result.Success<IReadOnlyList<CommunityInitiativeUpdateResponse>>(
            [.. updates.Select(update => CommunityInitiativeAccess.ToResponse(update, names))]);
    }
}

/// <summary>Post a progress update. Its organisers, or anybody with canManageInitiatives.</summary>
[RequirePermission(CommunityProgrammePermissions.Participate)]
public sealed record PostCommunityInitiativeUpdateCommand : ICommand<CommunityInitiativeUpdateResponse>
{
    public required string IdOrSlug { get; init; }

    public required Guid InitiativeId { get; init; }

    public required PostCommunityInitiativeUpdateRequest Request { get; init; }
}

public sealed class PostCommunityInitiativeUpdateCommandHandler
    : ICommandHandler<PostCommunityInitiativeUpdateCommand, CommunityInitiativeUpdateResponse>
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly ICommunityInitiativeRepository _initiatives;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IMemberProfileService _profiles;
    private readonly IUnitOfWork _unitOfWork;

    public PostCommunityInitiativeUpdateCommandHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        ICommunityInitiativeRepository initiatives,
        CommunityProgrammeAccessService access,
        IMemberProfileService profiles,
        IUnitOfWork unitOfWork)
    {
        _communities = communities;
        _memberships = memberships;
        _initiatives = initiatives;
        _access = access;
        _profiles = profiles;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CommunityInitiativeUpdateResponse>> Handle(
        PostCommunityInitiativeUpdateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CommunityInitiativeUpdateResponse>(caller.Error);
        }

        var target = await CommunityInitiativeAccess.ResolveAsync(
            command.IdOrSlug, command.InitiativeId, _communities, _memberships, _initiatives, _access,
            cancellationToken);

        if (target.IsFailure)
        {
            return Result.Failure<CommunityInitiativeUpdateResponse>(target.Error);
        }

        var (readable, initiative) = target.Value;

        if (!CommunityInitiativeAccess.HasAuthority(initiative, readable, caller.Value))
        {
            return Result.Failure<CommunityInitiativeUpdateResponse>(CommunityInitiativeErrors.CannotPostUpdate);
        }

        var posted = CommunityInitiativeUpdate.Post(
            initiative, caller.Value, command.Request.Title, command.Request.Body, DateTimeOffset.UtcNow);

        if (posted.IsFailure)
        {
            return Result.Failure<CommunityInitiativeUpdateResponse>(posted.Error);
        }

        _initiatives.AddUpdate(posted.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var names = await CommunityInitiativeAccess.NameableAsync(
            readable, [caller.Value], _initiatives, _profiles, _access, cancellationToken);

        return Result.Success(CommunityInitiativeAccess.ToResponse(posted.Value, names));
    }
}

internal static class CommunityInitiativeAccess
{
    /// <summary>Null for no status. Names only — a number would be an enum's ordinal leaking onto the wire.</summary>
    public static Result<CommunityInitiativeStatus?> ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Success<CommunityInitiativeStatus?>(null);
        }

        var trimmed = value.Trim();

        if (int.TryParse(trimmed, out _)
            || !Enum.TryParse<CommunityInitiativeStatus>(trimmed, ignoreCase: true, out var status)
            || !Enum.IsDefined(status))
        {
            return Result.Failure<CommunityInitiativeStatus?>(CommunityInitiativeErrors.StatusUnknown);
        }

        return Result.Success<CommunityInitiativeStatus?>(status);
    }

    /// <summary>The community, if the caller may read its initiatives, and the initiative in it.</summary>
    public static async Task<Result<(ReadableCommunity Readable, CommunityInitiative Initiative)>> ResolveAsync(
        string idOrSlug,
        Guid initiativeId,
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        ICommunityInitiativeRepository initiatives,
        CommunityProgrammeAccessService access,
        CancellationToken cancellationToken)
    {
        var readable = await CommunityNoticeAccess.ResolveReaderAsync(
            idOrSlug, communities, memberships, access, cancellationToken);

        if (readable is null)
        {
            return Result.Failure<(ReadableCommunity, CommunityInitiative)>(CommunityErrors.NotFound);
        }

        var initiative = await initiatives.GetAsync(
            readable.Community.Id, CommunityInitiativeId.From(initiativeId), cancellationToken);

        return initiative is null
            ? Result.Failure<(ReadableCommunity, CommunityInitiative)>(CommunityInitiativeErrors.NotFound)
            : Result.Success((readable, initiative));
    }

    /// <summary>A current member of a community that has not closed for good.</summary>
    public static bool IsActingMember(ReadableCommunity readable) =>
        readable.Membership?.IsCurrentMember == true
        && readable.Community.Status != CommunityStatus.Archived;

    /// <summary>
    /// May this caller change the initiative or post to it: <c>CanManageInitiatives</c>, or one of
    /// its organisers who still belongs. Derived without the staff bypass.
    /// </summary>
    public static bool HasAuthority(CommunityInitiative initiative, ReadableCommunity readable, UserId? caller)
    {
        if (caller is null)
        {
            return false;
        }

        if (CommunityCapabilitySet.For(readable.Community, readable.Membership).CanManageInitiatives)
        {
            return true;
        }

        return initiative.IsOrganiser(caller) && IsActingMember(readable);
    }

    /// <summary>
    /// Each named organiser must be a current member whose membership the community can see — an
    /// organiser is named to it, and a Private membership is one the member asked nobody to see.
    /// </summary>
    public static async Task<Result<IReadOnlyCollection<UserId>>> ValidateOrganisersAsync(
        ReadableCommunity readable,
        IReadOnlyCollection<Guid> requested,
        ICommunityInitiativeRepository initiatives,
        CancellationToken cancellationToken)
    {
        var ids = requested.Distinct().ToList();

        if (ids.Count == 0)
        {
            return Result.Failure<IReadOnlyCollection<UserId>>(CommunityInitiativeErrors.OrganiserRequired);
        }

        if (ids.Count > CommunityInitiative.MaximumOrganisers)
        {
            return Result.Failure<IReadOnlyCollection<UserId>>(CommunityInitiativeErrors.TooManyOrganisers);
        }

        var organisers = ids.Select(UserId.From).ToList();
        var rows = await initiatives.ListMembershipsAsync(readable.Community.Id, organisers, cancellationToken);
        var byMember = rows.ToDictionary(row => row.MemberId.Value);

        foreach (var id in ids)
        {
            if (!byMember.TryGetValue(id, out var membership) || !membership.IsCurrentMember)
            {
                return Result.Failure<IReadOnlyCollection<UserId>>(CommunityInitiativeErrors.OrganiserNotMember);
            }

            if (membership.Visibility == CommunityMembershipVisibility.Private)
            {
                return Result.Failure<IReadOnlyCollection<UserId>>(CommunityInitiativeErrors.OrganiserNotVisible);
            }
        }

        return Result.Success<IReadOnlyCollection<UserId>>(organisers);
    }

    /// <summary>
    /// The people among <paramref name="people"/> this caller may see named, with their community
    /// standing: current members only, Public to anybody, CommunityOnly to members, and the caller
    /// themself always.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, (MemberProfile Profile, CommunityMembership Membership)>> NameableAsync(
        ReadableCommunity readable,
        IReadOnlyCollection<UserId> people,
        ICommunityInitiativeRepository initiatives,
        IMemberProfileService profiles,
        CommunityProgrammeAccessService access,
        CancellationToken cancellationToken)
    {
        var empty = new Dictionary<Guid, (MemberProfile, CommunityMembership)>();

        if (people.Count == 0)
        {
            return empty;
        }

        var callerIsMember = readable.Membership?.IsCurrentMember == true;
        var caller = access.CallerId;

        var rows = await initiatives.ListMembershipsAsync(readable.Community.Id, people, cancellationToken);

        var visible = rows
            .Where(row => row.IsCurrentMember
                && (row.Visibility == CommunityMembershipVisibility.Public
                    || (callerIsMember && row.Visibility == CommunityMembershipVisibility.CommunityOnly)
                    || (caller is not null && row.MemberId == caller)))
            .ToList();

        if (visible.Count == 0)
        {
            return empty;
        }

        var names = await profiles.ResolveAsync([.. visible.Select(row => row.MemberId)], cancellationToken);

        // A row with no profile is an account that no longer exists; there is nobody to name.
        return visible
            .Where(row => names.ContainsKey(row.MemberId.Value))
            .ToDictionary(row => row.MemberId.Value, row => (names[row.MemberId.Value], row));
    }

    public static async Task<CommunityInitiativeResponse> ComposeOneAsync(
        ReadableCommunity readable,
        CommunityInitiative initiative,
        ICommunityInitiativeRepository initiatives,
        IMemberProfileService profiles,
        CommunityProgrammeAccessService access,
        CancellationToken cancellationToken) =>
        (await ComposeAsync(readable, [initiative], initiatives, profiles, access, cancellationToken))[0];

    public static async Task<IReadOnlyList<CommunityInitiativeResponse>> ComposeAsync(
        ReadableCommunity readable,
        IReadOnlyList<CommunityInitiative> list,
        ICommunityInitiativeRepository initiatives,
        IMemberProfileService profiles,
        CommunityProgrammeAccessService access,
        CancellationToken cancellationToken)
    {
        if (list.Count == 0)
        {
            return [];
        }

        var ids = list.Select(initiative => initiative.Id).ToList();
        var participants = await initiatives.CountParticipantsAsync(readable.Community.Id, ids, cancellationToken);
        var updates = await initiatives.CountUpdatesAsync(ids, cancellationToken);

        var caller = access.CallerId;
        var joined = caller is null
            ? new HashSet<Guid>()
            : await initiatives.ListJoinedAsync(caller, ids, cancellationToken);

        var organisers = list
            .SelectMany(initiative => initiative.OrganiserIds)
            .Distinct()
            .Select(UserId.From)
            .ToList();

        var names = await NameableAsync(readable, organisers, initiatives, profiles, access, cancellationToken);
        var actingMember = IsActingMember(readable);

        return
        [
            .. list.Select(initiative =>
            {
                var authority = HasAuthority(initiative, readable, caller);
                var isParticipant = joined.Contains(initiative.Id.Value);

                return new CommunityInitiativeResponse
                {
                    Id = initiative.Id.Value,
                    CommunityId = initiative.CommunityId.Value,
                    Title = initiative.Title,
                    Description = initiative.Description,
                    Status = initiative.Status.ToString(),
                    TargetDate = initiative.TargetDate,
                    StartedAt = initiative.StartedAt,
                    ClosedAt = initiative.ClosedAt,
                    Organisers =
                    [
                        .. initiative.OrganiserIds
                            .Where(names.ContainsKey)
                            .Select(id => new CommunityRoleHolderResponse
                            {
                                UserId = id,
                                DisplayName = names[id].Profile.DisplayName,
                                Role = CommunityNoticeAccess.HighestRole(names[id].Membership).ToString(),
                                Since = names[id].Membership.JoinedAt
                            })
                    ],
                    ParticipantCount = participants.GetValueOrDefault(initiative.Id.Value),
                    UpdateCount = updates.GetValueOrDefault(initiative.Id.Value),
                    IsParticipant = isParticipant,
                    IsOrganiser = initiative.IsOrganiser(caller),
                    CanJoin = actingMember && initiative.AcceptsParticipants && !isParticipant,
                    CanEdit = authority && !initiative.IsClosed,
                    CanPostUpdate = authority && initiative.AcceptsUpdates,
                    CreatedAt = initiative.CreatedAt,
                    UpdatedAt = initiative.ModifiedAt ?? initiative.CreatedAt
                };
            })
        ];
    }

    public static CommunityInitiativeUpdateResponse ToResponse(
        CommunityInitiativeUpdate update,
        IReadOnlyDictionary<Guid, (MemberProfile Profile, CommunityMembership Membership)> names) => new()
    {
        Id = update.Id.Value,
        InitiativeId = update.InitiativeId.Value,
        Title = update.Title,
        Body = update.Body,
        PostedAt = update.PostedAt,
        Author = names.TryGetValue(update.AuthorId.Value, out var author)
            ? new CommunityInitiativeAuthorResponse
            {
                UserId = update.AuthorId.Value,
                DisplayName = author.Profile.DisplayName
            }
            : null
    };
}
