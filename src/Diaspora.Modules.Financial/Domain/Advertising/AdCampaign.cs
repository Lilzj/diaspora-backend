using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Domain.Advertising;

/// <summary>
/// An advertising campaign: what somebody bought, for how much, aimed at whom, and how it is going.
/// <para>
/// <b>Not to be confused with <see cref="Giving.Campaign"/>, which is in this same module.</b> That
/// one is a member-run <em>fundraising</em> programme with a donation cause behind it. This one is
/// an advertiser paying to be seen. Two entities, one word, and the collision is the reason this
/// type is <c>AdCampaign</c> in an <c>Advertising</c> namespace rather than <c>Campaign</c> in a
/// second one — the routes may share a noun, the C# must not.
/// </para>
///
/// <para>
/// <b>This is the layer above <see cref="Placements.Placement"/>, and the register said so exactly:
/// "a campaign is the missing layer above it".</b> A placement knows one subject is boosted until a
/// date. It has no creative, no budget, no spend, no objective and no run state — so a client that
/// bought one and drew a campaign dashboard was drawing an entity the platform did not store. One
/// campaign books many placements; the placements stay exactly as they are.
/// </para>
///
/// <para>
/// <b>Budget is a ceiling, not a payment.</b> Money moves through B15's existing purchase and
/// payment-intent lifecycle, which is built and connected; this aggregate does not move a penny. It
/// records what was authorised, accumulates what was consumed, and refuses to consume past the
/// ceiling — and the difference between the two on a cancellation is what the settlement owes back.
/// </para>
///
/// <para>
/// <b>Spend only moves in <see cref="AdCampaignStatus.Active"/>.</b> A paused campaign that kept
/// charging would be the worst defect this aggregate could carry: the advertiser stopped it, and the
/// only evidence would be a number going up. <see cref="RecordSpend"/> refuses in every other state.
/// </para>
/// </summary>
public sealed class AdCampaign : AggregateRoot<AdCampaignId>, IAuditableEntity
{
    public const int NameMinimumLength = 3;
    public const int NameMaximumLength = 120;
    public const int HeadlineMaximumLength = 120;
    public const int BodyMaximumLength = 500;
    public const int ReasonMaximumLength = 500;

    /// <summary>The placements this campaign booked. Owned; a placement outlives nothing here.</summary>
    private readonly List<AdCampaignPlacement> _placements = [];

    private AdCampaign(
        AdCampaignId id,
        Guid ownerUserId,
        string name,
        AdCampaignObjective objective,
        Money budget,
        DateWindow schedule,
        AdAudience audience)
        : base(id)
    {
        OwnerUserId = ownerUserId;
        Name = name;
        Objective = objective;
        Budget = budget;
        Spend = Money.Zero(budget.Currency, budget.Exponent);
        Schedule = schedule;
        Audience = audience;
        Status = AdCampaignStatus.Draft;
        ReviewDecision = AdReviewDecision.Pending;
    }

    // EF Core.
    private AdCampaign() { }

    public Guid OwnerUserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public AdCampaignObjective Objective { get; private set; }

    public AdCampaignStatus Status { get; private set; }

    /// <summary>What the advertiser authorised. Never moves after submission.</summary>
    public Money Budget { get; private set; } = default!;

    /// <summary>What has been consumed. Only ever moves while Active.</summary>
    public Money Spend { get; private set; } = default!;

    public DateWindow Schedule { get; private set; } = default!;

    public AdAudience Audience { get; private set; } = default!;

    /// <summary>The creative's image, held as a media id. Resolved at the edge, never here.</summary>
    public Guid? CreativeMediaId { get; private set; }

    public string? Headline { get; private set; }

    public string? Body { get; private set; }

    /// <summary>Where a click goes. The subject the campaign promotes.</summary>
    public SubjectReference? Subject { get; private set; }

    /// <summary>The catalogue product bought. Ties the campaign to a price and a policy version.</summary>
    public string? ProductCode { get; private set; }

    /// <summary>B15's purchase, once the campaign is funded.</summary>
    public MonetizationPurchaseId? PurchaseId { get; private set; }

    public IReadOnlyCollection<AdCampaignPlacement> Placements => _placements.AsReadOnly();

    // ── Review ──────────────────────────────────────────────────────────────────────────────

    public AdReviewDecision ReviewDecision { get; private set; }

    /// <summary>Shown to the advertiser verbatim on a rejection. §74.</summary>
    public string? ReviewNote { get; private set; }

    public Guid? ReviewedByUserId { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    /// <summary>Set when the advertiser contests a rejection. One appeal, not a thread.</summary>
    public string? AppealNote { get; private set; }

    public DateTimeOffset? AppealedAt { get; private set; }

    // ── Lifecycle timestamps ────────────────────────────────────────────────────────────────

    public DateTimeOffset? SubmittedAt { get; private set; }

    public DateTimeOffset? ActivatedAt { get; private set; }

    public DateTimeOffset? PausedAt { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }

    public string? EndReason { get; private set; }

    /// <summary>What was owed back on a cancellation. Null until one happens.</summary>
    public Money? SettledAmount { get; private set; }

    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    // ── Derived ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Everything authorised and not yet consumed.</summary>
    public Money RemainingBudget => Budget.Subtract(Spend);

    /// <summary>Whether the advertiser may still change it. Draft only — see <see cref="Update"/>.</summary>
    public bool IsEditable => Status == AdCampaignStatus.Draft;

    /// <summary>Whether it is finished, whichever way it finished.</summary>
    public bool IsTerminal => Status
        is AdCampaignStatus.Completed
        or AdCampaignStatus.Rejected
        or AdCampaignStatus.Cancelled
        or AdCampaignStatus.Failed;

    /// <summary>Whether spend may move. See the type summary.</summary>
    public bool IsDelivering => Status == AdCampaignStatus.Active;

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Creation
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Drafts a campaign. Nothing is charged and nothing is booked.
    /// </summary>
    public static Result<AdCampaign> Draft(
        AdCampaignId id,
        Guid ownerUserId,
        string? name,
        AdCampaignObjective objective,
        Money budget,
        DateWindow schedule,
        AdAudience? audience = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(schedule);

        if (ownerUserId == Guid.Empty)
        {
            return Result.Failure<AdCampaign>(AdCampaignErrors.OwnerRequired);
        }

        var trimmed = name?.Trim();

        if (string.IsNullOrEmpty(trimmed)
            || trimmed.Length is < NameMinimumLength or > NameMaximumLength)
        {
            return Result.Failure<AdCampaign>(AdCampaignErrors.NameLength);
        }

        if (!Enum.IsDefined(objective))
        {
            return Result.Failure<AdCampaign>(AdCampaignErrors.ObjectiveUnknown);
        }

        // A campaign with nothing to spend cannot deliver, and would sit Active for ever showing a
        // zero. Refused at the door rather than discovered on the dashboard.
        if (budget.MinorUnits <= 0)
        {
            return Result.Failure<AdCampaign>(AdCampaignErrors.BudgetRequired);
        }

        // An open-ended advertising campaign has no cost ceiling in time, only in money, and the
        // completion sweep would have nothing to fire on. Fundraising campaigns may run open; these
        // may not, and that is a difference between the two entities rather than an oversight.
        if (schedule.EndsAt is null)
        {
            return Result.Failure<AdCampaign>(AdCampaignErrors.ScheduleRequiresEnd);
        }

        return Result.Success(new AdCampaign(
            id, ownerUserId, trimmed, objective, budget, schedule, audience ?? AdAudience.Everyone()));
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Editing
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Rewrites a draft.
    /// <para>
    /// <b>Draft only, and the refusal is the contract.</b> Once a campaign is funded the budget is a
    /// sum somebody paid and the audience is what a reviewer approved; letting either move
    /// afterwards would mean the thing that ran was not the thing that was bought.
    /// </para>
    /// </summary>
    public Result Update(
        string? name,
        AdCampaignObjective objective,
        Money budget,
        DateWindow schedule,
        AdAudience? audience)
    {
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(schedule);

        if (!IsEditable)
        {
            return Result.Failure(AdCampaignErrors.NotEditable);
        }

        var drafted = Draft(Id, OwnerUserId, name, objective, budget, schedule, audience);

        if (drafted.IsFailure)
        {
            return Result.Failure(drafted.Error);
        }

        Name = drafted.Value.Name;
        Objective = objective;
        Budget = budget;
        Spend = Money.Zero(budget.Currency, budget.Exponent);
        Schedule = schedule;
        Audience = audience ?? AdAudience.Everyone();
        Revision++;

        return Result.Success();
    }

    /// <summary>Sets the creative. Draft only, for the same reason as <see cref="Update"/>.</summary>
    public Result SetCreative(Guid? mediaId, string? headline, string? body)
    {
        if (!IsEditable)
        {
            return Result.Failure(AdCampaignErrors.NotEditable);
        }

        CreativeMediaId = mediaId;
        Headline = Clip(headline, HeadlineMaximumLength);
        Body = Clip(body, BodyMaximumLength);
        Revision++;

        return Result.Success();
    }

    /// <summary>What a click leads to, and which catalogue product priced it.</summary>
    public Result SetSubject(SubjectReference? subject, string? productCode)
    {
        if (!IsEditable)
        {
            return Result.Failure(AdCampaignErrors.NotEditable);
        }

        Subject = subject;

        var code = productCode?.Trim();
        ProductCode = string.IsNullOrEmpty(code) ? null : code;
        Revision++;

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Submission and funding
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Submits for review. The campaign stops being editable here, before the money arrives.
    /// <para>
    /// <b>Two steps rather than one, and the reason is what a single step would hide.</b> A campaign
    /// that went straight from Draft to UnderReview on submission would be under review with no
    /// funds behind it, and a reviewer would be reading creative for a campaign that may never pay.
    /// <see cref="AdCampaignStatus.PendingPayment"/> is the advertiser's move; review starts when
    /// <see cref="MarkFunded"/> says the money landed.
    /// </para>
    /// </summary>
    public Result Submit(DateTimeOffset now)
    {
        if (Status != AdCampaignStatus.Draft)
        {
            return Result.Failure(AdCampaignErrors.NotSubmittable);
        }

        // A campaign whose schedule has already run out cannot be submitted into it.
        if (Schedule.HasEnded(now))
        {
            return Result.Failure(AdCampaignErrors.ScheduleAlreadyOver);
        }

        Status = AdCampaignStatus.PendingPayment;
        SubmittedAt = now;
        Revision++;

        return Result.Success();
    }

    /// <summary>
    /// The money arrived. Queues the campaign for review.
    /// </summary>
    public Result MarkFunded(MonetizationPurchaseId purchaseId)
    {
        ArgumentNullException.ThrowIfNull(purchaseId);

        if (Status != AdCampaignStatus.PendingPayment)
        {
            return Result.Failure(AdCampaignErrors.NotAwaitingPayment);
        }

        PurchaseId = purchaseId;
        Status = AdCampaignStatus.Submitted;
        Revision++;

        return Result.Success();
    }

    /// <summary>
    /// The platform could not carry it — a payment that did not capture, a settlement that errored.
    /// <para>
    /// Deliberately separate from <see cref="Reject"/>. Nobody decided this, and a client that
    /// rendered it as a decision would be telling an advertiser they were refused when they were not.
    /// </para>
    /// </summary>
    public Result Fail(string? reason, DateTimeOffset now)
    {
        if (IsTerminal)
        {
            return Result.Failure(AdCampaignErrors.AlreadyFinished);
        }

        Status = AdCampaignStatus.Failed;
        EndedAt = now;
        EndReason = Clip(reason, ReasonMaximumLength);
        Revision++;

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Review
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>A reviewer picked it up.</summary>
    public Result BeginReview()
    {
        if (Status != AdCampaignStatus.Submitted)
        {
            return Result.Failure(AdCampaignErrors.NotReviewable);
        }

        Status = AdCampaignStatus.UnderReview;
        Revision++;

        return Result.Success();
    }

    /// <summary>
    /// Approved. The campaign waits for its start date rather than going live immediately.
    /// </summary>
    public Result Approve(Guid reviewerUserId, string? note, DateTimeOffset now)
    {
        if (Status is not (AdCampaignStatus.Submitted or AdCampaignStatus.UnderReview))
        {
            return Result.Failure(AdCampaignErrors.NotReviewable);
        }

        ReviewDecision = AdReviewDecision.Approved;
        ReviewedByUserId = reviewerUserId;
        ReviewNote = Clip(note, ReasonMaximumLength);
        ReviewedAt = now;

        Status = AdCampaignStatus.Approved;
        Revision++;

        return Result.Success();
    }

    /// <summary>
    /// Refused, with a reason.
    /// <para>
    /// <b>A reason is required and is not optional.</b> The register asks for the decision, the
    /// reviewer note and the appeal route together, because a rejection without a reason is one the
    /// advertiser cannot act on and cannot meaningfully appeal.
    /// </para>
    /// </summary>
    public Result Reject(Guid reviewerUserId, string? reason, DateTimeOffset now)
    {
        if (Status is not (AdCampaignStatus.Submitted or AdCampaignStatus.UnderReview))
        {
            return Result.Failure(AdCampaignErrors.NotReviewable);
        }

        var note = reason?.Trim();

        if (string.IsNullOrEmpty(note))
        {
            return Result.Failure(AdCampaignErrors.RejectionNeedsReason);
        }

        ReviewDecision = AdReviewDecision.Rejected;
        ReviewedByUserId = reviewerUserId;
        ReviewNote = Clip(note, ReasonMaximumLength);
        ReviewedAt = now;

        Status = AdCampaignStatus.Rejected;
        EndedAt = now;
        Revision++;

        return Result.Success();
    }

    /// <summary>
    /// The advertiser contests a rejection.
    /// <para>
    /// One appeal, recorded on the campaign, not a thread. The status does not move: an appeal is a
    /// request for a second look, and pretending it reopened the campaign would show the advertiser
    /// a running campaign that is not running.
    /// </para>
    /// </summary>
    public Result Appeal(string? note, DateTimeOffset now)
    {
        if (Status != AdCampaignStatus.Rejected)
        {
            return Result.Failure(AdCampaignErrors.NothingToAppeal);
        }

        if (AppealedAt is not null)
        {
            return Result.Failure(AdCampaignErrors.AlreadyAppealed);
        }

        var text = note?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            return Result.Failure(AdCampaignErrors.AppealNeedsReason);
        }

        AppealNote = Clip(text, ReasonMaximumLength);
        AppealedAt = now;
        Revision++;

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Delivery
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Its placements are booked. Approved becomes Scheduled.</summary>
    public Result MarkScheduled()
    {
        if (Status != AdCampaignStatus.Approved)
        {
            return Result.Failure(AdCampaignErrors.NotSchedulable);
        }

        Status = AdCampaignStatus.Scheduled;
        Revision++;

        return Result.Success();
    }

    /// <summary>Goes live. Only from Scheduled, and only once the schedule has started.</summary>
    public Result Activate(DateTimeOffset now)
    {
        if (Status is not (AdCampaignStatus.Scheduled or AdCampaignStatus.Approved))
        {
            return Result.Failure(AdCampaignErrors.NotActivatable);
        }

        if (!Schedule.HasStarted(now))
        {
            return Result.Failure(AdCampaignErrors.ScheduleNotStarted);
        }

        Status = AdCampaignStatus.Active;
        ActivatedAt ??= now;
        PausedAt = null;
        Revision++;

        return Result.Success();
    }

    /// <summary>Stopped, reversibly. Spend freezes here — see the type summary.</summary>
    public Result Pause(DateTimeOffset now)
    {
        if (Status != AdCampaignStatus.Active)
        {
            return Result.Failure(AdCampaignErrors.NotPausable);
        }

        Status = AdCampaignStatus.Paused;
        PausedAt = now;
        Revision++;

        return Result.Success();
    }

    /// <summary>
    /// Restarted after a pause.
    /// <para>
    /// A campaign whose schedule ran out while it was paused completes rather than resuming: the
    /// window it was bought for is gone, and resuming into it would deliver nothing while looking
    /// like it was delivering.
    /// </para>
    /// </summary>
    public Result Resume(DateTimeOffset now)
    {
        if (Status != AdCampaignStatus.Paused)
        {
            return Result.Failure(AdCampaignErrors.NotResumable);
        }

        if (Schedule.HasEnded(now))
        {
            return Complete("The schedule ended while the campaign was paused.", now);
        }

        Status = AdCampaignStatus.Active;
        PausedAt = null;
        Revision++;

        return Result.Success();
    }

    /// <summary>
    /// Consumes budget.
    /// <para>
    /// <b>Active only, and never past the ceiling.</b> A spend that would exceed the budget is
    /// clamped to what remains and completes the campaign — the advertiser authorised a number and
    /// the platform does not get to charge past it, whatever the delivery engine reports.
    /// </para>
    /// </summary>
    public Result RecordSpend(Money amount, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(amount);

        if (!IsDelivering)
        {
            return Result.Failure(AdCampaignErrors.NotDelivering);
        }

        if (!string.Equals(amount.Currency, Budget.Currency, StringComparison.Ordinal))
        {
            return Result.Failure(AdCampaignErrors.CurrencyMismatch);
        }

        if (amount.MinorUnits <= 0)
        {
            return Result.Failure(AdCampaignErrors.SpendMustBePositive);
        }

        var remaining = RemainingBudget;

        // Clamped rather than refused. Refusing loses the delivery that already happened; charging
        // past the ceiling charges money nobody authorised. Clamping keeps both honest.
        var applied = amount.MinorUnits >= remaining.MinorUnits ? remaining : amount;

        Spend = Spend.Add(applied);
        Revision++;

        return Spend.MinorUnits >= Budget.MinorUnits
            ? Complete("The budget was fully spent.", now)
            : Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Endings
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Ran its course — the schedule ended, or the budget did.</summary>
    public Result Complete(string? reason, DateTimeOffset now)
    {
        if (IsTerminal)
        {
            return Result.Failure(AdCampaignErrors.AlreadyFinished);
        }

        Status = AdCampaignStatus.Completed;
        EndedAt = now;
        EndReason = Clip(reason, ReasonMaximumLength);
        PausedAt = null;
        Revision++;

        return Result.Success();
    }

    /// <summary>
    /// Stopped for good by the advertiser, and the unspent budget is recorded as owed.
    /// <para>
    /// <b>Recorded, not refunded.</b> This aggregate does not move money — B15's settlement path
    /// does, reading <see cref="SettledAmount"/>. Writing the figure here and paying it there keeps
    /// the two auditable separately, which is what "settlement of unspent budget" needs to mean if
    /// anybody is ever going to reconcile it.
    /// </para>
    /// </summary>
    public Result Cancel(string? reason, DateTimeOffset now)
    {
        if (IsTerminal)
        {
            return Result.Failure(AdCampaignErrors.AlreadyFinished);
        }

        SettledAmount = RemainingBudget;

        Status = AdCampaignStatus.Cancelled;
        EndedAt = now;
        EndReason = Clip(reason, ReasonMaximumLength);
        PausedAt = null;
        Revision++;

        return Result.Success();
    }

    // ── Placements ──────────────────────────────────────────────────────────────────────────

    /// <summary>Records a placement this campaign booked.</summary>
    public Result AddPlacement(AdCampaignPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);

        if (_placements.Any(existing => existing.PlacementId == placement.PlacementId))
        {
            return Result.Failure(AdCampaignErrors.PlacementAlreadyBooked);
        }

        _placements.Add(placement);
        Revision++;

        return Result.Success();
    }

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

    private static string? Clip(string? value, int maximum)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length <= maximum ? trimmed : trimmed[..maximum];
    }
}
