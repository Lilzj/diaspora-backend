using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Events;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Subscriptions;

/// <summary>
/// One subscriber's subscription to one plan.
/// <para>
/// <b>A cancellation does not end access.</b> <see cref="CancelAtPeriodEnd"/> is what "cancel" means
/// for somebody who has already paid for the month: they keep what they bought until the period they
/// paid for runs out. Ending access the moment somebody clicks cancel is taking back something that
/// was paid for, and it is the single most common way a subscription implementation is unfair.
/// </para>
/// <para>
/// <b>A failed renewal enters a grace period rather than a suspension.</b> A card that expired on a
/// Friday should not remove somebody's paid features before anybody could have told them. The grace
/// window is a policy value rather than a constant in a handler, so a deployment can shorten it.
/// </para>
/// <para>
/// <b>The entitlements are copied onto the subscription at activation.</b> They are not read through
/// to the plan, because a plan repriced or re-scoped next month must not silently change what
/// somebody is already paying for. This is the same reasoning that puts a policy version on a ledger
/// entry.
/// </para>
/// </summary>
public sealed class Subscription : AggregateRoot<SubscriptionId>, IAuditableEntity
{
    private readonly List<Entitlement> _entitlements = [];

    private Subscription(
        SubscriptionId id,
        Guid subscriberUserId,
        FinancialAccountId accountId,
        SubscriptionPlanId planId,
        string planCode,
        SubscriptionTier tier,
        BillingInterval interval,
        DateWindow period,
        DateTimeOffset createdAt) : base(id)
    {
        SubscriberUserId = subscriberUserId;
        AccountId = accountId;
        PlanId = planId;
        PlanCode = planCode;
        Tier = tier;
        Interval = interval;
        CurrentPeriod = period;
        Status = SubscriptionStatus.Active;
        CreatedAt = createdAt;
    }

    private Subscription() { }

    public Guid SubscriberUserId { get; private set; }

    public FinancialAccountId AccountId { get; private set; } = default!;

    public SubscriptionPlanId PlanId { get; private set; } = default!;

    /// <summary>Denormalised so a subscription reads without a join.</summary>
    public string PlanCode { get; private set; } = string.Empty;

    public SubscriptionTier Tier { get; private set; }

    public BillingInterval Interval { get; private set; }

    public SubscriptionStatus Status { get; private set; }

    /// <summary>The period paid for. The end is exclusive; null for a lifetime plan.</summary>
    public DateWindow CurrentPeriod { get; private set; } = default!;

    public DateTimeOffset? TrialEndsAt { get; private set; }

    /// <summary>True once somebody has cancelled but the paid period has not ended.</summary>
    public bool CancelAtPeriodEnd { get; private set; }

    /// <summary>
    /// The plan this subscription moves to at the period boundary, when a downgrade is pending.
    /// <para>
    /// Null when nothing is scheduled. The renewal sweep reads it, so a deferred change happens
    /// without anything having to remember to run.
    /// </para>
    /// </summary>
    public string? PendingPlanCode { get; private set; }

    public DateTimeOffset? PendingChangeAt { get; private set; }

    public DateTimeOffset? GraceEndsAt { get; private set; }

    public int RenewalCount { get; private set; }

    public PaymentIntentId? LastPaymentIntentId { get; private set; }

    public string? EndReason { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }

    /// <summary>
    /// The period end this subscription has already announced as approaching.
    /// <para>
    /// Stored so a member is warned once per period rather than once per sweep pass, and so a renewal
    /// that moved the date makes the next period announceable again without any state in the sweep.
    /// </para>
    /// </summary>
    public DateTimeOffset? ExpiryAnnouncedFor { get; private set; }

    /// <summary>What this subscription grants. Copied at activation — see the class summary.</summary>
    public IReadOnlyList<Entitlement> Entitlements => _entitlements.AsReadOnly();

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public IReadOnlyList<string> EntitlementKeys =>
        [.. _entitlements.Select(entitlement => entitlement.Key).Order(StringComparer.Ordinal)];

    /// <summary>Whether the subscriber currently gets what they subscribed to.</summary>
    public bool GrantsAccess =>
        Status is SubscriptionStatus.Trial or SubscriptionStatus.Active or SubscriptionStatus.GracePeriod;

    public static Subscription Start(
        SubscriptionId id,
        Guid subscriberUserId,
        FinancialAccountId accountId,
        SubscriptionPlan plan,
        DateTimeOffset now,
        Func<Guid> entitlementIds,
        bool withTrial = false)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(entitlementIds);

        var trialEndsAt = withTrial && plan.TrialDays > 0 ? now.AddDays(plan.TrialDays) : (DateTimeOffset?)null;
        var periodEnd = trialEndsAt ?? plan.NextPeriodEnd(now);

        var subscription = new Subscription(
            id, subscriberUserId, accountId, plan.Id, plan.Code, plan.Tier, plan.Interval,
            DateWindow.Create(now, periodEnd), now)
        {
            TrialEndsAt = trialEndsAt,
            Status = trialEndsAt is null ? SubscriptionStatus.Active : SubscriptionStatus.Trial
        };

        foreach (var entitlement in plan.Entitlements)
        {
            subscription._entitlements.Add(
                Entitlement.Create(entitlementIds(), entitlement.Key, entitlement.Value));
        }

        subscription.Announce(isRenewal: false, now);

        return subscription;
    }

    /// <summary>
    /// Moves the subscription into a new period.
    /// <para>
    /// Also the path out of a grace period: a renewal that succeeds after a failed one clears the
    /// grace window and restores the subscription to active, which is exactly what the member
    /// expects to happen when they fix their card.
    /// </para>
    /// </summary>
    public void Renew(SubscriptionPlan plan, DateTimeOffset now, Func<Guid> entitlementIds)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(entitlementIds);

        if (Status is SubscriptionStatus.Cancelled or SubscriptionStatus.Expired)
        {
            throw new DomainException(
                $"A {Status} subscription cannot be renewed. Start a new one.",
                FinancialErrorCodes.SubscriptionNotFound);
        }

        var from = CurrentPeriod.EndsAt is { } end && end > now ? end : now;

        CurrentPeriod = DateWindow.Create(from, plan.NextPeriodEnd(from));
        Status = SubscriptionStatus.Active;
        GraceEndsAt = null;
        TrialEndsAt = null;
        RenewalCount++;

        // The subscription adopts the plan it renewed onto. For an ordinary renewal these are the
        // values it already had; for a plan change this is the moment the change takes effect, and
        // leaving the old code here would leave a subscription whose entitlements and whose name
        // disagreed — which is exactly the sort of half-applied change nobody finds until billing.
        PlanId = plan.Id;
        PlanCode = plan.Code;
        Tier = plan.Tier;
        Interval = plan.Interval;
        PendingPlanCode = null;
        PendingChangeAt = null;
        ExpiryAnnouncedFor = null;

        _entitlements.Clear();

        foreach (var entitlement in plan.Entitlements)
        {
            _entitlements.Add(Entitlement.Create(entitlementIds(), entitlement.Key, entitlement.Value));
        }

        Touch(now);
        Announce(isRenewal: true, now);
    }

    /// <summary>A renewal payment failed. Access continues for the grace window.</summary>
    public void EnterGracePeriod(
        DateTimeOffset now, int graceDays = FinancialLimits.SubscriptionGracePeriodDays)
    {
        if (Status is SubscriptionStatus.Cancelled or SubscriptionStatus.Expired)
        {
            return;
        }

        Status = SubscriptionStatus.GracePeriod;
        GraceEndsAt = now.AddDays(Math.Max(1, graceDays));
        Touch(now);
    }

    /// <summary>
    /// Schedules the subscription to stop at the end of the paid period.
    /// <para>
    /// Does not raise the ended event and does not withdraw anything. See the class summary — the
    /// member has paid for this period and keeps it.
    /// </para>
    /// </summary>
    public void CancelAtEndOfPeriod(string? reason, DateTimeOffset now)
    {
        if (Status is SubscriptionStatus.Cancelled or SubscriptionStatus.Expired)
        {
            throw new DomainException(
                $"A {Status} subscription cannot be cancelled.",
                FinancialErrorCodes.SubscriptionNotCancellable);
        }

        CancelAtPeriodEnd = true;
        EndReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        Touch(now);
    }

    /// <summary>
    /// Records a plan change that takes effect at the period boundary.
    /// <para>
    /// <b>A downgrade defers, and this is where that is stored.</b> Somebody who has paid for the
    /// month keeps the month; the renewal sweep reads <see cref="PendingPlanCode"/> and renews onto
    /// the new plan instead of the old one. Applying it now would take back something paid for, which
    /// is the single most common way a subscription implementation is unfair.
    /// </para>
    /// </summary>
    public void ScheduleChange(string planCode, DateTimeOffset effectiveAt, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planCode);

        if (Status is SubscriptionStatus.Cancelled or SubscriptionStatus.Expired)
        {
            throw new DomainException(
                $"A {Status} subscription cannot change plan.",
                FinancialErrorCodes.SubscriptionChangeNotPermitted);
        }

        PendingPlanCode = planCode.Trim().ToLowerInvariant();
        PendingChangeAt = effectiveAt;

        // A scheduled change is a reason to stay subscribed. Leaving a pending cancellation beside it
        // would end the subscription on the same day it was supposed to move plan.
        CancelAtPeriodEnd = false;
        Touch(now);
    }

    /// <summary>Clears a scheduled change, once it has been applied or called off.</summary>
    public void ClearScheduledChange(DateTimeOffset now)
    {
        if (PendingPlanCode is null)
        {
            return;
        }

        PendingPlanCode = null;
        PendingChangeAt = null;
        Touch(now);
    }

    /// <summary>Undoes a scheduled cancellation while the period is still running.</summary>
    public void ResumeAutoRenewal(DateTimeOffset now)
    {
        CancelAtPeriodEnd = false;
        EndReason = null;
        Touch(now);
    }

    /// <summary>Ends the subscription now, withdrawing what it granted.</summary>
    public void End(SubscriptionStatus status, string? reason, DateTimeOffset now)
    {
        if (status is not (SubscriptionStatus.Cancelled or SubscriptionStatus.Expired
            or SubscriptionStatus.Suspended))
        {
            throw new ArgumentException(
                "A subscription only ends as cancelled, expired or suspended.", nameof(status));
        }

        if (Status == status)
        {
            return;
        }

        Status = status;
        EndReason = string.IsNullOrWhiteSpace(reason) ? EndReason : reason.Trim();
        EndedAt = now;
        GraceEndsAt = null;
        Touch(now);

        RaiseDomainEvent(new SubscriptionEndedDomainEvent
        {
            SubscriptionId = Id,
            SubscriberUserId = SubscriberUserId,
            PlanCode = PlanCode,
            Tier = Tier,
            Status = status,
            Reason = EndReason,
            EndedAt = now
        });
    }

    public void AttachPayment(PaymentIntentId paymentIntentId)
    {
        ArgumentNullException.ThrowIfNull(paymentIntentId);

        LastPaymentIntentId = paymentIntentId;
        Revision++;
    }

    /// <summary>Whether the paid period has run out.</summary>
    public bool HasLapsed(DateTimeOffset moment) =>
        GrantsAccess && CurrentPeriod.HasEnded(moment)
        && (GraceEndsAt is null || moment >= GraceEndsAt);

    /// <summary>
    /// Records that the approaching end of this period has been announced.
    /// <para>
    /// Returns false when it already had been, so the sweep announces once per period without
    /// holding state of its own.
    /// </para>
    /// </summary>
    public bool MarkExpiryAnnounced(DateTimeOffset now)
    {
        if (CurrentPeriod.EndsAt is not { } end || ExpiryAnnouncedFor == end)
        {
            return false;
        }

        ExpiryAnnouncedFor = end;
        Touch(now);

        return true;
    }

    /// <summary>Whether the subscription is due to be renewed or ended.</summary>
    public bool IsDueForRenewal(DateTimeOffset moment) =>
        Status is SubscriptionStatus.Active or SubscriptionStatus.Trial
        && CurrentPeriod.HasEnded(moment);

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

    private void Announce(bool isRenewal, DateTimeOffset now) =>
        RaiseDomainEvent(new SubscriptionActivatedDomainEvent
        {
            SubscriptionId = Id,
            SubscriberUserId = SubscriberUserId,
            PlanCode = PlanCode,
            Tier = Tier,
            Status = Status,
            Interval = Interval,
            Entitlements = EntitlementKeys,
            IsTrial = Status == SubscriptionStatus.Trial,
            IsRenewal = isRenewal,
            PeriodStartsAt = CurrentPeriod.StartsAt,
            PeriodEndsAt = CurrentPeriod.EndsAt,
            ActivatedAt = now
        });

    private void Touch(DateTimeOffset now)
    {
        Revision++;
        ModifiedAt = now;
    }
}
