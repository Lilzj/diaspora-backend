using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Memberships;
using Diaspora.Modules.CommunityProgramme.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityProgramme.Domain.Polls;

/// <summary>
/// One member's answer to one poll.
/// <para>
/// <b>Its own aggregate, and one-member-one-vote is a unique index on it.</b> A check inside a loaded
/// response list loses the race between two devices every time; a unique index on
/// <c>(PollId, VoterUserId)</c> does not, and the second insert fails at the database with a
/// violation the handler turns into the 409 the register asks for.
/// </para>
/// <para>
/// <b>An anonymous poll stores no voter identifier at all.</b> Not a nulled-out column with the value
/// hidden at the mapper — <see cref="VoterUserId"/> is genuinely null, which means there is nothing
/// for a future query, a future export or a future subpoena to find. That is the same structural
/// choice B15 makes for an anonymous donation, and it is what makes the promise real rather than
/// procedural. It also has a cost, stated plainly: an anonymous poll cannot show a voter what they
/// chose and cannot let them change it, because it does not know who they are.
/// </para>
/// <para>
/// <b><see cref="VoterFingerprint"/> is how one-member-one-vote survives anonymity.</b> A keyed hash
/// of the voter and the poll, so the same person answering twice collides on the unique index while
/// the row still carries nothing that identifies them. It cannot be reversed into a member, and it
/// cannot be correlated across polls, because the poll's own identifier is part of what is hashed.
/// </para>
/// </summary>
public sealed class PollResponse : AggregateRoot<PollResponseId>, IAuditableEntity
{
    private readonly List<PollResponseSelection> _selections = [];

    private PollResponse(
        PollResponseId id,
        CommunityPollId pollId,
        UserId? voterUserId,
        string voterFingerprint,
        string? openResponse,
        DateTimeOffset now) : base(id)
    {
        PollId = pollId;
        VoterUserId = voterUserId;
        VoterFingerprint = voterFingerprint;
        OpenResponse = openResponse;
        RespondedAt = now;
        CreatedAt = now;
    }

    private PollResponse() { }

    public CommunityPollId PollId { get; private set; } = default!;

    /// <summary>Null on an anonymous poll, structurally. See the type summary.</summary>
    public UserId? VoterUserId { get; private set; }

    /// <summary>
    /// A keyed, poll-scoped hash of the voter. Uniquely indexed with the poll.
    /// <para>
    /// Present on every response, anonymous or not, so the uniqueness rule is one index rather than
    /// two and cannot be satisfied for one kind of poll and forgotten for the other.
    /// </para>
    /// </summary>
    public string VoterFingerprint { get; private set; } = string.Empty;

    /// <summary>The free-text answer on an open-response poll.</summary>
    public string? OpenResponse { get; private set; }

    public DateTimeOffset RespondedAt { get; private set; }

    /// <summary>B15's idempotency primitive. Uniquely indexed.</summary>
    public string? IdempotencyKey { get; private set; }

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public IReadOnlyList<PollResponseSelection> Selections => _selections.AsReadOnly();

    // ── Auditing ────────────────────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static PollResponse Cast(
        PollResponseId id,
        CommunityPollId pollId,
        UserId? voterUserId,
        string voterFingerprint,
        IEnumerable<PollOptionId> selectedOptions,
        string? openResponse,
        DateTimeOffset now,
        string? idempotencyKey = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(pollId);
        ArgumentNullException.ThrowIfNull(selectedOptions);

        var response = new PollResponse(
            id,
            pollId,
            voterUserId,
            voterFingerprint,
            MembershipProgramme.Clip(openResponse, CommunityProgrammeLimits.MaxOpenResponseLength),
            now)
        {
            IdempotencyKey = MembershipProgramme.Clip(
                idempotencyKey, CommunityProgrammeLimits.MaxIdempotencyKeyLength)
        };

        foreach (var option in selectedOptions.Distinct())
        {
            response._selections.Add(PollResponseSelection.Create(option));
        }

        return response;
    }

    /// <summary>
    /// Replaces the answer, on a poll that allows changes.
    /// <para>
    /// The response keeps its identity and its original <see cref="CreatedAt"/>; what moves is
    /// <see cref="RespondedAt"/>. A changed vote is one vote that changed, not a second vote, and the
    /// total this module reports must not move when somebody changes their mind.
    /// </para>
    /// </summary>
    public void Recast(
        IEnumerable<PollOptionId> selectedOptions, string? openResponse, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(selectedOptions);

        _selections.Clear();

        foreach (var option in selectedOptions.Distinct())
        {
            _selections.Add(PollResponseSelection.Create(option));
        }

        OpenResponse = MembershipProgramme.Clip(
            openResponse, CommunityProgrammeLimits.MaxOpenResponseLength);
        RespondedAt = now;
        Revision++;
        ModifiedAt = now;
    }

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }
}

/// <summary>
/// One option a response chose.
/// <para>
/// An owned entity with a declared key, because an owned collection without one gets a
/// store-generated shadow integer in a composite primary key and SQLite refuses the insert once a
/// row exists. Learned in B10 and paid for again in B11.
/// </para>
/// </summary>
public sealed class PollResponseSelection
{
    private PollResponseSelection(Guid id, PollOptionId optionId)
    {
        Id = id;
        OptionId = optionId;
    }

    private PollResponseSelection() { }

    public Guid Id { get; private set; }

    public PollOptionId OptionId { get; private set; } = default!;

    public static PollResponseSelection Create(PollOptionId optionId) =>
        new(Guid.NewGuid(), optionId);
}
