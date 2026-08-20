using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Domain.Polls;

/// <summary>
/// A question attached to a post.
/// <para>
/// A child of <see cref="Posts.CommunityPost"/> rather than an aggregate of its own, because a
/// poll has no life without its post: it is created with it, deleted with it, and its audience
/// is the post's audience. Individual votes <em>are</em> a separate aggregate, for the usual
/// reason — a poll with four thousand votes must not be loaded to cast the four thousand and
/// first.
/// </para>
/// <para>
/// <b>The client has no polls.</b> This is one of the ten intentional deviations the frontend
/// contract report records: the brief requires them, B9 and B13 will need them, and shipping
/// the capability now costs less than retrofitting a post model that never anticipated one.
/// </para>
/// </summary>
public sealed class Poll : Entity<PollId>
{
    public const int MaxQuestionLength = 300;

    private readonly List<PollOption> _options = [];

    private Poll(
        PollId id,
        CommunityPostId postId,
        string question,
        bool allowsMultiple,
        PollResultVisibility resultVisibility,
        DateTimeOffset? closesAt) : base(id)
    {
        PostId = postId;
        Question = question;
        AllowsMultiple = allowsMultiple;
        ResultVisibility = resultVisibility;
        ClosesAt = closesAt;
        Status = PollStatus.Open;
    }

    private Poll() { }

    public CommunityPostId PostId { get; private set; } = default!;

    public string Question { get; private set; } = string.Empty;

    public bool AllowsMultiple { get; private set; }

    public PollResultVisibility ResultVisibility { get; private set; }

    public PollStatus Status { get; private set; }

    public DateTimeOffset? ClosesAt { get; private set; }

    public int TotalVotes { get; private set; }

    /// <summary>Distinct voters. Differs from <see cref="TotalVotes"/> on a multi-choice poll.</summary>
    public int VoterCount { get; private set; }

    public IReadOnlyList<PollOption> Options =>
        _options.OrderBy(option => option.Position).ToList().AsReadOnly();

    public static Result<Poll> Create(
        CommunityPostId postId,
        string question,
        IReadOnlyList<(string Label, MediaId? ImageMediaId)> options,
        bool allowsMultiple,
        PollResultVisibility resultVisibility,
        DateTimeOffset? closesAt)
    {
        ArgumentNullException.ThrowIfNull(postId);
        ArgumentNullException.ThrowIfNull(options);

        if (options.Count < CommunityLimits.MinPollOptions ||
            options.Count > CommunityLimits.MaxPollOptions)
        {
            return Result.Failure<Poll>(CommunityErrors.PollOptionCount(
                CommunityLimits.MinPollOptions, CommunityLimits.MaxPollOptions));
        }

        var trimmed = (question ?? string.Empty).Trim();

        var poll = new Poll(
            PollId.New(),
            postId,
            trimmed.Length > MaxQuestionLength ? trimmed[..MaxQuestionLength] : trimmed,
            allowsMultiple,
            resultVisibility,
            closesAt);

        for (var index = 0; index < options.Count; index++)
        {
            poll._options.Add(
                PollOption.Create(poll.Id, options[index].Label, index, options[index].ImageMediaId));
        }

        return Result.Success(poll);
    }

    public bool IsOpen(DateTimeOffset now) =>
        Status == PollStatus.Open && (ClosesAt is null || ClosesAt > now);

    /// <summary>
    /// Whether a given member may see the tallies.
    /// <para>
    /// Enforced in the domain rather than left to a projection, because it is the only privacy
    /// property a poll has. Showing a running tally before somebody votes measurably moves their
    /// vote, which turns a question about whether a neighbourhood feels safe into an echo of
    /// whoever answered first.
    /// </para>
    /// </summary>
    public bool CanSeeResults(bool hasVoted, DateTimeOffset now) => ResultVisibility switch
    {
        PollResultVisibility.Always => true,
        PollResultVisibility.AfterVoting => hasVoted || !IsOpen(now),
        PollResultVisibility.AfterClosing => !IsOpen(now),
        _ => hasVoted
    };

    internal Result RegisterVote(PollOptionId optionId, bool isNewVoter, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(optionId);

        if (!IsOpen(now))
        {
            return Result.Failure(CommunityErrors.PollClosed);
        }

        var option = _options.Find(candidate => candidate.Id == optionId);

        if (option is null)
        {
            return Result.Failure(CommunityErrors.PollOptionNotFound);
        }

        option.AdjustVotes(1);
        TotalVotes++;

        if (isNewVoter)
        {
            VoterCount++;
        }

        return Result.Success();
    }

    internal void WithdrawVote(PollOptionId optionId, bool voterHasNoRemainingVotes)
    {
        var option = _options.Find(candidate => candidate.Id == optionId);

        option?.AdjustVotes(-1);
        TotalVotes = Math.Max(0, TotalVotes - 1);

        if (voterHasNoRemainingVotes)
        {
            VoterCount = Math.Max(0, VoterCount - 1);
        }
    }

    public void Close(DateTimeOffset now)
    {
        Status = PollStatus.Closed;
        ClosesAt ??= now;
    }

    public void Cancel() => Status = PollStatus.Cancelled;
}
