using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Events;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Domain.Neighborhoods;

/// <summary>
/// One thing a member says about a place they know.
/// <para>
/// <b>"Quiet after ten", "heavy traffic in the mornings", "the buses are reliable", "frequent
/// power cuts".</b> These are the sentences that actually decide where somebody lives, and no
/// editor can write them for a thousand neighbourhoods. The community can — which is the whole
/// argument for this aggregate, and the whole reason it is moderated.
/// </para>
/// <para>
/// <b>Submitted is not published, and nothing renders an unapproved insight.</b> The same
/// mechanism that lets a member warn newcomers about a genuine problem lets one publish a
/// sentence about who lives somewhere. A moderator reads it first. That is not a hedge: an
/// unmoderated free-text field attached to a real residential area is the most predictable
/// liability a housing platform can build.
/// </para>
/// <para>
/// <b>Endorsements are what turn one opinion into a signal.</b> One person saying the water goes
/// off is an anecdote; eleven people saying it is a fact about the neighbourhood, and it is what
/// the profile ranks by. Endorsement is deliberately not a B6 reaction: a reaction is a response
/// to content, and this is a second resident attesting to a claim.
/// </para>
/// <para>
/// <b>A separate aggregate rather than an owned collection on the profile</b>, because a popular
/// neighbourhood attracts hundreds and a moderation queue pages across neighbourhoods rather than
/// within one. Loading Peckham's profile to answer "what is waiting for review" would load every
/// insight ever written about Peckham.
/// </para>
/// </summary>
public sealed class NeighborhoodInsight
    : AggregateRoot<NeighborhoodInsightId>, IAuditableEntity, ISoftDeletable
{
    private NeighborhoodInsight(
        NeighborhoodInsightId id,
        NeighborhoodProfileId profileId,
        Guid areaId,
        UserId authorId,
        NeighborhoodInsightCategory category,
        string body,
        string? label,
        DateTimeOffset createdAt) : base(id)
    {
        ProfileId = profileId;
        AreaId = areaId;
        AuthorId = authorId;
        Category = category;
        Body = body;
        Fingerprint = Fingerprints(body);
        Label = label;
        Status = NeighborhoodInsightStatus.Submitted;
        CreatedAt = createdAt;
    }

    private NeighborhoodInsight() { }

    // ── Subject & author ────────────────────────────────────────────────────────────────────

    public NeighborhoodProfileId ProfileId { get; private set; } = default!;

    /// <summary>
    /// B4's area, denormalised beside the profile identifier. Carried so a query for "everything
    /// said about this place" needs no join through the profile, and so an insight survives a
    /// profile being rebuilt.
    /// </summary>
    public Guid AreaId { get; private set; }

    public UserId AuthorId { get; private set; } = default!;

    public NeighborhoodInsightCategory Category { get; private set; }

    /// <summary>
    /// What the member said. Deliberately short — an observation, not an essay. A member with a
    /// paragraph to write has Community, where it can be discussed.
    /// </summary>
    public string Body { get; private set; } = string.Empty;

    /// <summary>
    /// An optional short chip: "safe for families", "good for students". What a profile renders as
    /// a tag when it has no room for the sentence.
    /// </summary>
    public string? Label { get; private set; }

    // ── State ───────────────────────────────────────────────────────────────────────────────

    public NeighborhoodInsightStatus Status { get; private set; }

    /// <summary>How many other members have attested to it. What the profile ranks by.</summary>
    public int EndorsementCount { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public UserId? DecidedBy { get; private set; }

    /// <summary>
    /// The moderator's reason, which the author can read. A rejection with no reason is one the
    /// author will simply submit again.
    /// </summary>
    public string? DecisionNote { get; private set; }

    // ── Auditing & soft delete ──────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    // ── Derived reads ───────────────────────────────────────────────────────────────────────

    /// <summary>Whether this insight renders anywhere a member can see it.</summary>
    public bool IsPublished =>
        !IsDeleted && Status == NeighborhoodInsightStatus.Approved;

    /// <summary>Whether a moderator has yet looked at it.</summary>
    public bool IsAwaitingReview =>
        !IsDeleted && Status == NeighborhoodInsightStatus.Submitted;

    /// <summary>
    /// The comparable form of the body, for duplicate detection. Lower-cased with runs of
    /// whitespace collapsed, so one member submitting the same observation twice with different
    /// spacing is caught.
    /// <para>
    /// <b>Stored rather than computed on read.</b> A query that normalised in SQL could not use an
    /// index, which would make the duplicate check a table scan on the one path an impatient member
    /// hits repeatedly — resubmitting because their first attempt has not appeared, because it is
    /// waiting for a moderator.
    /// </para>
    /// </summary>
    public string Fingerprint { get; private set; } = string.Empty;

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Creation
    // ════════════════════════════════════════════════════════════════════════════════════════

    public static Result<NeighborhoodInsight> Submit(
        NeighborhoodInsightId id,
        NeighborhoodProfileId profileId,
        Guid areaId,
        UserId authorId,
        NeighborhoodInsightCategory category,
        string? body,
        string? label,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(profileId);
        ArgumentNullException.ThrowIfNull(authorId);

        if (areaId == Guid.Empty)
        {
            return Result.Failure<NeighborhoodInsight>(HousingErrors.NeighborhoodAreaInvalid);
        }

        var trimmed = body?.Trim();

        if (string.IsNullOrWhiteSpace(trimmed)
            || trimmed.Length > HousingLimits.MaxInsightBody)
        {
            return Result.Failure<NeighborhoodInsight>(HousingErrors.InsightBodyInvalid);
        }

        var chip = label?.Trim();

        if (chip is { Length: > HousingLimits.MaxInsightLabel })
        {
            chip = chip[..HousingLimits.MaxInsightLabel];
        }

        var insight = new NeighborhoodInsight(
            id,
            profileId,
            areaId,
            authorId,
            category,
            trimmed,
            string.IsNullOrWhiteSpace(chip) ? null : chip,
            now);

        insight.RaiseDomainEvent(new NeighborhoodInsightSubmittedDomainEvent
        {
            InsightId = id,
            ProfileId = profileId,
            AreaId = areaId,
            AuthorId = authorId,
            Category = category,
            SubmittedAt = now
        });

        return Result.Success(insight);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Moderation
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Publishes the insight on the neighbourhood profile.</summary>
    public Result Approve(UserId moderatorId, string? note, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(moderatorId);

        return Decide(NeighborhoodInsightStatus.Approved, moderatorId, note, now);
    }

    /// <summary>
    /// Refuses it, with a reason the author can read.
    /// <para>
    /// The rejection is deliberately not published outward by the relay — a rejected insight's
    /// existence is between its author and the moderator, and broadcasting it would tell the bus
    /// what a member tried to say about a place.
    /// </para>
    /// </summary>
    public Result Reject(UserId moderatorId, string? note, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(moderatorId);

        return Decide(NeighborhoodInsightStatus.Rejected, moderatorId, note, now);
    }

    /// <summary>
    /// Takes down an insight that was true and has stopped being true.
    /// <para>
    /// A distinct state from rejection, because the two mean different things to the author: one
    /// says "we will not publish this", the other says "this was right and the place has changed".
    /// Retiring rather than deleting also keeps the profile's history honest.
    /// </para>
    /// </summary>
    public Result Retire(UserId moderatorId, string? note, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(moderatorId);

        if (Status != NeighborhoodInsightStatus.Approved)
        {
            return Result.Failure(HousingErrors.InsightNotApproved);
        }

        return Decide(NeighborhoodInsightStatus.Retired, moderatorId, note, now, force: true);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Endorsement
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Records that another member agrees.
    /// <para>
    /// The count is set from the caller's tally rather than incremented, for B1.1's reason: a
    /// consumer that increments on an at-least-once delivery drifts upward forever. The uniqueness
    /// of (insight, endorser) is the repository's job, not this aggregate's.
    /// </para>
    /// </summary>
    public Result Endorse(UserId endorsedBy, int totalEndorsements, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(endorsedBy);

        if (!IsPublished)
        {
            return Result.Failure(HousingErrors.InsightNotApproved);
        }

        if (endorsedBy == AuthorId)
        {
            // Endorsing your own observation adds nothing and inflates the one number the profile
            // ranks by.
            return Result.Success();
        }

        EndorsementCount = Math.Max(0, totalEndorsements);
        ModifiedAt = now;

        RaiseDomainEvent(new NeighborhoodInsightEndorsedDomainEvent
        {
            InsightId = Id,
            ProfileId = ProfileId,
            EndorsedBy = endorsedBy,
            TotalEndorsements = EndorsementCount
        });

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Soft delete & auditing
    // ════════════════════════════════════════════════════════════════════════════════════════

    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
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

    /// <summary>
    /// The comparable form of an insight body. Exposed so the repository's duplicate check
    /// compares like with like rather than raw strings.
    /// </summary>
    public static string Fingerprints(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        return string.Join(
                ' ', body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();
    }

    private Result Decide(
        NeighborhoodInsightStatus status,
        UserId moderatorId,
        string? note,
        DateTimeOffset now,
        bool force = false)
    {
        if (IsDeleted)
        {
            return Result.Failure(HousingErrors.InsightNotFound);
        }

        if (!force && Status != NeighborhoodInsightStatus.Submitted)
        {
            return Result.Failure(HousingErrors.InsightAlreadyDecided);
        }

        var previous = Status;
        var trimmed = note?.Trim();

        if (trimmed is { Length: > HousingLimits.MaxInsightDecisionNote })
        {
            trimmed = trimmed[..HousingLimits.MaxInsightDecisionNote];
        }

        Status = status;
        DecidedAt = now;
        DecidedBy = moderatorId;
        DecisionNote = string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        ModifiedAt = now;

        RaiseDomainEvent(new NeighborhoodInsightDecidedDomainEvent
        {
            InsightId = Id,
            ProfileId = ProfileId,
            AreaId = AreaId,
            AuthorId = AuthorId,
            Category = Category,
            PreviousStatus = previous,
            CurrentStatus = status,
            DecidedBy = moderatorId,
            Reason = DecisionNote,
            DecidedAt = now
        });

        return Result.Success();
    }
}
