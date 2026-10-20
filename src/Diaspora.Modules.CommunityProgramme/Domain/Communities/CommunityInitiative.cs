using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityProgramme.Domain.Communities;

/// <summary>
/// Where a community initiative is in its life — 21 Oct 2026.
/// <para>
/// Planned and Active take participants; Completed and Cancelled are the two ends, and neither is
/// left again. Stored as its name, as every enum in this module is, and never compared by order.
/// </para>
/// </summary>
public enum CommunityInitiativeStatus
{
    Planned = 1,
    Active = 2,
    Completed = 3,
    Cancelled = 4
}

/// <summary>
/// A project a community runs over time — FRONTEND-PHASE13-GAP-019, 21 Oct 2026.
/// <para>
/// <b>A goal, the people leading it, and nothing priced.</b> Fundraising is a campaign's job, and an
/// initiative carries no amount, target sum or payment link so that it can never become a second way
/// to take money that the campaign rules do not see.
/// </para>
/// <para>
/// <b>Organisers are members of the community, held by user id.</b> Who may name them is decided by
/// <see cref="CommunityCapabilitySet.CanManageInitiatives"/>; whether each still belongs is checked
/// by the handler on every act, because an organiser who leaves the community stops organising
/// without anybody having to edit this row.
/// </para>
/// <para>
/// <b>No progress figure.</b> The app's simulated shape carried a percentage; §29 forbids inventing an
/// authoritative metric, and nothing on the platform measures an initiative. Progress is told in the
/// organisers' own updates (<see cref="CommunityInitiativeUpdate"/>).
/// </para>
/// </summary>
public sealed class CommunityInitiative : AggregateRoot<CommunityInitiativeId>, IAuditableEntity
{
    public const int TitleMaximumLength = 140;
    public const int DescriptionMaximumLength = 4000;
    public const int MaximumOrganisers = 10;

    private readonly List<Guid> _organiserIds = [];

    private CommunityInitiative(CommunityInitiativeId id, CommunityId communityId, UserId proposedBy)
        : base(id)
    {
        CommunityId = communityId;
        ProposedBy = proposedBy;
    }

    // EF Core.
    private CommunityInitiative() { }

    public CommunityId CommunityId { get; private set; } = null!;

    /// <summary>Who created it. Attribution only; authority is the organisers and the capability set.</summary>
    public UserId ProposedBy { get; private set; } = null!;

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    /// <summary>When the community hopes to be done. A date, not a moment, and optional.</summary>
    public DateOnly? TargetDate { get; private set; }

    public CommunityInitiativeStatus Status { get; private set; }

    /// <summary>When it first became Active.</summary>
    public DateTimeOffset? StartedAt { get; private set; }

    /// <summary>When it was completed or cancelled.</summary>
    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>The members leading it. Never empty.</summary>
    public IReadOnlyCollection<Guid> OrganiserIds => _organiserIds.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public bool IsClosed =>
        Status is CommunityInitiativeStatus.Completed or CommunityInitiativeStatus.Cancelled;

    /// <summary>Members may join while it is planned or under way.</summary>
    public bool AcceptsParticipants =>
        Status is CommunityInitiativeStatus.Planned or CommunityInitiativeStatus.Active;

    /// <summary>
    /// Organisers may post until it is cancelled — a completed initiative can still say how it ended.
    /// </summary>
    public bool AcceptsUpdates => Status != CommunityInitiativeStatus.Cancelled;

    public bool IsOrganiser(UserId? member) =>
        member is not null && _organiserIds.Contains(member.Value);

    public static Result<CommunityInitiative> Create(
        CommunityInitiativeId id,
        CommunityId communityId,
        UserId proposedBy,
        string? title,
        string? description,
        DateOnly? targetDate,
        CommunityInitiativeStatus status,
        IReadOnlyCollection<UserId> organisers,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(communityId);
        ArgumentNullException.ThrowIfNull(proposedBy);
        ArgumentNullException.ThrowIfNull(organisers);

        // Begun as planned or already under way; nothing is created finished.
        if (status is not (CommunityInitiativeStatus.Planned or CommunityInitiativeStatus.Active))
        {
            return Result.Failure<CommunityInitiative>(CommunityInitiativeErrors.CreateStatus);
        }

        if (targetDate is { } date && IsBeforeToday(date, now))
        {
            return Result.Failure<CommunityInitiative>(CommunityInitiativeErrors.TargetDateInPast);
        }

        var initiative = new CommunityInitiative(id, communityId, proposedBy)
        {
            Status = status,
            TargetDate = targetDate,
            StartedAt = status == CommunityInitiativeStatus.Active ? now : null
        };

        var described = initiative.Describe(title, description);

        if (described.IsFailure)
        {
            return Result.Failure<CommunityInitiative>(described.Error);
        }

        var organised = initiative.ReplaceOrganisers(organisers);

        return organised.IsFailure
            ? Result.Failure<CommunityInitiative>(organised.Error)
            : Result.Success(initiative);
    }

    /// <summary>
    /// Changes what it says. Null leaves a field as it is; <paramref name="clearTargetDate"/> removes
    /// the date. A closed initiative is a record and is not rewritten.
    /// </summary>
    public Result Revise(
        string? title, string? description, DateOnly? targetDate, bool clearTargetDate, DateTimeOffset now)
    {
        if (IsClosed)
        {
            return Result.Failure(CommunityInitiativeErrors.Closed);
        }

        if (clearTargetDate && targetDate is not null)
        {
            return Result.Failure(CommunityInitiativeErrors.TargetDateConflict);
        }

        // Only a date being changed is held to today: an initiative that ran late may still be
        // corrected without its own old date refusing the edit.
        if (targetDate is { } date && date != TargetDate && IsBeforeToday(date, now))
        {
            return Result.Failure(CommunityInitiativeErrors.TargetDateInPast);
        }

        var described = Describe(title ?? Title, description ?? Description);

        if (described.IsFailure)
        {
            return described;
        }

        if (clearTargetDate)
        {
            TargetDate = null;
        }
        else if (targetDate is not null)
        {
            TargetDate = targetDate;
        }

        return Result.Success();
    }

    /// <summary>
    /// Moves it along: Planned to Active or Cancelled, Active to Completed or Cancelled. Asking for
    /// the status it already has changes nothing and is not refused.
    /// </summary>
    public Result ChangeStatus(CommunityInitiativeStatus target, DateTimeOffset now)
    {
        if (target == Status)
        {
            return Result.Success();
        }

        var allowed = (Status, target) switch
        {
            (CommunityInitiativeStatus.Planned, CommunityInitiativeStatus.Active) => true,
            (CommunityInitiativeStatus.Planned, CommunityInitiativeStatus.Cancelled) => true,
            (CommunityInitiativeStatus.Active, CommunityInitiativeStatus.Completed) => true,
            (CommunityInitiativeStatus.Active, CommunityInitiativeStatus.Cancelled) => true,
            _ => false
        };

        if (!allowed)
        {
            return Result.Failure(CommunityInitiativeErrors.StatusTransition(Status, target));
        }

        Status = target;

        if (target == CommunityInitiativeStatus.Active)
        {
            StartedAt ??= now;
        }
        else
        {
            ClosedAt = now;
        }

        return Result.Success();
    }

    /// <summary>Names who leads it: one to ten distinct members. Membership is the handler's check.</summary>
    public Result ReplaceOrganisers(IReadOnlyCollection<UserId> organisers)
    {
        ArgumentNullException.ThrowIfNull(organisers);

        var distinct = organisers.Select(organiser => organiser.Value).Distinct().ToList();

        if (distinct.Count == 0)
        {
            return Result.Failure(CommunityInitiativeErrors.OrganiserRequired);
        }

        if (distinct.Count > MaximumOrganisers)
        {
            return Result.Failure(CommunityInitiativeErrors.TooManyOrganisers);
        }

        _organiserIds.Clear();
        _organiserIds.AddRange(distinct);

        return Result.Success();
    }

    /// <summary>
    /// Account erasure: takes the member off the initiative without taking the initiative away from
    /// the community — 21 Oct 2026.
    /// <para>
    /// <b>The one place the organiser list may become empty, and deliberately.</b>
    /// <see cref="ReplaceOrganisers"/> refuses an empty list because a person editing an initiative
    /// must name somebody to lead it. Erasure is not an edit and has nobody to ask, and refusing it
    /// would mean keeping a person's identifier because of a rule about forms. An initiative left
    /// with no organiser stays readable exactly as before — its title, description, status and
    /// updates are the community's — and stays manageable by whoever holds
    /// <see cref="CommunityCapabilitySet.CanManageInitiatives"/> (owner, administrators,
    /// ambassadors), who may name new organisers with the ordinary edit.
    /// </para>
    /// <para>
    /// A proposer is attribution only, so it is replaced by <see cref="UserId.Empty"/>, which no
    /// membership and no profile resolves, and which every reader already renders as "nobody named".
    /// </para>
    /// </summary>
    /// <returns>Whether anything changed.</returns>
    public bool ForgetMember(UserId member)
    {
        ArgumentNullException.ThrowIfNull(member);

        var removed = _organiserIds.RemoveAll(id => id == member.Value) > 0;

        if (ProposedBy == member)
        {
            ProposedBy = UserId.Empty;
            removed = true;
        }

        return removed;
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

    private Result Describe(string? title, string? description)
    {
        var trimmedTitle = title?.Trim();
        var trimmedDescription = description?.Trim();

        if (string.IsNullOrEmpty(trimmedTitle))
        {
            return Result.Failure(CommunityInitiativeErrors.TitleRequired);
        }

        if (trimmedTitle.Length > TitleMaximumLength)
        {
            return Result.Failure(CommunityInitiativeErrors.TitleLength);
        }

        if (string.IsNullOrEmpty(trimmedDescription))
        {
            return Result.Failure(CommunityInitiativeErrors.DescriptionRequired);
        }

        if (trimmedDescription.Length > DescriptionMaximumLength)
        {
            return Result.Failure(CommunityInitiativeErrors.DescriptionLength);
        }

        Title = trimmedTitle;
        Description = trimmedDescription;

        return Result.Success();
    }

    /// <summary>
    /// A day of grace: the organiser's "today" may be the server's yesterday anywhere west of UTC,
    /// and refusing today's date there would be refusing the obvious answer.
    /// </summary>
    private static bool IsBeforeToday(DateOnly date, DateTimeOffset now) =>
        date < DateOnly.FromDateTime(now.UtcDateTime).AddDays(-1);
}

/// <summary>
/// One member taking part in an initiative — 21 Oct 2026.
/// <para>
/// Its own row, as a vote is, so "has this member joined" is a unique index rather than a search of
/// a loaded list, and an initiative with a thousand participants is not a thousand-element column.
/// Leaving deletes the row: taking part is a present-tense fact, and nothing reads its history.
/// </para>
/// </summary>
public sealed class CommunityInitiativeParticipant
    : AggregateRoot<CommunityInitiativeParticipantId>, IAuditableEntity
{
    private CommunityInitiativeParticipant(
        CommunityInitiativeParticipantId id,
        CommunityInitiativeId initiativeId,
        CommunityId communityId,
        UserId memberId,
        DateTimeOffset joinedAt)
        : base(id)
    {
        InitiativeId = initiativeId;
        CommunityId = communityId;
        MemberId = memberId;
        JoinedAt = joinedAt;
    }

    // EF Core.
    private CommunityInitiativeParticipant() { }

    public CommunityInitiativeId InitiativeId { get; private set; } = null!;

    /// <summary>Denormalised so a participant can be checked against the community's roll in one join.</summary>
    public CommunityId CommunityId { get; private set; } = null!;

    public UserId MemberId { get; private set; } = null!;

    public DateTimeOffset JoinedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static Result<CommunityInitiativeParticipant> Join(
        CommunityInitiative initiative, UserId memberId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(initiative);
        ArgumentNullException.ThrowIfNull(memberId);

        if (!initiative.AcceptsParticipants)
        {
            return Result.Failure<CommunityInitiativeParticipant>(CommunityInitiativeErrors.NotJoinable);
        }

        return Result.Success(new CommunityInitiativeParticipant(
            CommunityInitiativeParticipantId.New(), initiative.Id, initiative.CommunityId, memberId, now));
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
/// A progress report an organiser posts on an initiative — 21 Oct 2026.
/// <para>
/// The only account of progress the platform holds, and it is the organisers' own words: nothing
/// here is a measurement, and the client should not draw a bar from it.
/// </para>
/// </summary>
public sealed class CommunityInitiativeUpdate : AggregateRoot<CommunityInitiativeUpdateId>, IAuditableEntity
{
    public const int TitleMaximumLength = 140;
    public const int BodyMaximumLength = 4000;

    private CommunityInitiativeUpdate(
        CommunityInitiativeUpdateId id,
        CommunityInitiativeId initiativeId,
        CommunityId communityId,
        UserId authorId,
        DateTimeOffset postedAt)
        : base(id)
    {
        InitiativeId = initiativeId;
        CommunityId = communityId;
        AuthorId = authorId;
        PostedAt = postedAt;
    }

    // EF Core.
    private CommunityInitiativeUpdate() { }

    public CommunityInitiativeId InitiativeId { get; private set; } = null!;

    public CommunityId CommunityId { get; private set; } = null!;

    public UserId AuthorId { get; private set; } = null!;

    public DateTimeOffset PostedAt { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static Result<CommunityInitiativeUpdate> Post(
        CommunityInitiative initiative,
        UserId authorId,
        string? title,
        string? body,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(initiative);
        ArgumentNullException.ThrowIfNull(authorId);

        if (!initiative.AcceptsUpdates)
        {
            return Result.Failure<CommunityInitiativeUpdate>(CommunityInitiativeErrors.UpdatesClosed);
        }

        var trimmedTitle = title?.Trim();
        var trimmedBody = body?.Trim();

        if (string.IsNullOrEmpty(trimmedTitle))
        {
            return Result.Failure<CommunityInitiativeUpdate>(CommunityInitiativeErrors.UpdateTitleRequired);
        }

        if (trimmedTitle.Length > TitleMaximumLength)
        {
            return Result.Failure<CommunityInitiativeUpdate>(CommunityInitiativeErrors.UpdateTitleLength);
        }

        if (string.IsNullOrEmpty(trimmedBody))
        {
            return Result.Failure<CommunityInitiativeUpdate>(CommunityInitiativeErrors.UpdateBodyRequired);
        }

        if (trimmedBody.Length > BodyMaximumLength)
        {
            return Result.Failure<CommunityInitiativeUpdate>(CommunityInitiativeErrors.UpdateBodyLength);
        }

        return Result.Success(new CommunityInitiativeUpdate(
            CommunityInitiativeUpdateId.New(), initiative.Id, initiative.CommunityId, authorId, now)
        {
            Title = trimmedTitle,
            Body = trimmedBody
        });
    }

    /// <summary>
    /// Account erasure: the update is the community's account of its own project and stays; its
    /// author becomes <see cref="UserId.Empty"/>, which every reader renders as nobody named.
    /// </summary>
    public void ForgetAuthor() => AuthorId = UserId.Empty;

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
