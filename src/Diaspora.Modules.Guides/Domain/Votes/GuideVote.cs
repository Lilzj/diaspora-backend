using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Events;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Domain.Votes;

/// <summary>
/// One member's helpfulness vote on one guide.
/// <para>
/// An aggregate of its own rather than a child of the guide, because two members voting at
/// once must not contend on the guide row. The handler that writes the vote adjusts the
/// guide's engagement rollup in the same transaction, using the event's previous-kind to
/// compute the delta.
/// </para>
/// <para>
/// <see cref="VoterKey"/> is the denormalised uniqueness key. It exists because an EF index
/// cannot span an owned type's columns and the aggregate's own — B6's lesson — and "one vote
/// per member per guide" is exactly such an index. The key is derived, deterministic, and the
/// database enforces it.
/// </para>
/// </summary>
public sealed class GuideVote : AggregateRoot<GuideVoteId>, IAuditableEntity
{
    private GuideVote(
        GuideVoteId id, GuideId guideId, UserId voterId, GuideVoteKind kind, DateTimeOffset createdAt)
        : base(id)
    {
        GuideId = guideId;
        VoterId = voterId;
        Kind = kind;
        VoterKey = KeyFor(guideId, voterId);
        CreatedAt = createdAt;
    }

    private GuideVote() { }

    public GuideId GuideId { get; private set; } = default!;

    public UserId VoterId { get; private set; } = default!;

    public GuideVoteKind Kind { get; private set; }

    /// <summary>The denormalised (guide, voter) key behind the unique index.</summary>
    public string VoterKey { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static Result<GuideVote> Cast(
        GuideVoteId id, GuideId guideId, UserId voterId, GuideVoteKind kind, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(guideId);
        ArgumentNullException.ThrowIfNull(voterId);

        var vote = new GuideVote(id, guideId, voterId, kind, now);

        vote.RaiseDomainEvent(new GuideVoteCastDomainEvent
        {
            VoteId = id,
            GuideId = guideId,
            VoterId = voterId,
            Kind = kind,
            PreviousKind = null
        });

        return Result.Success(vote);
    }

    /// <summary>
    /// A changed mind. Fails when nothing would change, so the handler never recomputes a
    /// rollup for a no-op.
    /// </summary>
    public Result ChangeKind(GuideVoteKind kind)
    {
        if (Kind == kind)
        {
            return Result.Failure(GuideErrors.VoteUnchanged);
        }

        var previous = Kind;
        Kind = kind;

        RaiseDomainEvent(new GuideVoteCastDomainEvent
        {
            VoteId = Id,
            GuideId = GuideId,
            VoterId = VoterId,
            Kind = kind,
            PreviousKind = previous
        });

        return Result.Success();
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

    private static string KeyFor(GuideId guideId, UserId voterId) =>
        $"{guideId.Value:N}:{voterId.Value:N}";
}
