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
using Diaspora.Modules.CommunityProgramme.Domain.Policies;
using Diaspora.Modules.CommunityProgramme.Domain.Polls;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.CommunityProgramme.Application.Polls;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// COMMUNITY-LIFE-GAP-012 — community polls, voting and results.
//
// Two contract details the register says the frontend depends on, and both are structural here
// rather than conventional:
//
//   "CommunityPoll must carry no per-option count." The aggregate has no such field, so no mapper can
//   leak one and no future refactor can add one by accident. Counts exist only on the result read.
//
//   "A poll withholding its results must return options: null, not an empty array." An empty array
//   renders as "nobody voted", which is a different and false statement. The result handler returns
//   null and a reason.
//
// One-member-one-vote is a unique index on (poll, voter fingerprint) rather than a check, because a
// check loses the race between two devices every time and the loser is the tally.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>The poll list, with the audience rule applied inside the query.</summary>
[Authorize(Permission = CommunityProgrammePermissions.Read)]
public sealed record ListPollsQuery : IQuery<CommunityPageResponse<CommunityPollResponse>>
{
    public Guid? OrganizationId { get; init; }

    public string? Audience { get; init; }

    public string? State { get; init; }

    public string? Query { get; init; }

    public string? Cursor { get; init; }

    public int? Take { get; init; }
}

public sealed class ListPollsQueryHandler
    : IQueryHandler<ListPollsQuery, CommunityPageResponse<CommunityPollResponse>>
{
    private readonly ICommunityPollRepository _polls;
    private readonly CommunityProgrammeAccessService _access;
    private readonly PollAudienceResolver _audience;

    public ListPollsQueryHandler(
        ICommunityPollRepository polls,
        CommunityProgrammeAccessService access,
        PollAudienceResolver audience)
    {
        _polls = polls;
        _access = access;
        _audience = audience;
    }

    public async Task<Result<CommunityPageResponse<CommunityPollResponse>>> Handle(
        ListPollsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var memberOf = await _access.MemberOfAsync(cancellationToken);

        var managed = await _access.ManagedForAsync(
            OrganizationCapability.ManagePolls, cancellationToken);

        var page = await _polls.SearchAsync(
            new PollQueryFilter
            {
                OrganizationId = query.OrganizationId,
                Audience = Enum.TryParse<PollAudience>(
                    query.Audience, ignoreCase: true, out var audience)
                        ? audience
                        : null,
                Statuses = ParseStatuses(query.State),
                MemberOfOrganizationIds = memberOf,
                ManagedOrganizationIds = managed,
                ViewerUserId = _access.CallerId,
                SearchTerm = query.Query,
                Cursor = query.Cursor,
                Limit = CommunityProgrammeLimits.PageSize(query.Take)
            },
            cancellationToken);

        if (page.Items.Count == 0)
        {
            return Result.Success(new CommunityPageResponse<CommunityPollResponse>
            {
                Items = [],
                HasMore = false
            });
        }

        var organizations = await _access.FindOrganizationsAsync(
            [.. page.Items.Where(poll => poll.OrganizationId is not null)
                .Select(poll => poll.OrganizationId!.Value)],
            cancellationToken);

        var rows = new List<CommunityPollResponse>();

        foreach (var poll in page.Items)
        {
            var myResponse = await FindMyResponseAsync(poll, cancellationToken);

            var reason = await _audience.CannotVoteReasonAsync(
                poll, _access.CallerId, myResponse is not null, cancellationToken);

            rows.Add(CommunityProgrammeMapper.ToResponse(
                poll,
                poll.OrganizationId is { } organizationId
                    ? organizations.GetValueOrDefault(organizationId)
                    : null,
                myResponse,
                reason,
                poll.OrganizationId is { } id && managed.Contains(id)));
        }

        return Result.Success(new CommunityPageResponse<CommunityPollResponse>
        {
            Items = rows,
            NextCursor = page.NextCursor,
            HasMore = page.HasMore
        });
    }

    private async Task<PollResponse?> FindMyResponseAsync(
        CommunityPoll poll, CancellationToken cancellationToken)
    {
        if (_access.CallerId is not { } caller)
        {
            return null;
        }

        return await _polls.FindResponseAsync(
            poll.Id, _audience.Fingerprint(poll.Id, caller), cancellationToken);
    }

    private static IReadOnlyList<PollStatus>? ParseStatuses(string? state) =>
        string.IsNullOrWhiteSpace(state)
            ? null
            : Enum.TryParse<PollStatus>(state, ignoreCase: true, out var parsed)
                ? [parsed]
                : [];
}

/// <summary>
/// One poll.
/// <para>
/// <b>403 outside the audience, and the register asks for exactly that.</b> Not a filtered response
/// and not a 404: a member who was sent a link to their organization's poll and is not in the
/// organization should be told they may not see it, which is different from being told it does not
/// exist.
/// </para>
/// </summary>
[Authorize(Permission = CommunityProgrammePermissions.Read)]
public sealed record GetPollQuery : IQuery<CommunityPollResponse>
{
    public required Guid PollId { get; init; }
}

public sealed class GetPollQueryHandler : IQueryHandler<GetPollQuery, CommunityPollResponse>
{
    private readonly ICommunityPollRepository _polls;
    private readonly CommunityProgrammeAccessService _access;
    private readonly PollAudienceResolver _audience;

    public GetPollQueryHandler(
        ICommunityPollRepository polls,
        CommunityProgrammeAccessService access,
        PollAudienceResolver audience)
    {
        _polls = polls;
        _access = access;
        _audience = audience;
    }

    public async Task<Result<CommunityPollResponse>> Handle(
        GetPollQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var poll = await _polls.GetAsync(CommunityPollId.From(query.PollId), cancellationToken);

        if (poll is null)
        {
            return Result.Failure<CommunityPollResponse>(CommunityProgrammeErrors.PollNotFound);
        }

        var canManage = poll.OrganizationId is { } organizationId
            && await _access.HoldsAsync(
                organizationId, OrganizationCapability.ManagePolls, cancellationToken);

        if (!poll.IsDiscoverable && !canManage)
        {
            return Result.Failure<CommunityPollResponse>(CommunityProgrammeErrors.PollNotFound);
        }

        if (!canManage
            && !await _audience.IsInAudienceAsync(poll, _access.CallerId, cancellationToken))
        {
            return Result.Failure<CommunityPollResponse>(CommunityProgrammeErrors.OutsideAudience);
        }

        var organization = poll.OrganizationId is { } id
            ? await _access.FindOrganizationAsync(id, cancellationToken)
            : null;

        var myResponse = _access.CallerId is { } caller
            ? await _polls.FindResponseAsync(
                poll.Id, _audience.Fingerprint(poll.Id, caller), cancellationToken)
            : null;

        var reason = await _audience.CannotVoteReasonAsync(
            poll, _access.CallerId, myResponse is not null, cancellationToken);

        return Result.Success(CommunityProgrammeMapper.ToResponse(
            poll, organization, myResponse, reason, canManage));
    }
}

/// <summary>
/// The tally.
/// <para>
/// <b>Computed in the database, and the client is never given raw votes.</b> §24 forbids computing an
/// authoritative result from incomplete client data; this route is what makes that possible rather
/// than merely required.
/// </para>
/// </summary>
[Authorize(Permission = CommunityProgrammePermissions.Read)]
public sealed record GetPollResultQuery : IQuery<PollResultResponse>
{
    public required Guid PollId { get; init; }
}

public sealed class GetPollResultQueryHandler
    : IQueryHandler<GetPollResultQuery, PollResultResponse>
{
    private readonly ICommunityPollRepository _polls;
    private readonly CommunityProgrammeAccessService _access;
    private readonly PollAudienceResolver _audience;
    private readonly PollResultReader _results;

    public GetPollResultQueryHandler(
        ICommunityPollRepository polls,
        CommunityProgrammeAccessService access,
        PollAudienceResolver audience,
        PollResultReader results)
    {
        _polls = polls;
        _access = access;
        _audience = audience;
        _results = results;
    }

    public async Task<Result<PollResultResponse>> Handle(
        GetPollResultQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var poll = await _polls.GetAsync(CommunityPollId.From(query.PollId), cancellationToken);

        if (poll is null)
        {
            return Result.Failure<PollResultResponse>(CommunityProgrammeErrors.PollNotFound);
        }

        var canManage = poll.OrganizationId is { } organizationId
            && await _access.HoldsAsync(
                organizationId, OrganizationCapability.ManagePolls, cancellationToken);

        if (!poll.IsDiscoverable && !canManage)
        {
            return Result.Failure<PollResultResponse>(CommunityProgrammeErrors.PollNotFound);
        }

        // The authorization is this handler's; the arithmetic is the reader's. See PollResultReader
        // for why the vote command shares the second and not the first.
        if (!canManage
            && !await _audience.IsInAudienceAsync(poll, _access.CallerId, cancellationToken))
        {
            return Result.Failure<PollResultResponse>(CommunityProgrammeErrors.OutsideAudience);
        }

        return Result.Success(await _results.ReadAsync(poll, canManage, cancellationToken));
    }
}

/// <summary>Creates a poll.</summary>
[Authorize(Permission = CommunityProgrammePermissions.ManagePolls)]
[Transactional(Reason =
    "Writes the poll, its options and its invitee list as one unit. A poll that committed without "
    + "its options would be a question nobody can answer, and the client would render it as broken.")]
public sealed record CreatePollCommand : ICommand<CommunityPollResponse>
{
    public required CreatePollRequest Request { get; init; }

    public string? IdempotencyKeyHeader { get; init; }
}

public sealed class CreatePollCommandValidator : AbstractValidator<CreatePollCommand>
{
    public CreatePollCommandValidator()
    {
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.Question)
            .NotEmpty()
            .MaximumLength(CommunityProgrammeLimits.MaxSummaryLength);

        RuleFor(command => command.Request.VotingType).NotEmpty();
        RuleFor(command => command.Request.Audience).NotEmpty();

        RuleFor(command => command.Request.Options)
            .Must(options => options is { Count: >= 2 })
            .When(command => !string.Equals(
                command.Request.VotingType, "OpenResponse", StringComparison.OrdinalIgnoreCase))
            .WithMessage("A poll with choices needs at least two of them.");

        RuleFor(command => command.Request.Options!.Count)
            .LessThanOrEqualTo(CommunityProgrammeLimits.MaxPollOptions)
            .When(command => command.Request.Options is not null);
    }
}

public sealed class CreatePollCommandHandler
    : ICommandHandler<CreatePollCommand, CommunityPollResponse>
{
    private readonly ICommunityPollRepository _polls;
    private readonly CommunityProgrammeAccessService _access;
    private readonly PollAudienceResolver _audience;
    private readonly ProgrammeIdempotency _commit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CreatePollCommandHandler(
        ICommunityPollRepository polls,
        CommunityProgrammeAccessService access,
        PollAudienceResolver audience,
        ProgrammeIdempotency commit,
        IUnitOfWork unitOfWork,
        IGuidGenerator guids,
        IClock clock)
    {
        _polls = polls;
        _access = access;
        _audience = audience;
        _commit = commit;
        _unitOfWork = unitOfWork;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<CommunityPollResponse>> Handle(
        CreatePollCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CommunityPollResponse>(caller.Error);
        }

        // An organization poll needs the capability in that organization. A poll with no
        // organization — a neighbourhood or country question — needs the platform permission the
        // attribute already checked, and nothing more: there is no body to hold a role in.
        if (command.Request.OrganizationId is { } organizationId)
        {
            var permitted = await _access.RequireCapabilityAsync(
                organizationId, OrganizationCapability.ManagePolls, cancellationToken);

            if (permitted.IsFailure)
            {
                return Result.Failure<CommunityPollResponse>(permitted.Error);
            }
        }

        if (!Enum.TryParse<PollVotingType>(
            command.Request.VotingType, ignoreCase: true, out var votingType))
        {
            return Result.Failure<CommunityPollResponse>(CommunityProgrammeErrors.Validation(
                $"'{command.Request.VotingType}' is not a voting type this platform supports."));
        }

        if (!Enum.TryParse<PollAudience>(
            command.Request.Audience, ignoreCase: true, out var audience))
        {
            return Result.Failure<CommunityPollResponse>(CommunityProgrammeErrors.Validation(
                $"'{command.Request.Audience}' is not an audience this platform recognises."));
        }

        var now = _clock.UtcNow;

        var drafted = CommunityPoll.Draft(
            CommunityPollId.From(_guids.NewSequentialGuid()),
            caller.Value,
            command.Request.Question,
            votingType,
            audience,
            now);

        if (drafted.IsFailure)
        {
            return Result.Failure<CommunityPollResponse>(drafted.Error);
        }

        var poll = drafted.Value;

        var described = poll.Describe(
            command.Request.Description,
            command.Request.OrganizationId,

            // Catalogue areas are retired (14 Oct 2026): an id from an older client is ignored.
            neighborhoodAreaId: null,
            command.Request.CountryCode,
            command.Request.MaxSelections,
            command.Request.IsAnonymous,
            command.Request.AllowsChange,
            command.Request.ShowResultsBeforeClose,
            command.Request.OpensAt,
            command.Request.ClosesAt,
            now,
            command.Request.CityName);

        if (described.IsFailure)
        {
            return Result.Failure<CommunityPollResponse>(described.Error);
        }

        var options = (command.Request.Options ?? [])
            .Take(CommunityProgrammeLimits.MaxPollOptions)
            .Select((option, index) => PollOption.Create(
                PollOptionId.From(_guids.NewSequentialGuid()),
                option.Label,
                option.Description,
                index))
            .ToList();

        if (options.Count > 0)
        {
            var replaced = poll.ReplaceOptions(options, now);

            if (replaced.IsFailure)
            {
                return Result.Failure<CommunityPollResponse>(replaced.Error);
            }
        }

        if (command.Request.InviteeUserIds is { Count: > 0 })
        {
            poll.ReplaceInvitees(
                command.Request.InviteeUserIds.Select(UserId.From), now);
        }

        if (command.Request.Publish)
        {
            var published = poll.Publish(now);

            if (published.IsFailure)
            {
                return Result.Failure<CommunityPollResponse>(published.Error);
            }
        }

        _polls.Add(poll);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var organization = poll.OrganizationId is { } id
            ? await _access.FindOrganizationAsync(id, cancellationToken)
            : null;

        var reason = await _audience.CannotVoteReasonAsync(
            poll, caller.Value, hasVoted: false, cancellationToken);

        return Result.Success(CommunityProgrammeMapper.ToResponse(
            poll, organization, myResponse: null, reason, canManage: true));
    }
}

/// <summary>Publishes, closes, cancels or publishes the results of a poll.</summary>
[Authorize(Permission = CommunityProgrammePermissions.ManagePolls)]
public sealed record ActOnPollCommand : ICommand<CommunityPollResponse>
{
    public required Guid PollId { get; init; }

    /// <summary><c>publish</c>, <c>close</c>, <c>cancel</c> or <c>publish-results</c>.</summary>
    public required string Action { get; init; }
}

public sealed class ActOnPollCommandHandler
    : ICommandHandler<ActOnPollCommand, CommunityPollResponse>
{
    private readonly ICommunityPollRepository _polls;
    private readonly CommunityProgrammeAccessService _access;
    private readonly PollAudienceResolver _audience;
    private readonly ProgrammeIdempotency _commit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ActOnPollCommandHandler(
        ICommunityPollRepository polls,
        CommunityProgrammeAccessService access,
        PollAudienceResolver audience,
        ProgrammeIdempotency commit,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _polls = polls;
        _access = access;
        _audience = audience;
        _commit = commit;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<CommunityPollResponse>> Handle(
        ActOnPollCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CommunityPollResponse>(caller.Error);
        }

        var poll = await _polls.GetAsync(CommunityPollId.From(command.PollId), cancellationToken);

        if (poll is null)
        {
            return Result.Failure<CommunityPollResponse>(CommunityProgrammeErrors.PollNotFound);
        }

        var permitted = await RequireManageAsync(poll, cancellationToken);

        if (permitted.IsFailure)
        {
            return Result.Failure<CommunityPollResponse>(permitted.Error);
        }

        var now = _clock.UtcNow;

        var moved = command.Action.ToLowerInvariant() switch
        {
            "publish" => poll.Publish(now),
            "close" => poll.MoveTo(PollStatus.Closed, now),
            "cancel" => poll.MoveTo(PollStatus.Cancelled, now),
            "publish-results" => poll.MoveTo(PollStatus.ResultsPublished, now),
            _ => Result.Failure(CommunityProgrammeErrors.Validation(
                $"'{command.Action}' is not something you can do to a poll."))
        };

        if (moved.IsFailure)
        {
            return Result.Failure<CommunityPollResponse>(moved.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var organization = poll.OrganizationId is { } id
            ? await _access.FindOrganizationAsync(id, cancellationToken)
            : null;

        var reason = await _audience.CannotVoteReasonAsync(
            poll, caller.Value, hasVoted: false, cancellationToken);

        return Result.Success(CommunityProgrammeMapper.ToResponse(
            poll, organization, myResponse: null, reason, canManage: true));
    }

    /// <summary>
    /// Whether the caller may run this poll.
    /// <para>
    /// An organization poll asks the capability. A poll with no organization is the creator's own,
    /// and only they may move it — the platform permission on the command says they may run polls
    /// somewhere, and this says which ones.
    /// </para>
    /// </summary>
    private async Task<Result> RequireManageAsync(
        CommunityPoll poll, CancellationToken cancellationToken)
    {
        if (poll.OrganizationId is { } organizationId)
        {
            return await _access.RequireCapabilityAsync(
                organizationId, OrganizationCapability.ManagePolls, cancellationToken);
        }

        return poll.CreatedByUserId == _access.CallerId
            ? Result.Success()
            : Result.Failure(CommunityProgrammeErrors.NotOwner);
    }
}
