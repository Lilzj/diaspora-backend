using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Application.Services;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Requests;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;
using Diaspora.Modules.CommunityProgramme.Domain.Enums;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Policies;
using Diaspora.Modules.CommunityProgramme.Domain.Polls;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.CommunityProgramme.Application.Polls;

/// <summary>
/// Casts or changes a vote.
/// <para>
/// <b>The response carries the result, not the vote.</b> The register: "the UI shows what the server
/// counted rather than what the device sent". A handler that echoed the selections back would let a
/// client render a tally it had not been told.
/// </para>
/// <para>
/// <b>Four refusals, and each is one the register names.</b> 403 outside the audience, 409 on a
/// repeat where changes are disallowed, 409 on a closed poll, 400 on too many selections. Every one
/// of them is decided here, because one-member-one-vote is unenforceable on a device.
/// </para>
/// <para>
/// <b>The uniqueness guarantee is the index, not this handler.</b> The lookup below catches the
/// ordinary repeat — the same phone, a second later — and two devices voting in the same instant both
/// pass it. What stops them is the unique index on (poll, fingerprint), and what turns the resulting
/// violation into a 409 rather than a 500 is the idempotency guard.
/// </para>
/// </summary>
[Authorize(Permission = CommunityProgrammePermissions.Participate)]
[Transactional(Reason =
    "Writes the response, its selections and the poll's response counter as one unit. A vote that "
    + "committed without incrementing the counter would make the poll under-report itself, and a "
    + "counter that moved without the vote would over-report it.")]
public sealed record SubmitPollResponseCommand : ICommand<PollResultResponse>
{
    public required Guid PollId { get; init; }

    public required SubmitPollResponseRequest Request { get; init; }

    public string? IdempotencyKeyHeader { get; init; }
}

public sealed class SubmitPollResponseCommandValidator
    : AbstractValidator<SubmitPollResponseCommand>
{
    public SubmitPollResponseCommandValidator()
    {
        RuleFor(command => command.PollId).NotEmpty();
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.OpenResponse)
            .MaximumLength(CommunityProgrammeLimits.MaxOpenResponseLength);
    }
}

public sealed class SubmitPollResponseCommandHandler
    : ICommandHandler<SubmitPollResponseCommand, PollResultResponse>
{
    private const string Operation = "poll-vote";

    private readonly ICommunityPollRepository _polls;
    private readonly CommunityProgrammeAccessService _access;
    private readonly PollAudienceResolver _audience;
    private readonly ProgrammeIdempotency _commit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly PollResultReader _results;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public SubmitPollResponseCommandHandler(
        ICommunityPollRepository polls,
        CommunityProgrammeAccessService access,
        PollAudienceResolver audience,
        ProgrammeIdempotency commit,
        IUnitOfWork unitOfWork,
        PollResultReader results,
        IGuidGenerator guids,
        IClock clock)
    {
        _polls = polls;
        _access = access;
        _audience = audience;
        _commit = commit;
        _unitOfWork = unitOfWork;
        _results = results;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<PollResultResponse>> Handle(
        SubmitPollResponseCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<PollResultResponse>(caller.Error);
        }

        var poll = await _polls.GetAsync(CommunityPollId.From(command.PollId), cancellationToken);

        if (poll is null)
        {
            return Result.Failure<PollResultResponse>(CommunityProgrammeErrors.PollNotFound);
        }

        // Computed before the key, because the key is scoped by it: an anonymous poll stores no
        // voter, so neither may the vote's key.
        var fingerprint = _audience.Fingerprint(poll.Id, caller.Value);

        var key = ProgrammeIdempotency.NormaliseForVoter(
            Operation, fingerprint, command.IdempotencyKeyHeader, command.Request.IdempotencyKey);

        if (key is not null
            && await _polls.FindResponseByIdempotencyKeyAsync(fingerprint, key, cancellationToken) is not null)
        {
            // A retried vote is the same vote. Answering with the result rather than a 409 is what
            // makes a flaky connection harmless.
            return await ResultFor(poll, cancellationToken);
        }

        if (!poll.AcceptsVotes)
        {
            return Result.Failure<PollResultResponse>(CommunityProgrammeErrors.PollNotOpen);
        }

        if (!await _audience.IsInAudienceAsync(poll, caller.Value, cancellationToken))
        {
            return Result.Failure<PollResultResponse>(CommunityProgrammeErrors.OutsideAudience);
        }

        var selections = (command.Request.SelectedOptionIds ?? [])
            .Distinct()
            .Select(PollOptionId.From)
            .ToList();

        var validated = Validate(poll, selections, command.Request.OpenResponse);

        if (validated.IsFailure)
        {
            return Result.Failure<PollResultResponse>(validated.Error);
        }

        var existing = await _polls.FindResponseAsync(poll.Id, fingerprint, cancellationToken);

        if (existing is not null)
        {
            if (!poll.AllowsChange)
            {
                return Result.Failure<PollResultResponse>(CommunityProgrammeErrors.AlreadyVoted);
            }

            // A changed vote is one vote that changed, not a second vote. The counter does not move.
            existing.Recast(selections, command.Request.OpenResponse, _clock.UtcNow);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return await ResultFor(poll, cancellationToken);
        }

        var response = PollResponse.Cast(
            PollResponseId.From(_guids.NewSequentialGuid()),
            poll.Id,

            // Structurally absent on an anonymous poll: there is no identifier to store, not an
            // identifier that is hidden. See PollResponse's own documentation.
            poll.IsAnonymous ? null : caller.Value,
            fingerprint,
            selections,
            command.Request.OpenResponse,
            _clock.UtcNow,
            key);

        _polls.AddResponse(response);
        poll.RecordResponse(1, _clock.UtcNow);

        var committed = await ProgrammeIdempotency.GuardAsync(
            key,
            token => _unitOfWork.SaveChangesAsync(token),
            token => key is null
                ? Task.FromResult<PollResponse?>(null)
                : _polls.FindResponseByIdempotencyKeyAsync(fingerprint, key, token),
            cancellationToken,
            inTransaction: true);

        if (committed.IsFailure)
        {
            return Result.Failure<PollResultResponse>(committed.Error);
        }

        return await ResultFor(poll, cancellationToken);
    }

    /// <summary>
    /// The voting-type rules, in one place.
    /// <para>
    /// A single-choice poll takes one option. A multiple-choice poll takes up to its own maximum. An
    /// open-response poll takes text. Each refusal is a 400 with the reason, which is what the client
    /// renders — §51's "too many selections" is a sentence, not a code.
    /// </para>
    /// </summary>
    private static Result Validate(
        CommunityPoll poll, List<PollOptionId> selections, string? openResponse)
    {
        if (poll.VotingType == PollVotingType.OpenResponse)
        {
            return string.IsNullOrWhiteSpace(openResponse)
                ? Result.Failure(CommunityProgrammeErrors.OpenResponseRequired)
                : Result.Success();
        }

        if (selections.Count == 0)
        {
            return Result.Failure(CommunityProgrammeErrors.SelectionRequired);
        }

        foreach (var selection in selections)
        {
            if (!poll.HasOption(selection))
            {
                return Result.Failure(CommunityProgrammeErrors.PollOptionNotFound);
            }
        }

        var maximum = poll.VotingType == PollVotingType.SingleChoice
            ? 1
            : poll.MaxSelections ?? poll.Options.Count;

        return selections.Count > maximum
            ? Result.Failure(CommunityProgrammeErrors.TooManySelections)
            : Result.Success();
    }

    /// <summary>
    /// The result, read through the same service the results route uses.
    /// <para>
    /// <b>A shared service rather than a dispatched query, and <c>DSP1001</c> is why.</b> An earlier
    /// version sent <c>GetPollResultQuery</c> through <c>ISender</c>, which is a handler reaching
    /// another handler — forbidden, and forbidden for a reason: a handler that can reach any other
    /// handler can reach one whose authorization it has not performed. Sharing
    /// <see cref="PollResultReader"/> keeps the arithmetic and the visibility rule in one place while
    /// each caller does its own authorization.
    /// </para>
    /// <para>
    /// <b>A voter is inside the audience by the time this runs</b> — the guards above established it —
    /// so <c>canManage</c> is false here and the reader applies the poll's own visibility rule.
    /// </para>
    /// </summary>
    private async Task<Result<PollResultResponse>> ResultFor(
        CommunityPoll poll, CancellationToken cancellationToken) =>
        Result.Success(await _results.ReadAsync(poll, canManage: false, cancellationToken));
}
