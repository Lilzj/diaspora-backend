using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Polls;

/// <summary>
/// One member's choice of one option.
/// <para>
/// An aggregate of its own, not a child of the poll, for the reason every high-write social
/// record in this module is: a poll with four thousand votes would otherwise have to be
/// materialised in full to accept the next one, and two members voting at the same moment would
/// contend on the same row.
/// </para>
/// <para>
/// Carries the voter identifier because a member must be able to change or withdraw their vote,
/// and because one-vote-per-member is otherwise unenforceable. It is never projected: a poll
/// result is public, and who voted for what is not.
/// </para>
/// </summary>
public sealed class PollVote : AggregateRoot<PollVoteId>, IAuditableEntity
{
    private PollVote(
        PollVoteId id, PollId pollId, PollOptionId optionId, UserId voterId, DateTimeOffset castAt)
        : base(id)
    {
        PollId = pollId;
        OptionId = optionId;
        VoterId = voterId;
        CastAt = castAt;
    }

    private PollVote() { }

    public PollId PollId { get; private set; } = default!;

    public PollOptionId OptionId { get; private set; } = default!;

    public UserId VoterId { get; private set; } = default!;

    public DateTimeOffset CastAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    public static PollVote Cast(
        PollId pollId, PollOptionId optionId, UserId voterId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(pollId);
        ArgumentNullException.ThrowIfNull(optionId);
        ArgumentNullException.ThrowIfNull(voterId);

        return new PollVote(PollVoteId.New(), pollId, optionId, voterId, now);
    }

    /// <summary>Stamped by the shared persistence interceptor. Never called by a handler.</summary>
    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    /// <inheritdoc cref="SetCreated" />
    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }
}
