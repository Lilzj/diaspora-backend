using Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;
using Diaspora.Modules.CommunityProgramme.Domain.Enums;
using Diaspora.Modules.CommunityProgramme.Domain.Polls;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.CommunityProgramme.Application.Services;

/// <summary>
/// Computes a poll's result.
/// <para>
/// <b>A service rather than a query one handler sends to another, and <c>DSP1001</c> is why.</b> The
/// vote command answers with the result — the register: "the UI shows what the server counted rather
/// than what the device sent" — and the results route answers with the same thing. An earlier version
/// had the command dispatch the query through <c>ISender</c>, which is a handler reaching another
/// handler: the rule forbids it, and the reason it forbids it is real. A handler that can reach any
/// other handler can reach one whose authorization it has not performed, and the call site does not
/// look like it did anything.
/// </para>
/// <para>
/// <b>What is shared is the arithmetic and the visibility rule, not the authorization.</b> Each caller
/// decides who may see the poll before asking for its numbers, which is why this takes an
/// already-decided <c>canManage</c> rather than resolving one.
/// </para>
/// </summary>
public sealed class PollResultReader
{
    private readonly ICommunityPollRepository _polls;
    private readonly PollAudienceResolver _audience;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IClock _clock;

    public PollResultReader(
        ICommunityPollRepository polls,
        PollAudienceResolver audience,
        CommunityProgrammeAccessService access,
        IClock clock)
    {
        _polls = polls;
        _audience = audience;
        _access = access;
        _clock = clock;
    }

    /// <summary>
    /// The tally, or the reason it is being withheld.
    /// <para>
    /// <b>A poll withholding its results returns <c>Options</c> null, not an empty array.</b> An empty
    /// array renders as "nobody voted", which is a different and false statement — and the client
    /// draws <c>ResultsHiddenReason</c> in its place.
    /// </para>
    /// </summary>
    public async Task<PollResultResponse> ReadAsync(
        CommunityPoll poll, bool canManage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(poll);

        var total = await _polls.CountResponsesAsync(poll.Id, cancellationToken);

        // A manager always sees the numbers — they are the ones who decide to publish them. Everybody
        // else sees them when the poll says so.
        if (!poll.ResultsAreVisible && !canManage)
        {
            return new PollResultResponse
            {
                PollId = poll.Id.Value,
                Status = poll.Status.ToString(),
                Options = null,
                TotalResponses = total,
                AreResultsVisible = false,
                ResultsHiddenReason = poll.Status == PollStatus.Open
                    ? "Results will be published when this poll closes."
                    : "The organizer has not published the results yet.",
                ComputedAt = _clock.UtcNow
            };
        }

        var tally = await _polls.TallyAsync(poll.Id, cancellationToken);

        var myResponse = _access.CallerId is { } caller && !poll.IsAnonymous
            ? await _polls.FindResponseAsync(
                poll.Id, _audience.Fingerprint(poll.Id, caller), cancellationToken)
            : null;

        var mine = myResponse?.Selections
            .Select(selection => selection.OptionId)
            .ToHashSet() ?? [];

        // The denominator is the number of responses, not the number of selections. On a
        // multiple-choice poll the percentages therefore sum to more than a hundred, which is correct
        // and is what "62% of people chose this" means.
        var denominator = Math.Max(1, total);

        var options = poll.Options
            .Select(option =>
            {
                var count = tally.TryGetValue(option.Id, out var value) ? value : 0;

                return new PollOptionResultResponse
                {
                    OptionId = option.Id.Value,
                    Label = option.Label,
                    Count = count,
                    Percentage = Math.Round(count * 100m / denominator, 1),
                    IsMyChoice = poll.IsAnonymous ? null : mine.Contains(option.Id)
                };
            })
            .ToList();

        var openResponses = poll.VotingType == PollVotingType.OpenResponse
            ? await _polls.ListOpenResponsesAsync(poll.Id, 200, cancellationToken)
            : null;

        return new PollResultResponse
        {
            PollId = poll.Id.Value,
            Status = poll.Status.ToString(),
            Options = options,
            TotalResponses = total,
            OpenResponses = openResponses,
            AreResultsVisible = true,
            ComputedAt = _clock.UtcNow
        };
    }
}
