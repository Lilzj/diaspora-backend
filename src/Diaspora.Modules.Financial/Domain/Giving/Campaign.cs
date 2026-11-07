using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Events;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Domain.Giving;

/// <summary>
/// A member-run fundraising programme, and the lifecycle around a <see cref="DonationCause"/>.
/// <para>
/// <b>Campaign is the programme; the cause is the collection target.</b> COMMUNITY-LIFE-GAP-006 asks
/// for twelve states where <c>DonationCause.IsActive</c> is a boolean, and the answer is not to grow
/// twelve states onto the cause. A cause is reference data a donation names by code and B15's giving
/// surfaces have read since B15 shipped; a campaign is somebody's project, with a draft, a review, a
/// pause and an ending. Two responsibilities, two aggregates, one link.
/// </para>
/// <para>
/// <b>The load-bearing rule: publishing creates the cause, in the same unit of work.</b> A published
/// campaign with no <c>causeCode</c> would be a donate button pointing nowhere, and the client's own
/// <c>useDonateToCampaign</c> throws rather than draw one. <see cref="Publish"/> therefore takes the
/// cause it is to be linked to and refuses without it — so there is no ordering in which the status
/// moves first. The handler saves both together or neither.
/// </para>
/// <para>
/// <b>Not one figure on this aggregate is a running total.</b> <c>raised</c>, <c>supporterCount</c>
/// and the milestone that follows from them are the cause's, which is B15's, which is derived from
/// donations. A campaign that kept its own copy would be a second number for the same money and the
/// two would disagree the first time a donation was refunded.
/// </para>
/// <para>
/// <b>Terms acceptance is stored as the moment it happened.</b> A boolean would be a claim the
/// server could not date; <see cref="AcceptedTermsAt"/> is a fact, and it is refused at creation
/// rather than checked in the form.
/// </para>
/// </summary>
public sealed class Campaign : AggregateRoot<CampaignId>, IAuditableEntity
{
    private readonly List<CampaignMilestone> _milestones = [];
    private readonly List<CampaignFundUsageLine> _fundUsage = [];

    private Campaign(
        CampaignId id,
        Guid organizerUserId,
        string organizerName,
        string title,
        string summary,
        string story,
        CampaignCategory category,
        BeneficiaryType beneficiaryType,
        string beneficiaryName,
        string currency,
        int exponent,
        DateTimeOffset acceptedTermsAt,
        DateTimeOffset createdAt) : base(id)
    {
        OrganizerUserId = organizerUserId;
        OrganizerName = organizerName;
        Title = title;
        Summary = summary;
        Story = story;
        Category = category;
        BeneficiaryType = beneficiaryType;
        BeneficiaryName = beneficiaryName;
        Currency = currency;
        Exponent = exponent;
        Status = CampaignStatus.Draft;
        Urgency = ContributionUrgency.Ongoing;
        AcceptedKinds = ContributionKind.Monetary.ToString();
        AcceptedTermsAt = acceptedTermsAt;
        CreatedAt = createdAt;
    }

    private Campaign() { }

    // ── Who runs it ─────────────────────────────────────────────────────────────────────────

    /// <summary>The member who created it. The only one who may drive it, alongside the organization.</summary>
    public Guid OrganizerUserId { get; private set; }

    public string OrganizerName { get; private set; } = string.Empty;

    /// <summary>
    /// B12's organization, when this is an organization's campaign rather than a personal one.
    /// <para>
    /// Null makes it personal, and the two are authorized differently: a personal campaign needs a
    /// verified account, an organization's needs <c>canManageCampaigns</c> inside that body, asked
    /// of whoever owns the organization rather than decided here.
    /// </para>
    /// </summary>
    public Guid? OrganizationId { get; private set; }

    // ── What it is ──────────────────────────────────────────────────────────────────────────

    public string Title { get; private set; } = string.Empty;

    public string Summary { get; private set; } = string.Empty;

    public string Story { get; private set; } = string.Empty;

    public CampaignCategory Category { get; private set; }

    /// <summary>A Media reference, never a URL. B5's rule, honoured by every module since.</summary>
    public string? CoverImageReference { get; private set; }

    /// <summary>Comma-separated Media references. Never URLs.</summary>
    public string Gallery { get; private set; } = string.Empty;

    public BeneficiaryType BeneficiaryType { get; private set; }

    public Guid? BeneficiaryId { get; private set; }

    public string BeneficiaryName { get; private set; } = string.Empty;

    public ContributionUrgency Urgency { get; private set; }

    /// <summary>Comma-separated <c>ContributionKind</c> names, exactly as a cause carries them.</summary>
    public string AcceptedKinds { get; private set; } = string.Empty;

    public string? CountryCode { get; private set; }

    public string? LocationLabel { get; private set; }

    // ── The money side ──────────────────────────────────────────────────────────────────────

    public string Currency { get; private set; } = string.Empty;

    public int Exponent { get; private set; } = Money.DefaultExponent;

    /// <summary>What the organizer is trying to raise. Null where there is no target.</summary>
    public long? TargetMinorUnits { get; private set; }

    /// <summary>When it stops. Null where it runs until it is closed.</summary>
    public DateTimeOffset? Deadline { get; private set; }

    /// <summary>
    /// The cause this campaign collects through, set at publication and never afterwards.
    /// <para>
    /// Null before publication is the whole invariant: a campaign with no cause cannot collect, and
    /// the client refuses to draw a donate button for one.
    /// </para>
    /// </summary>
    public DonationCauseId? CauseId { get; private set; }

    /// <summary>The cause's code, denormalised so a campaign reads without a join.</summary>
    public string? CauseCode { get; private set; }

    // ── Lifecycle ───────────────────────────────────────────────────────────────────────────

    public CampaignStatus Status { get; private set; }

    /// <summary>
    /// Surfaced to the organizer verbatim when the campaign is rejected, or suspended by moderation.
    /// <para>
    /// Only ever set in a state nobody else can see, and cleared on the way out of it — a reviewer's
    /// or a moderator's reason is the organiser's to read, never the public's.
    /// </para>
    /// </summary>
    public string? ReviewNote { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>When the organizer accepted the fundraising terms. Never null — creation refuses without it.</summary>
    public DateTimeOffset AcceptedTermsAt { get; private set; }

    /// <summary>B15's idempotency primitive, copied rather than reinvented. Uniquely indexed.</summary>
    public string? IdempotencyKey { get; private set; }

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public IReadOnlyList<CampaignMilestone> Milestones =>
        _milestones.OrderBy(milestone => milestone.TargetMinorUnits).ToList().AsReadOnly();

    public IReadOnlyList<CampaignFundUsageLine> FundUsage =>
        _fundUsage.OrderBy(line => line.CreatedAt).ToList().AsReadOnly();

    // ── Auditing ────────────────────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    // ── Derived reads ───────────────────────────────────────────────────────────────────────

    public Money? Target => TargetMinorUnits is { } target
        ? Money.FromMinorUnits(target, Currency, Exponent)
        : null;

    public bool IsTerminal => CampaignTransitions.IsTerminal(Status);

    public bool IsEditable => CampaignTransitions.IsEditable(Status);

    public bool IsPubliclyVisible => CampaignTransitions.IsPubliclyVisible(Status);

    /// <summary>
    /// Whether money may be given to this campaign right now.
    /// <para>
    /// <b>Both halves, and the second is the one that matters.</b> A state that accepts donations is
    /// not enough — without a cause there is nowhere certified to collect, and answering true would
    /// put a donate button on a campaign whose donation call would fail.
    /// </para>
    /// </summary>
    public bool AcceptsDonations =>
        CauseCode is not null && CampaignTransitions.AcceptsDonations(Status);

    /// <summary>Whether the caller is the member who created this.</summary>
    public bool IsOrganizedBy(Guid userId) => OrganizerUserId == userId;

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Creation
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Opens a campaign in draft.
    /// <para>
    /// <b>Terms first.</b> The register asks for acceptance to be refused server-side rather than
    /// only in the form, and the cheapest way for that to be true is for the constructor to be
    /// unreachable without it.
    /// </para>
    /// </summary>
    public static Result<Campaign> Open(
        CampaignId id,
        Guid organizerUserId,
        string organizerName,
        string title,
        string summary,
        string story,
        CampaignCategory category,
        BeneficiaryType beneficiaryType,
        string beneficiaryName,
        string currency,
        int exponent,
        bool acceptedTerms,
        DateTimeOffset now,
        Guid? organizationId = null,
        Guid? beneficiaryId = null,
        Money? target = null,
        DateTimeOffset? deadline = null,
        string? countryCode = null,
        string? locationLabel = null,
        string? coverImageReference = null,
        IEnumerable<string>? gallery = null,
        IEnumerable<ContributionKind>? acceptedKinds = null,
        string? idempotencyKey = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (!acceptedTerms)
        {
            return Result.Failure<Campaign>(Error.Validation(
                FinancialErrorCodes.CampaignTermsNotAccepted,
                "The fundraising terms have to be accepted before a campaign can be created."));
        }

        var normalized = Money.NormalizeCurrency(currency);

        if (normalized is null)
        {
            return Result.Failure<Campaign>(Error.Validation(
                FinancialErrorCodes.CurrencyInvalid,
                $"'{currency}' is not an ISO 4217 currency code."));
        }

        if (target is not null && !string.Equals(target.Currency, normalized, StringComparison.Ordinal))
        {
            return Result.Failure<Campaign>(Error.Validation(
                FinancialErrorCodes.CurrencyMismatch,
                $"A {normalized} campaign cannot have a {target.Currency} target."));
        }

        if (deadline is { } ends && ends <= now)
        {
            return Result.Failure<Campaign>(Error.Validation(
                FinancialErrorCodes.CampaignNotEditable,
                "A deadline that has already passed is not a deadline."));
        }

        var campaign = new Campaign(
            id,
            organizerUserId,
            string.IsNullOrWhiteSpace(organizerName) ? "A Diaspora member" : organizerName.Trim(),
            title.Trim(),
            summary.Trim(),
            story.Trim(),
            category,
            beneficiaryType,
            string.IsNullOrWhiteSpace(beneficiaryName) ? "The beneficiary" : beneficiaryName.Trim(),
            normalized,
            exponent,
            now,
            now)
        {
            OrganizationId = organizationId,
            BeneficiaryId = beneficiaryId,
            TargetMinorUnits = target?.MinorUnits,
            Deadline = deadline,
            CountryCode = string.IsNullOrWhiteSpace(countryCode)
                ? null
                : countryCode.Trim().ToUpperInvariant(),
            LocationLabel = string.IsNullOrWhiteSpace(locationLabel) ? null : locationLabel.Trim(),
            CoverImageReference = string.IsNullOrWhiteSpace(coverImageReference)
                ? null
                : coverImageReference.Trim(),
            Gallery = JoinReferences(gallery),
            IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim()
        };

        campaign.SetAcceptedKinds(acceptedKinds);

        return Result.Success(campaign);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Editing
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Changes the campaign's own text and presentation.
    /// <para>
    /// <b>Nothing financial moves through here.</b> The currency is fixed at creation because a
    /// campaign that changed currency after collecting would have raised two different things; the
    /// target may move, because raising the goal of a campaign that is going well is an ordinary
    /// thing to do.
    /// </para>
    /// </summary>
    public Result Revise(
        string? title,
        string? summary,
        string? story,
        CampaignCategory? category,
        Money? target,
        DateTimeOffset? deadline,
        string? coverImageReference,
        IEnumerable<string>? gallery,
        IEnumerable<ContributionKind>? acceptedKinds,
        ContributionUrgency? urgency,
        string? locationLabel,
        DateTimeOffset now)
    {
        if (!IsEditable)
        {
            return Result.Failure(Error.Conflict(
                FinancialErrorCodes.CampaignNotEditable,
                $"A {Status} campaign cannot be edited."));
        }

        if (target is not null && !string.Equals(target.Currency, Currency, StringComparison.Ordinal))
        {
            return Result.Failure(Error.Validation(
                FinancialErrorCodes.CurrencyMismatch,
                $"A {Currency} campaign cannot have a {target.Currency} target."));
        }

        if (deadline is { } ends && ends <= now)
        {
            return Result.Failure(Error.Validation(
                FinancialErrorCodes.CampaignNotEditable,
                "A deadline that has already passed is not a deadline."));
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            Title = title.Trim();
        }

        if (!string.IsNullOrWhiteSpace(summary))
        {
            Summary = summary.Trim();
        }

        if (!string.IsNullOrWhiteSpace(story))
        {
            Story = story.Trim();
        }

        if (category is { } wanted)
        {
            Category = wanted;
        }

        if (target is not null)
        {
            TargetMinorUnits = target.MinorUnits;
        }

        if (deadline is not null)
        {
            Deadline = deadline;
        }

        if (coverImageReference is not null)
        {
            CoverImageReference = string.IsNullOrWhiteSpace(coverImageReference)
                ? null
                : coverImageReference.Trim();
        }

        if (gallery is not null)
        {
            Gallery = JoinReferences(gallery);
        }

        if (acceptedKinds is not null)
        {
            SetAcceptedKinds(acceptedKinds);
        }

        if (urgency is { } level)
        {
            Urgency = level;
        }

        if (locationLabel is not null)
        {
            LocationLabel = string.IsNullOrWhiteSpace(locationLabel) ? null : locationLabel.Trim();
        }

        Touch(now);

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The one lifecycle move, for every transition except publication.
    /// <para>
    /// <b>Publication has its own method because it has a precondition nothing else has</b> — the
    /// cause — and routing it through here would make the invariant a parameter somebody could pass
    /// null for.
    /// </para>
    /// <para>
    /// <b>A move to the state it is already in succeeds and does nothing.</b> That is what makes the
    /// deadline sweep idempotent: a second pass over a campaign it already moved is a no-op rather
    /// than a second event.
    /// </para>
    /// </summary>
    public Result MoveTo(CampaignStatus target, DateTimeOffset now, string? reason = null)
    {
        if (Status == target)
        {
            return Result.Success();
        }

        if (target == CampaignStatus.Published)
        {
            return Result.Failure(Error.Conflict(
                FinancialErrorCodes.CampaignTransitionInvalid,
                "Publishing has to create the giving cause. Use the publish operation."));
        }

        if (!CampaignTransitions.CanTransition(Status, target))
        {
            return Result.Failure(Error.Conflict(
                FinancialErrorCodes.CampaignTransitionInvalid,
                $"A {Status} campaign cannot move to {target}."));
        }

        var previous = Status;

        Status = target;

        if (previous == CampaignStatus.Suspended)
        {
            // Leaving a suspension: the moderator's reason was the organiser's to read while nobody
            // else could see the campaign, and must not follow it back into public view.
            ReviewNote = null;
        }

        switch (target)
        {
            case CampaignStatus.PendingReview:
                SubmittedAt = now;
                ReviewNote = null;
                break;

            case CampaignStatus.Rejected:
            case CampaignStatus.Suspended:
                ReviewNote = Clip(reason);
                break;

            case CampaignStatus.Draft:
                SubmittedAt = null;
                break;

            case CampaignStatus.Completed:
            case CampaignStatus.Cancelled:
            case CampaignStatus.Closed:
                ClosedAt = now;
                break;
        }

        Touch(now);

        RaiseDomainEvent(new CampaignStatusChangedDomainEvent
        {
            CampaignId = Id,
            OrganizerUserId = OrganizerUserId,
            OrganizationId = OrganizationId,
            PreviousStatus = previous.ToString(),
            CurrentStatus = target.ToString(),
            CauseCode = CauseCode,
            Reason = reason,
            ChangedAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// Publishes the campaign against the cause it will collect through.
    /// <para>
    /// <b>The cause is a parameter, not something this method goes and gets.</b> The caller creates
    /// it and saves both in one unit of work, so there is no instant at which the campaign says
    /// published and no cause exists — which is the partially-published state the register forbids.
    /// </para>
    /// </summary>
    public Result Publish(DonationCause cause, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(cause);

        if (Status == CampaignStatus.Published)
        {
            return Result.Success();
        }

        if (!CampaignTransitions.CanTransition(Status, CampaignStatus.Published))
        {
            return Result.Failure(Error.Conflict(
                FinancialErrorCodes.CampaignTransitionInvalid,
                $"A {Status} campaign cannot be published."));
        }

        if (!string.Equals(cause.Currency, Currency, StringComparison.Ordinal))
        {
            return Result.Failure(Error.Validation(
                FinancialErrorCodes.CurrencyMismatch,
                $"A {Currency} campaign cannot collect through a {cause.Currency} cause."));
        }

        var previous = Status;

        CauseId = cause.Id;
        CauseCode = cause.Code;
        Status = CampaignStatus.Published;
        PublishedAt = now;
        ReviewNote = null;
        Touch(now);

        RaiseDomainEvent(new CampaignStatusChangedDomainEvent
        {
            CampaignId = Id,
            OrganizerUserId = OrganizerUserId,
            OrganizationId = OrganizationId,
            PreviousStatus = previous.ToString(),
            CurrentStatus = CampaignStatus.Published.ToString(),
            CauseCode = cause.Code,
            ChangedAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// Takes a live campaign down on a moderator's decision.
    /// <para>
    /// <b>A reason or nothing.</b> The organiser reads it as the campaign's review note, and the
    /// status event carries it, so there is no suspension without a recorded why. The caller closes
    /// the cause in the same unit of work, which is what makes "suspended" mean the money stops.
    /// </para>
    /// </summary>
    public Result Suspend(string reason, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(Error.Validation(
                FinancialErrorCodes.CampaignModerationInvalid,
                "A campaign is suspended with a reason, or not at all."));
        }

        if (Status != CampaignStatus.Suspended
            && !CampaignTransitions.CanTransition(Status, CampaignStatus.Suspended))
        {
            return Result.Failure(Error.Conflict(
                FinancialErrorCodes.CampaignTransitionInvalid,
                $"A {Status} campaign cannot be suspended."));
        }

        return MoveTo(CampaignStatus.Suspended, now, reason);
    }

    /// <summary>
    /// Lifts a suspension.
    /// <para>
    /// <b>Restored, not resumed.</b> The campaign comes back paused — or past its deadline, where
    /// that has gone by while it was down — and it is its organiser who decides whether it collects
    /// again. A moderator turning donations back on would be acting for the organiser, which staff
    /// never do.
    /// </para>
    /// </summary>
    public Result Restore(string reason, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(Error.Validation(
                FinancialErrorCodes.CampaignModerationInvalid,
                "A suspension is lifted with a reason, or not at all."));
        }

        if (Status != CampaignStatus.Suspended)
        {
            return Result.Failure(Error.Conflict(
                FinancialErrorCodes.CampaignTransitionInvalid,
                $"A {Status} campaign is not suspended."));
        }

        return MoveTo(
            IsPastDeadline(now) ? CampaignStatus.DeadlineReached : CampaignStatus.Paused,
            now,
            reason);
    }

    /// <summary>Whether the campaign's deadline has passed as at the given moment.</summary>
    public bool IsPastDeadline(DateTimeOffset asOf) =>
        Deadline is { } ends && ends <= asOf;

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Milestones — GAP-007
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Replaces the declared milestones.
    /// <para>
    /// The organizer declares a label and a figure; whether it has been reached is never theirs and
    /// never the client's. <see cref="EvaluateMilestones"/> is the only thing that answers it, from
    /// the cause's own total.
    /// </para>
    /// </summary>
    public Result SetMilestones(
        IEnumerable<(Guid Id, string Label, long TargetMinorUnits)> milestones,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(milestones);

        var declared = milestones.ToList();

        if (declared.Any(milestone => milestone.TargetMinorUnits <= 0))
        {
            return Result.Failure(Error.Validation(
                FinancialErrorCodes.CampaignMilestoneInvalid,
                "A milestone of nothing is not a milestone."));
        }

        if (declared.Select(milestone => milestone.TargetMinorUnits).Distinct().Count()
            != declared.Count)
        {
            return Result.Failure(Error.Validation(
                FinancialErrorCodes.CampaignMilestoneInvalid,
                "Two milestones cannot sit at the same figure."));
        }

        // Reached-ness survives a re-declaration at the same figure. Somebody renaming a milestone
        // must not un-reach it, because a supporter has already been told it was reached.
        var reached = _milestones.ToDictionary(
            milestone => milestone.TargetMinorUnits, milestone => milestone.ReachedAt);

        _milestones.Clear();

        foreach (var milestone in declared)
        {
            _milestones.Add(CampaignMilestone.Declare(
                milestone.Id,
                milestone.Label,
                milestone.TargetMinorUnits,
                reached.TryGetValue(milestone.TargetMinorUnits, out var when) ? when : null));
        }

        Touch(now);

        return Result.Success();
    }

    /// <summary>
    /// Marks every milestone the cause's total has now passed, and announces each one once.
    /// <para>
    /// <b>Called from three places and written once</b> — when a donation completes, when the
    /// milestones are declared, and from the reconciliation sweep. A milestone declared below a total
    /// already reached is reached immediately, which is why the declaration path calls this too:
    /// otherwise it would sit unreached until the next donation, or for ever if there is not one.
    /// </para>
    /// </summary>
    public IReadOnlyList<CampaignMilestone> EvaluateMilestones(
        long raisedMinorUnits, DateTimeOffset now)
    {
        var newlyReached = new List<CampaignMilestone>();

        foreach (var milestone in _milestones
            .Where(milestone => milestone.ReachedAt is null
                && milestone.TargetMinorUnits <= raisedMinorUnits)
            .OrderBy(milestone => milestone.TargetMinorUnits))
        {
            milestone.MarkReached(now);
            newlyReached.Add(milestone);

            RaiseDomainEvent(new CampaignMilestoneReachedDomainEvent
            {
                CampaignId = Id,
                MilestoneId = milestone.Id,
                Label = milestone.Label,
                TargetMinorUnits = milestone.TargetMinorUnits,
                RaisedMinorUnits = raisedMinorUnits,
                Currency = Currency,
                CauseCode = CauseCode,
                ReachedAt = now
            });
        }

        if (newlyReached.Count > 0)
        {
            Touch(now);
        }

        return newlyReached;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Fund usage — GAP-007
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Replaces the declared fund-usage breakdown.
    /// <para>
    /// <b>An allocation is a declaration and a spend is a record, and the response says which.</b>
    /// The platform can verify a spend only where a settled B15 withdrawal backs it; everything else
    /// is the organizer's own report and is returned marked as one. Presenting the two identically
    /// would be inventing a financial figure, which §17 forbids in as many words.
    /// </para>
    /// </summary>
    public Result SetFundUsage(
        IEnumerable<(Guid Id, string Label, long AllocatedMinorUnits, long? SpentMinorUnits,
            string? Note, Guid? WithdrawalId)> lines,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var declared = lines.ToList();

        if (declared.Any(line => line.AllocatedMinorUnits < 0 || line.SpentMinorUnits < 0))
        {
            return Result.Failure(Error.Validation(
                FinancialErrorCodes.CampaignAllocationInvalid,
                "A negative allocation is not an allocation."));
        }

        var created = _fundUsage.ToDictionary(line => line.Id, line => line.CreatedAt);

        _fundUsage.Clear();

        foreach (var line in declared)
        {
            _fundUsage.Add(CampaignFundUsageLine.Declare(
                line.Id,
                line.Label,
                line.AllocatedMinorUnits,
                line.SpentMinorUnits,
                line.Note,
                line.WithdrawalId,
                created.TryGetValue(line.Id, out var when) ? when : now));
        }

        Touch(now);

        return Result.Success();
    }

    public long AllocatedMinorUnits => _fundUsage.Sum(line => line.AllocatedMinorUnits);

    public long DisbursedMinorUnits => _fundUsage.Sum(line => line.SpentMinorUnits ?? 0);

    // ── Auditing ────────────────────────────────────────────────────────────────────────────

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

    /// <summary>Whether this cause will take a gift of this kind. The cause's own rule, mirrored.</summary>
    public bool Accepts(ContributionKind kind) =>
        AcceptedKinds.Length == 0
        || AcceptedKinds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(kind.ToString(), StringComparer.Ordinal);

    /// <summary>The accepted kinds, parsed. For the response and for the cause the publish creates.</summary>
    public IReadOnlyList<ContributionKind> AcceptedKindList =>
    [
        .. AcceptedKinds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(name => Enum.TryParse<ContributionKind>(name, out var kind)
                ? kind
                : (ContributionKind?)null)
            .Where(kind => kind is not null)
            .Select(kind => kind!.Value)
    ];

    /// <summary>The gallery, parsed. Media references, never URLs.</summary>
    public IReadOnlyList<string> GalleryReferences =>
    [
        .. Gallery.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ];

    private void SetAcceptedKinds(IEnumerable<ContributionKind>? kinds) =>
        AcceptedKinds = string.Join(
            ',',
            (kinds ?? [ContributionKind.Monetary])
                .DefaultIfEmpty(ContributionKind.Monetary)
                .Distinct()
                .Order()
                .Select(kind => kind.ToString()));

    private static string JoinReferences(IEnumerable<string>? references) =>
        references is null
            ? string.Empty
            : string.Join(
                ',',
                references
                    .Where(reference => !string.IsNullOrWhiteSpace(reference))
                    .Select(reference => reference.Trim())
                    .Distinct(StringComparer.Ordinal));

    private static string? Clip(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim()[..Math.Min(value.Trim().Length, FinancialLimits.MaxDescriptionLength)];

    private void Touch(DateTimeOffset now)
    {
        Revision++;
        ModifiedAt = now;
    }
}

/// <summary>
/// A funding figure the organizer declared and the server decides the reaching of.
/// <para>
/// <b><see cref="ReachedAt"/> is a stamp, not a comparison.</b> Deriving reached-ness on every read
/// would make two readers at different moments disagree about when it happened, and there would be
/// nothing to notify on. The server stamps it once, from the cause's authoritative total, and every
/// reader after that agrees.
/// </para>
/// </summary>
public sealed class CampaignMilestone
{
    private CampaignMilestone(Guid id, string label, long targetMinorUnits, DateTimeOffset? reachedAt)
    {
        Id = id;
        Label = label;
        TargetMinorUnits = targetMinorUnits;
        ReachedAt = reachedAt;
    }

    private CampaignMilestone() { }

    public Guid Id { get; private set; }

    public string Label { get; private set; } = string.Empty;

    public long TargetMinorUnits { get; private set; }

    public DateTimeOffset? ReachedAt { get; private set; }

    public bool IsReached => ReachedAt is not null;

    public static CampaignMilestone Declare(
        Guid id, string label, long targetMinorUnits, DateTimeOffset? reachedAt = null) =>
        new(
            id,
            string.IsNullOrWhiteSpace(label) ? "Milestone" : label.Trim(),
            targetMinorUnits,
            reachedAt);

    internal void MarkReached(DateTimeOffset now) => ReachedAt ??= now;
}

/// <summary>
/// One line of what a campaign says it will do with the money, and what it reports it did.
/// <para>
/// <b>Allocation is intent and spend is a report.</b> <see cref="WithdrawalId"/> is present only
/// where a real B15 payout backs the line, and <see cref="IsPlatformVerified"/> is what the response
/// carries so a supporter can tell a figure the platform stands behind from a figure somebody typed.
/// </para>
/// </summary>
public sealed class CampaignFundUsageLine
{
    private CampaignFundUsageLine(
        Guid id,
        string label,
        long allocatedMinorUnits,
        long? spentMinorUnits,
        string? note,
        Guid? withdrawalId,
        DateTimeOffset createdAt)
    {
        Id = id;
        Label = label;
        AllocatedMinorUnits = allocatedMinorUnits;
        SpentMinorUnits = spentMinorUnits;
        Note = note;
        WithdrawalId = withdrawalId;
        CreatedAt = createdAt;
    }

    private CampaignFundUsageLine() { }

    public Guid Id { get; private set; }

    public string Label { get; private set; } = string.Empty;

    public long AllocatedMinorUnits { get; private set; }

    /// <summary>Null where nothing has been spent against this line yet. Never a zero standing in for it.</summary>
    public long? SpentMinorUnits { get; private set; }

    public string? Note { get; private set; }

    /// <summary>The B15 payout that backs this line, when one does.</summary>
    public Guid? WithdrawalId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Whether the platform's own records back the spend, rather than the organizer's word.</summary>
    public bool IsPlatformVerified => WithdrawalId is not null;

    public static CampaignFundUsageLine Declare(
        Guid id,
        string label,
        long allocatedMinorUnits,
        long? spentMinorUnits,
        string? note,
        Guid? withdrawalId,
        DateTimeOffset createdAt) =>
        new(
            id,
            string.IsNullOrWhiteSpace(label) ? "Allocation" : label.Trim(),
            allocatedMinorUnits,
            spentMinorUnits,
            string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            withdrawalId,
            createdAt);
}
