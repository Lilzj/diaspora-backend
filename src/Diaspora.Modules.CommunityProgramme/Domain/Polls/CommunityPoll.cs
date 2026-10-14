using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Domain.Enums;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Events;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Memberships;
using Diaspora.Modules.CommunityProgramme.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityProgramme.Domain.Polls;

/// <summary>
/// A question put to a community, and the rules for who may answer it.
/// <para>
/// <b>The poll carries no per-option count, and the absence is the contract.</b> The register is
/// explicit: "a component holding a poll has nothing to add up", which is what makes §24 —
/// authoritative results are the server's — structurally true rather than a convention somebody could
/// break. Counts live on the result read alone, and this aggregate deliberately has no field that
/// would let a mapper leak one.
/// </para>
/// <para>
/// <b>One member, one vote, enforced by a unique index rather than by a check.</b> A
/// <c>PollResponse</c> is its own aggregate keyed uniquely on <c>(pollId, voterUserId)</c>, so two
/// devices voting at the same instant produce one row and one database error, not two rows. A check
/// inside a loaded list would lose that race every time, and the loser would be the tally.
/// </para>
/// <para>
/// <b>Options are owned rows.</b> They are always read with the poll, and an option outside its poll
/// is not a thing. Their identifiers cross the boundary because a vote names one.
/// </para>
/// </summary>
public sealed class CommunityPoll : AggregateRoot<CommunityPollId>, IAuditableEntity, ISoftDeletable
{
    private readonly List<PollOption> _options = [];
    private readonly List<PollInvitee> _invitees = [];

    private CommunityPoll(
        CommunityPollId id,
        UserId createdBy,
        string question,
        PollVotingType votingType,
        PollAudience audience,
        DateTimeOffset now) : base(id)
    {
        CreatedByUserId = createdBy;
        Question = question;
        VotingType = votingType;
        Audience = audience;
        Status = PollStatus.Draft;
        CreatedAt = now;
    }

    private CommunityPoll() { }

    public UserId CreatedByUserId { get; private set; } = default!;

    /// <summary>Set on an organization poll. Null on a neighbourhood, country or platform one.</summary>
    public Guid? OrganizationId { get; private set; }

    public string Question { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public PollStatus Status { get; private set; }

    public PollVotingType VotingType { get; private set; }

    public PollAudience Audience { get; private set; }

    /// <summary>
    /// Legacy: the catalogue area of a neighbourhood poll drafted before 14 Oct 2026. Nothing
    /// writes it any more; <see cref="CityName"/> names the place instead.
    /// </summary>
    public Guid? NeighborhoodAreaId { get; private set; }

    /// <summary>
    /// The town a neighbourhood poll is about, by name (14 Oct 2026) — what replaces a catalogue
    /// area now that the platform is retiring its place catalogue.
    /// </summary>
    public string? CityName { get; private set; }

    /// <summary>The country, on a country poll.</summary>
    public string? CountryCode { get; private set; }

    /// <summary>Max selections on a multiple-choice poll. Null elsewhere.</summary>
    public int? MaxSelections { get; private set; }

    /// <summary>
    /// Whether a response is stored without its voter's identity.
    /// <para>
    /// <b>Structural, not a display flag.</b> An anonymous poll stores no voter identifier on the
    /// response at all — the same shape B15 uses for an anonymous donation — so there is nothing to
    /// leak and nothing to be persuaded to reveal. The cost is real and is accepted: an anonymous
    /// poll cannot tell a voter which option they chose, and cannot let them change it.
    /// </para>
    /// </summary>
    public bool IsAnonymous { get; private set; }

    /// <summary>Whether a voter may change their answer. A server rule, not a client one.</summary>
    public bool AllowsChange { get; private set; }

    /// <summary>Whether the numbers are visible before the poll closes.</summary>
    public bool ShowResultsBeforeClose { get; private set; }

    public DateTimeOffset? OpensAt { get; private set; }

    public DateTimeOffset? ClosesAt { get; private set; }

    /// <summary>
    /// How many responses have been recorded.
    /// <para>
    /// A total, and deliberately not a breakdown. "Forty-one people have answered" is what a card
    /// renders; which forty-one chose what is the result read, and it is a different question with a
    /// different authorization.
    /// </para>
    /// </summary>
    public int TotalResponses { get; private set; }

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public IReadOnlyList<PollOption> Options =>
        _options.OrderBy(option => option.SortOrder).ToList().AsReadOnly();

    public IReadOnlyList<PollInvitee> Invitees => _invitees.AsReadOnly();

    // ── Auditing and deletion ───────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    // ── Derived reads ───────────────────────────────────────────────────────────────────────

    public bool AcceptsVotes => Status == PollStatus.Open;

    public bool IsDiscoverable => Status is PollStatus.Scheduled
        or PollStatus.Open
        or PollStatus.Closed
        or PollStatus.ResultsPublished;

    /// <summary>Whether this module is prepared to show anybody the numbers yet.</summary>
    public bool ResultsAreVisible => Status == PollStatus.ResultsPublished
        || (Status == PollStatus.Open && ShowResultsBeforeClose);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Creation
    // ════════════════════════════════════════════════════════════════════════════════════════

    public static Result<CommunityPoll> Draft(
        CommunityPollId id,
        UserId createdBy,
        string question,
        PollVotingType votingType,
        PollAudience audience,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(createdBy);

        if (string.IsNullOrWhiteSpace(question))
        {
            return Result.Failure<CommunityPoll>(
                CommunityProgrammeErrors.Validation("A poll needs a question."));
        }

        return Result.Success(new CommunityPoll(
            id,
            createdBy,
            MembershipProgramme.Clip(question, CommunityProgrammeLimits.MaxSummaryLength)!,
            votingType,
            audience,
            now));
    }

    public Result Describe(
        string? description,
        Guid? organizationId,
        Guid? neighborhoodAreaId,
        string? countryCode,
        int? maxSelections,
        bool isAnonymous,
        bool allowsChange,
        bool showResultsBeforeClose,
        DateTimeOffset? opensAt,
        DateTimeOffset? closesAt,
        DateTimeOffset now,
        string? cityName = null)
    {
        Description = MembershipProgramme.Clip(
            description, CommunityProgrammeLimits.MaxDescriptionLength);
        OrganizationId = organizationId;
        NeighborhoodAreaId = neighborhoodAreaId;
        CityName = MembershipProgramme.Clip(cityName, CommunityProgrammeLimits.MaxCityNameLength);
        CountryCode = MembershipProgramme.Clip(countryCode, CommunityProgrammeLimits.MaxCountryLength)
            ?.ToUpperInvariant();
        MaxSelections = VotingType == PollVotingType.MultipleChoice
            ? Math.Max(1, maxSelections ?? 1)
            : null;
        IsAnonymous = isAnonymous;

        // An anonymous poll cannot let a voter change their answer, because there is no stored
        // identity to find their previous one by. Silently accepting `allowsChange` on one would be
        // advertising a capability the storage model cannot deliver.
        AllowsChange = allowsChange && !isAnonymous;
        ShowResultsBeforeClose = showResultsBeforeClose;
        OpensAt = opensAt;
        ClosesAt = closesAt;

        if (opensAt is { } opens && closesAt is { } closes && closes <= opens)
        {
            return Result.Failure(CommunityProgrammeErrors.Validation(
                "A poll cannot close before it opens."));
        }

        if (Audience == PollAudience.OrganizationMembers && organizationId is null)
        {
            return Result.Failure(CommunityProgrammeErrors.Validation(
                "A members-only poll must name the organization whose members may vote."));
        }

        // A town and its country since 14 Oct 2026: the neighbourhood is no longer a catalogue
        // node. An area id stored by an older draft still satisfies the rule on a re-describe.
        if (Audience == PollAudience.Neighborhood
            && (string.IsNullOrWhiteSpace(CityName) || string.IsNullOrWhiteSpace(CountryCode))
            && neighborhoodAreaId is null)
        {
            return Result.Failure(CommunityProgrammeErrors.Validation(
                "A neighbourhood poll must name its town and country."));
        }

        if (Audience == PollAudience.Country && string.IsNullOrWhiteSpace(CountryCode))
        {
            return Result.Failure(CommunityProgrammeErrors.Validation(
                "A country poll must name the country."));
        }

        Revision++;
        ModifiedAt = now;

        return Result.Success();
    }

    public Result ReplaceOptions(IReadOnlyList<PollOption> options, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (Status != PollStatus.Draft)
        {
            // Changing the options of a poll people have voted in would silently reassign their
            // votes. The refusal is the only honest answer.
            return Result.Failure(CommunityProgrammeErrors.PollTransitionInvalid);
        }

        if (VotingType != PollVotingType.OpenResponse && options.Count < 2)
        {
            return Result.Failure(CommunityProgrammeErrors.Validation(
                "A poll with choices needs at least two of them."));
        }

        if (options.Count > CommunityProgrammeLimits.MaxPollOptions)
        {
            return Result.Failure(CommunityProgrammeErrors.Validation(
                $"A poll may have at most {CommunityProgrammeLimits.MaxPollOptions} options."));
        }

        _options.Clear();
        _options.AddRange(options);

        Revision++;
        ModifiedAt = now;

        return Result.Success();
    }

    public void ReplaceInvitees(IEnumerable<UserId> invitees, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(invitees);

        _invitees.Clear();

        foreach (var invitee in invitees.Distinct())
        {
            _invitees.Add(PollInvitee.Create(invitee));
        }

        Revision++;
        ModifiedAt = now;
    }

    public bool HasOption(PollOptionId optionId) => _options.Any(option => option.Id == optionId);

    public bool IsInvited(UserId userId) =>
        _invitees.Any(invitee => invitee.UserId == userId);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The one lifecycle move.
    /// <para>
    /// <b>A move to the state it is already in succeeds and does nothing</b>, which is what makes the
    /// sweep that opens scheduled polls and closes expired ones idempotent — it can run twice over
    /// the same poll and produce one event.
    /// </para>
    /// </summary>
    public Result MoveTo(PollStatus target, DateTimeOffset now)
    {
        if (Status == target)
        {
            return Result.Success();
        }

        if (!CanTransition(Status, target))
        {
            return Result.Failure(CommunityProgrammeErrors.PollTransitionInvalid);
        }

        if (target is PollStatus.Open or PollStatus.Scheduled
            && VotingType != PollVotingType.OpenResponse
            && _options.Count < 2)
        {
            return Result.Failure(CommunityProgrammeErrors.Validation(
                "A poll with choices needs at least two of them before it can open."));
        }

        var previous = Status;

        Status = target;
        Revision++;
        ModifiedAt = now;

        if (target == PollStatus.Open)
        {
            OpensAt ??= now;
        }

        if (target is PollStatus.Closed or PollStatus.Cancelled)
        {
            ClosesAt ??= now;
        }

        RaiseDomainEvent(new PollStatusChangedDomainEvent
        {
            PollId = Id,
            OrganizationId = OrganizationId,
            Audience = Audience,
            PreviousStatus = previous,
            CurrentStatus = target,
            ChangedAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// Publishing a draft: straight to Open, or to Scheduled when the opening moment is in the
    /// future. The sweep does the rest.
    /// </summary>
    public Result Publish(DateTimeOffset now) =>
        MoveTo(OpensAt is { } opens && opens > now ? PollStatus.Scheduled : PollStatus.Open, now);

    /// <summary>
    /// Records that a response was added or removed.
    /// <para>
    /// <b>Deliberately does not bump the revision.</b> A vote landing while an organizer closes the
    /// poll must not make the close fail — and the counter is reconciled from a recount anyway, so a
    /// lost increment repairs itself.
    /// </para>
    /// </summary>
    public void RecordResponse(int delta, DateTimeOffset now)
    {
        TotalResponses = Math.Max(0, TotalResponses + delta);
        ModifiedAt = now;
    }

    /// <summary>Sets the total from a recount. Idempotent, and does not bump the revision.</summary>
    public void ReconcileResponses(int total, DateTimeOffset now)
    {
        TotalResponses = Math.Max(0, total);
        ModifiedAt = now;
    }

    private static bool CanTransition(PollStatus from, PollStatus to) => from switch
    {
        PollStatus.Draft => to is PollStatus.Scheduled or PollStatus.Open or PollStatus.Cancelled,
        PollStatus.Scheduled => to is PollStatus.Open or PollStatus.Cancelled,
        PollStatus.Open => to is PollStatus.Closed or PollStatus.Cancelled,
        PollStatus.Closed => to is PollStatus.ResultsPublished,
        _ => false
    };

    // ── Auditing and deletion ───────────────────────────────────────────────────────────────

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
}

/// <summary>
/// One answer a poll offers.
/// <para>
/// <b>No count on it.</b> See the aggregate's summary: a poll object a client holds must have nothing
/// to add up, and that property is only real if the field does not exist.
/// </para>
/// </summary>
public sealed class PollOption
{
    private PollOption(PollOptionId id, string label, string? description, int sortOrder)
    {
        Id = id;
        Label = label;
        Description = description;
        SortOrder = sortOrder;
    }

    private PollOption() { }

    public PollOptionId Id { get; private set; } = default!;

    public string Label { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public int SortOrder { get; private set; }

    public static PollOption Create(
        PollOptionId id, string label, string? description, int sortOrder)
    {
        ArgumentNullException.ThrowIfNull(id);

        return new PollOption(
            id,
            MembershipProgramme.Clip(label, CommunityProgrammeLimits.MaxLabelLength) ?? string.Empty,
            MembershipProgramme.Clip(description, CommunityProgrammeLimits.MaxSummaryLength),
            sortOrder);
    }
}

/// <summary>One person named on an invitation-only poll.</summary>
public sealed class PollInvitee
{
    private PollInvitee(Guid id, UserId userId)
    {
        Id = id;
        UserId = userId;
    }

    private PollInvitee() { }

    public Guid Id { get; private set; }

    public UserId UserId { get; private set; } = default!;

    public static PollInvitee Create(UserId userId) => new(Guid.NewGuid(), userId);
}
