using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Application.Mapping;
using Diaspora.Modules.Creator.Application.Services;
using Diaspora.Modules.Creator.Contracts.V1.Requests;
using Diaspora.Modules.Creator.Contracts.V1.Responses;
using Diaspora.Modules.Creator.Domain.Errors;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Milestones;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.Creator.Application.Milestones;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// CREATOR-GAP-003 — creator milestones.
//
// Two routes. The register's objection to the client doing this is the shape of both:
//
//   "Computing completion on the device would put the client in charge of when a reward is owed."
//
// So completion is a stored state, the claim checks it, and the reward is published exactly once —
// carried by a grant reference that B15 de-duplicates on even if the message is redelivered.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// The milestone catalogue with this creator's standing against each.
/// <para>
/// <b>Every milestone in the catalogue appears, whether or not this creator has a row.</b> A
/// milestone nobody has started is <c>Locked</c> at 0% rather than absent — a catalogue that only
/// showed what you had begun would not be a catalogue.
/// </para>
/// </summary>
[Authorize(Permission = CreatorPermissions.ProfileRead)]
public sealed record GetMyMilestonesQuery : IQuery<IReadOnlyList<CreatorMilestoneResponse>>;

public sealed class GetMyMilestonesQueryHandler
    : IQueryHandler<GetMyMilestonesQuery, IReadOnlyList<CreatorMilestoneResponse>>
{
    private readonly CreatorContext _context;
    private readonly ICreatorMilestoneRepository _milestones;
    private readonly ICreatorMilestoneProgressRepository _progress;

    public GetMyMilestonesQueryHandler(
        CreatorContext context,
        ICreatorMilestoneRepository milestones,
        ICreatorMilestoneProgressRepository progress)
    {
        _context = context;
        _milestones = milestones;
        _progress = progress;
    }

    public async Task<Result<IReadOnlyList<CreatorMilestoneResponse>>> Handle(
        GetMyMilestonesQuery query, CancellationToken cancellationToken)
    {
        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<CreatorMilestoneResponse>>(caller.Error);
        }

        var definitions = await _milestones.ListActiveAsync(cancellationToken);
        var creator = await _context.FindCreatorAsync(cancellationToken);

        var rows = creator is null
            ? []
            : (await _progress.ListForCreatorAsync(creator.Id, cancellationToken))
                .ToDictionary(row => row.MilestoneId);

        var response = definitions
            .Select(definition => definition.ToResponse(
                rows.TryGetValue(definition.Id, out var row) ? row : null))
            .OrderBy(milestone => milestone.SortOrder)
            .ToList();

        return Result.Success<IReadOnlyList<CreatorMilestoneResponse>>(response);
    }
}

/// <summary>
/// Claims a completed milestone.
/// <para>
/// <b>Three barriers against a double reward, and each catches a different race.</b> The state check
/// catches a second claim after the first committed. The unique index on the claim key catches two
/// concurrent claims carrying one key — one of them loses at the database and replays the other's
/// answer. And B15's own unique index on the grant reference catches a redelivered points grant,
/// which is the one neither of the first two can see.
/// </para>
/// </summary>
[Authorize(Permission = CreatorPermissions.Participate)]
[Auditable(Action = "creator.milestone.claim")]
public sealed record ClaimMilestoneCommand : ICommand<CreatorMilestoneResponse>
{
    public required Guid MilestoneId { get; init; }

    public ClaimRequest? Request { get; init; }

    public string? IdempotencyKeyHeader { get; init; }
}

public sealed class ClaimMilestoneCommandValidator : AbstractValidator<ClaimMilestoneCommand>
{
    public ClaimMilestoneCommandValidator() =>
        RuleFor(command => command.MilestoneId).NotEmpty();
}

public sealed class ClaimMilestoneCommandHandler
    : ICommandHandler<ClaimMilestoneCommand, CreatorMilestoneResponse>
{
    private const string Operation = "creator-milestone-claim";

    private readonly CreatorContext _context;
    private readonly ICreatorMilestoneRepository _milestones;
    private readonly ICreatorMilestoneProgressRepository _progress;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ClaimMilestoneCommandHandler(
        CreatorContext context,
        ICreatorMilestoneRepository milestones,
        ICreatorMilestoneProgressRepository progress,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _context = context;
        _milestones = milestones;
        _progress = progress;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<CreatorMilestoneResponse>> Handle(
        ClaimMilestoneCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var creator = await _context.RequireCreatorAsync(cancellationToken);

        if (creator.IsFailure)
        {
            return Result.Failure<CreatorMilestoneResponse>(creator.Error);
        }

        var allowed = CreatorContext.EnsureCanParticipate(creator.Value);

        if (allowed.IsFailure)
        {
            return Result.Failure<CreatorMilestoneResponse>(allowed.Error);
        }

        var definition = await _milestones.GetAsync(
            CreatorMilestoneId.From(command.MilestoneId), cancellationToken);

        if (definition is null || !definition.IsActive)
        {
            return Result.Failure<CreatorMilestoneResponse>(CreatorErrors.MilestoneNotFound);
        }

        var key = CreatorIdempotency.Normalise(
            Operation, creator.Value.UserId, command.IdempotencyKeyHeader, command.Request?.IdempotencyKey);

        if (key is not null)
        {
            var replayed = await _progress.FindByClaimKeyAsync(creator.Value.Id, key, cancellationToken);

            if (replayed is not null)
            {
                // A key the caller already spent on another milestone is not a retry of this one.
                return replayed.MilestoneId == definition.Id
                    ? Result.Success(definition.ToResponse(replayed))
                    : Result.Failure<CreatorMilestoneResponse>(CreatorErrors.IdempotencyKeyReused);
            }
        }

        var row = await _progress.FindAsync(creator.Value.Id, definition.Id, cancellationToken);

        if (row is null)
        {
            return Result.Failure<CreatorMilestoneResponse>(CreatorErrors.MilestoneNotComplete);
        }

        // Recomputed against the definition as it is now, so a re-seeded milestone whose target
        // moved is judged on today's rule rather than yesterday's.
        row.Recompute(definition, _clock.UtcNow);

        var reference = $"milestone:{definition.Key}:{creator.Value.Id.Value:N}";

        var claimed = row.Claim(definition, reference, key, _clock.UtcNow);

        if (claimed.IsFailure)
        {
            return Result.Failure<CreatorMilestoneResponse>(claimed.Error);
        }

        _progress.Update(row);

        var commit = await CreatorIdempotency.GuardAsync(
            key,
            token => _unitOfWork.SaveChangesAsync(token),
            token => _progress.FindByClaimKeyAsync(creator.Value.Id, key!, token),
            cancellationToken);

        if (commit.IsFailure)
        {
            return Result.Failure<CreatorMilestoneResponse>(commit.Error);
        }

        if (commit.Value.Replayed is { } raced && raced.MilestoneId != definition.Id)
        {
            return Result.Failure<CreatorMilestoneResponse>(CreatorErrors.IdempotencyKeyReused);
        }

        return Result.Success(definition.ToResponse(commit.Value.Replayed ?? row));
    }
}

/// <summary>
/// Records a recognised action against every milestone that counts it.
/// <para>
/// <b>Extracted as a service rather than left in the consumer, and DSP1001 is why:</b> a handler
/// that could reach another handler makes settlement order implicit. Both the activity consumer and
/// the milestone read need this behaviour, so it lives in one place that neither of them is.
/// </para>
/// <para>
/// Stages; the caller saves. A milestone that completed here raises
/// <c>CreatorMilestoneCompletedDomainEvent</c> from the aggregate, exactly once.
/// </para>
/// </summary>
public sealed class MilestoneProgressService
{
    private readonly ICreatorMilestoneRepository _milestones;
    private readonly ICreatorMilestoneProgressRepository _progress;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public MilestoneProgressService(
        ICreatorMilestoneRepository milestones,
        ICreatorMilestoneProgressRepository progress,
        IGuidGenerator guids,
        IClock clock)
    {
        _milestones = milestones;
        _progress = progress;
        _guids = guids;
        _clock = clock;
    }

    public async Task RecordAsync(
        Domain.Creators.CreatorProfile creator,
        string actionType,
        long occurrences,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creator);

        var definitions = await _milestones.ListForActionAsync(actionType, cancellationToken);

        foreach (var definition in definitions)
        {
            var row = await _progress.FindAsync(creator.Id, definition.Id, cancellationToken);
            var isNew = row is null;

            row ??= CreatorMilestoneProgress.Begin(
                CreatorMilestoneProgressId.From(_guids.NewSequentialGuid()),
                creator.Id,
                creator.UserId,
                definition,
                _clock.UtcNow);

            row.RecordActivity(definition, actionType, occurrences, _clock.UtcNow);

            // Add or Update, never both. Calling Update on a row that was just Added flips its state
            // from Added to Modified, EF skips the parent INSERT, and the failure surfaces as a
            // foreign-key violation naming the child table — a trap this platform has already paid
            // for once.
            if (isNew)
            {
                _progress.Add(row);
            }
            else
            {
                _progress.Update(row);
            }
        }
    }
}
