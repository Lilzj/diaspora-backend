using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Events;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Placements;

/// <summary>
/// A purchased placement over somebody else's subject.
/// <para>
/// <b>This is the aggregate that draws the line B15 exists to draw.</b> Marketplace, Housing and
/// Community Life each have an <c>IsFeatured</c> flag today, set by a moderator, free; reputation
/// grants a third route through <c>LevelBenefits.isFeaturedEligible</c>. Those are <em>display
/// state</em> and stay where they are. A placement is <em>financial state</em> — what was bought,
/// under which policy version, for how long, by whom, for how much — and it belongs here and
/// nowhere else. B15 does not touch a single existing flag and does not turn a moderator's editorial
/// decision into a purchase.
/// </para>
/// <para>
/// <b>The subject is never resolved.</b> A placement over a marketplace listing knows a type and a
/// GUID; it cannot ask whether the listing exists, is published, or belongs to the buyer. That is
/// deliberate — the alternative is a synchronous call into Marketplace on the payment path, which
/// would make a marketplace outage a payment outage. The vertical validates the subject when it
/// consumes the published contract, and a placement nobody claims expires on its own.
/// </para>
/// <para>
/// <b>It expires by sweep, not by read.</b> A placement whose window has closed is only <em>really</em>
/// over once something has said so and published the withdrawal, because a consumer maintaining a
/// featured rail cannot poll every row it has ever been told about. B12's handbook entry applies:
/// when two background operations touch one number the ordering is a design decision, and this one
/// runs before the revenue rollup so a rollup never counts a placement the rail has already dropped.
/// </para>
/// </summary>
public sealed class Placement : AggregateRoot<PlacementId>, IAuditableEntity
{
    private Placement(
        PlacementId id,
        SubjectReference subject,
        PlacementKind kind,
        DateWindow window,
        decimal priorityWeight,
        Guid purchasedByUserId,
        string productCode,
        DateTimeOffset createdAt) : base(id)
    {
        Subject = subject;
        Kind = kind;
        Window = window;
        PriorityWeight = priorityWeight;
        PurchasedByUserId = purchasedByUserId;
        ProductCode = productCode;
        Status = PlacementStatus.Scheduled;
        CreatedAt = createdAt;
    }

    private Placement() { }

    /// <summary>What the placement is over. Never resolved here.</summary>
    public SubjectReference Subject { get; private set; } = default!;

    public PlacementKind Kind { get; private set; }

    public PlacementStatus Status { get; private set; }

    /// <summary>When it runs. The end is exclusive.</summary>
    public DateWindow Window { get; private set; } = default!;

    /// <summary>What a ranking consumer should weight the subject by. 1.0 is no change.</summary>
    public decimal PriorityWeight { get; private set; }

    public Guid PurchasedByUserId { get; private set; }

    public string ProductCode { get; private set; } = string.Empty;

    public MonetizationPurchaseId? PurchaseId { get; private set; }

    /// <summary>The policy version that decided the price and the duration.</summary>
    public string? PolicyVersion { get; private set; }

    public string? CountryCode { get; private set; }

    public DateTimeOffset? ActivatedAt { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }

    public string? EndReason { get; private set; }

    /// <summary>
    /// Whether the buyer has asked for this to renew itself when the window closes.
    /// <para>
    /// A stored preference rather than a subscription: renewal still buys the product again at
    /// whatever it costs then, and still fails if the buyer cannot pay. What this removes is the
    /// member having to be holding their phone at the right moment.
    /// </para>
    /// </summary>
    public bool AutoRenewEnabled { get; private set; }

    /// <summary>When the pause began. Null unless paused.</summary>
    public DateTimeOffset? PausedAt { get; private set; }

    /// <summary>
    /// How long this placement has spent paused, in total.
    /// <para>
    /// Accumulated rather than derived from a single pause, because a member may pause and resume
    /// more than once and each time owes them the days they paid for.
    /// </para>
    /// </summary>
    public TimeSpan PausedDuration { get; private set; }

    /// <summary>
    /// The window end this placement has already announced as approaching.
    /// <para>
    /// Stored so the sweep announces once per window rather than once per sweep. A member warned
    /// every fifteen minutes that their promotion is ending learns to ignore the warning.
    /// </para>
    /// </summary>
    public DateTimeOffset? ExpiryAnnouncedFor { get; private set; }

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static Placement Purchase(
        PlacementId id,
        SubjectReference subject,
        PlacementKind kind,
        DateWindow window,
        decimal priorityWeight,
        Guid purchasedByUserId,
        string productCode,
        DateTimeOffset createdAt,
        MonetizationPurchaseId? purchaseId = null,
        string? policyVersion = null,
        string? countryCode = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(window);
        ArgumentException.ThrowIfNullOrWhiteSpace(productCode);

        return new Placement(
            id, subject, kind, window, Math.Clamp(priorityWeight, 0.1m, 10m),
            purchasedByUserId, productCode.Trim().ToLowerInvariant(), createdAt)
        {
            PurchaseId = purchaseId,
            PolicyVersion = string.IsNullOrWhiteSpace(policyVersion) ? null : policyVersion.Trim(),
            CountryCode = string.IsNullOrWhiteSpace(countryCode)
                ? null
                : countryCode.Trim().ToUpperInvariant()
        };
    }

    /// <summary>
    /// Puts the placement into force and announces it.
    /// <para>
    /// Called when the payment captures, never before. A placement activated on authorisation is a
    /// placement somebody gets for free when the authorisation later fails.
    /// </para>
    /// </summary>
    public void Activate(DateTimeOffset now)
    {
        if (Status == PlacementStatus.Active)
        {
            return;
        }

        if (Status is PlacementStatus.Expired or PlacementStatus.Cancelled or PlacementStatus.Refunded)
        {
            throw new DomainException(
                $"A {Status} placement cannot be activated.",
                FinancialErrorCodes.PlacementNotActive);
        }

        // A paused placement resumes rather than activating. Activating one would skip the
        // paused-duration credit and quietly take back days the member paid for.
        if (Status == PlacementStatus.Paused)
        {
            throw new DomainException(
                "That placement is paused. Resume it rather than activating it.",
                FinancialErrorCodes.PlacementNotActive);
        }

        Status = PlacementStatus.Active;
        ActivatedAt = now;
        Touch(now);

        RaiseDomainEvent(new PlacementActivatedDomainEvent
        {
            PlacementId = Id,
            SubjectType = Subject.SubjectType,
            SubjectId = Subject.SubjectId,
            Kind = Kind,
            PurchasedByUserId = PurchasedByUserId,
            PriorityWeight = PriorityWeight,
            ProductCode = ProductCode,
            CountryCode = CountryCode,
            StartsAt = Window.StartsAt,
            EndsAt = Window.EndsAt,
            ActivatedAt = now
        });
    }

    /// <summary>
    /// Extends the window. What renewing does.
    /// <para>
    /// <b>Extends rather than opening a second placement, and that is the difference the member's
    /// money notices.</b> B15 refuses overlapping placements over one subject, so buying the same
    /// product again while one is running produces either a refusal or a second window that starts
    /// when the first ends. Extending the one that exists is what "renew" was always supposed to
    /// mean.
    /// </para>
    /// <para>
    /// The new end is measured from the current end when the window is still open and from now when
    /// it has already closed, so renewing early does not throw away the days that remain and renewing
    /// late does not silently backdate.
    /// </para>
    /// </summary>
    public void Extend(int additionalDays, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(additionalDays);

        if (Status is PlacementStatus.Cancelled or PlacementStatus.Refunded)
        {
            throw new DomainException(
                $"A {Status} placement cannot be extended.",
                FinancialErrorCodes.PlacementNotActive);
        }

        var from = Window.EndsAt is { } end && end > now ? end : now;

        Window = DateWindow.Create(Window.StartsAt, from.AddDays(additionalDays));

        if (Status == PlacementStatus.Expired)
        {
            Status = PlacementStatus.Active;
        }

        // A window that moved is a window worth warning about again.
        ExpiryAnnouncedFor = null;
        Touch(now);
    }

    /// <summary>
    /// Stops delivery and stops the clock.
    /// <para>
    /// <b>The pause banks the remaining time; the resume hands it back.</b> Cancelling and re-buying
    /// loses whatever was left of a paid window, and offering that as a pause would misdescribe what
    /// the member's money just did — which is exactly why the frontend refused to offer one against
    /// the old contract.
    /// </para>
    /// <para>
    /// Announcing the pause as a <c>PlacementEnded</c> event is deliberate: a ranking consumer
    /// maintaining a featured rail has to drop the subject, and it has no separate vocabulary for
    /// "temporarily". The resume announces an activation, which is the same shape as any other
    /// placement starting.
    /// </para>
    /// </summary>
    public void Pause(DateTimeOffset now)
    {
        if (Status is not (PlacementStatus.Active or PlacementStatus.Scheduled))
        {
            throw new DomainException(
                $"A {Status} placement cannot be paused.",
                FinancialErrorCodes.PlacementNotPausable);
        }

        Status = PlacementStatus.Paused;
        PausedAt = now;
        Touch(now);

        RaiseDomainEvent(new PlacementEndedDomainEvent
        {
            PlacementId = Id,
            SubjectType = Subject.SubjectType,
            SubjectId = Subject.SubjectId,
            Kind = Kind,
            Status = PlacementStatus.Paused,
            PurchasedByUserId = PurchasedByUserId,
            EndedAt = now
        });
    }

    /// <summary>
    /// Starts delivery again and gives back the paused days.
    /// <para>
    /// The window end moves forward by exactly how long the pause lasted. A resume that restarted the
    /// clock would give the member more than they bought; one that ignored the pause would give them
    /// less.
    /// </para>
    /// </summary>
    public void Resume(DateTimeOffset now)
    {
        if (Status != PlacementStatus.Paused)
        {
            throw new DomainException(
                "That placement is not paused.",
                FinancialErrorCodes.PlacementNotPaused);
        }

        var paused = PausedAt is { } from && now > from ? now - from : TimeSpan.Zero;

        PausedDuration += paused;
        PausedAt = null;

        if (Window.EndsAt is { } end)
        {
            Window = DateWindow.Create(Window.StartsAt, end + paused);
        }

        Status = Window.HasStarted(now) ? PlacementStatus.Active : PlacementStatus.Scheduled;
        ExpiryAnnouncedFor = null;
        Touch(now);

        if (Status != PlacementStatus.Active)
        {
            return;
        }

        RaiseDomainEvent(new PlacementActivatedDomainEvent
        {
            PlacementId = Id,
            SubjectType = Subject.SubjectType,
            SubjectId = Subject.SubjectId,
            Kind = Kind,
            PurchasedByUserId = PurchasedByUserId,
            PriorityWeight = PriorityWeight,
            ProductCode = ProductCode,
            CountryCode = CountryCode,
            StartsAt = Window.StartsAt,
            EndsAt = Window.EndsAt,
            ActivatedAt = now
        });
    }

    /// <summary>Records the buyer's renewal preference. Changes nothing about the current window.</summary>
    public void SetAutoRenew(bool enabled, DateTimeOffset now)
    {
        if (AutoRenewEnabled == enabled)
        {
            return;
        }

        AutoRenewEnabled = enabled;
        Touch(now);
    }

    /// <summary>
    /// Records that the approaching end of this window has been announced.
    /// <para>
    /// Returns false when it already had been, so the sweep can announce once per window rather than
    /// once per pass without holding state of its own.
    /// </para>
    /// </summary>
    public bool MarkExpiryAnnounced(DateTimeOffset now)
    {
        if (Window.EndsAt is not { } end || ExpiryAnnouncedFor == end)
        {
            return false;
        }

        ExpiryAnnouncedFor = end;
        Touch(now);

        return true;
    }

    /// <summary>Whether the window closes within a lead time and nothing has said so.</summary>
    public bool IsExpiringWithin(TimeSpan lead, DateTimeOffset moment) =>
        Status is PlacementStatus.Active or PlacementStatus.Scheduled
        && Window.EndsAt is { } end
        && end > moment
        && end - moment <= lead
        && ExpiryAnnouncedFor != end;

    public void Expire(DateTimeOffset now) => End(PlacementStatus.Expired, "Window closed.", now);

    public void Cancel(string reason, DateTimeOffset now) =>
        End(PlacementStatus.Cancelled, reason, now);

    public void MarkRefunded(DateTimeOffset now) =>
        End(PlacementStatus.Refunded, "Refunded.", now);

    /// <summary>Whether the placement is in force at a moment.</summary>
    public bool IsInForce(DateTimeOffset moment) =>
        Status == PlacementStatus.Active && Window.Contains(moment);

    /// <summary>Whether the window has closed and nothing has said so yet.</summary>
    public bool HasLapsed(DateTimeOffset moment) =>
        Status is PlacementStatus.Active or PlacementStatus.Scheduled && Window.HasEnded(moment);

    /// <summary>Whether the window has opened and the placement is still only scheduled.</summary>
    public bool IsDue(DateTimeOffset moment) =>
        Status == PlacementStatus.Scheduled && Window.HasStarted(moment);

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

    private void End(PlacementStatus status, string reason, DateTimeOffset now)
    {
        if (Status is PlacementStatus.Expired or PlacementStatus.Cancelled or PlacementStatus.Refunded)
        {
            return;
        }

        Status = status;
        EndedAt = now;
        EndReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        if (Window.EndsAt is null || Window.EndsAt > now)
        {
            Window = Window.EndingAt(now);
        }

        Touch(now);

        RaiseDomainEvent(new PlacementEndedDomainEvent
        {
            PlacementId = Id,
            SubjectType = Subject.SubjectType,
            SubjectId = Subject.SubjectId,
            Kind = Kind,
            Status = status,
            PurchasedByUserId = PurchasedByUserId,
            EndedAt = now
        });
    }

    private void Touch(DateTimeOffset now)
    {
        Revision++;
        ModifiedAt = now;
    }
}
