using System.Globalization;
using Diaspora.Contracts.Creator.V1.Events;
using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.Modules.Automation.Application.Engine;

namespace Diaspora.Modules.Automation.Application.Consumers;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// FRONTEND-PHASE18-GAP-017 · GAP-018 · GAP-019 — money, and creators.
//
// **Every consumer here reacts to money that has already moved, and none of them can move any.** The
// contracts are read-only facts B15 published; this module has no payment call, no ledger write and
// no wallet read, and there is no action key that could reach one.
//
// **Amounts are carried in minor units, as published.** They are exposed to the `amountAbove`
// condition as a decimal in the currency's major unit so that a member writing "more than 50" means
// fifty pounds rather than fifty pence — the conversion is the same two-decimal assumption B15's own
// contracts make, and it happens here rather than in the evaluator so the evaluator stays a
// comparison.
//
// **GAP-019 needed no scheduler.** The register assumed subscription and placement expiry would be a
// projection this platform had to build; B15 already publishes `SubscriptionExpiring` and
// `PlacementExpiring` with a lead time. Consuming them is the whole of the closure.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>A payment of the member's completed.</summary>
public sealed class PaymentCapturedAutomationConsumer
    : AutomationEventConsumer<PaymentCapturedIntegrationEvent>
{
    public PaymentCapturedAutomationConsumer(AutomationEngine engine) : base(engine) { }

    protected override IEnumerable<AutomationTriggerContext> Translate(
        PaymentCapturedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["amountAbove"] = Major(integrationEvent.AmountMinorUnits);
        values["paymentKind"] = integrationEvent.SubjectType ?? "payment";
        values["country"] = integrationEvent.CountryCode;

        yield return Context(
            "payment.completed",
            integrationEvent.EventId,
            Owners(integrationEvent.PayerUserId),

            // No amount in the summary. The summary lands in an inbox with a longer retention than
            // the payment record, and a member's balance is not something a notification should
            // restate.
            "A payment of yours went through.",
            integrationEvent.OccurredAt,
            "payment",
            integrationEvent.PaymentIntentId,
            null,
            null,
            null,
            values);
    }

    internal static string Major(long minorUnits) =>
        (minorUnits / 100m).ToString(CultureInfo.InvariantCulture);
}

/// <summary>A payment of the member's failed.</summary>
public sealed class PaymentFailedAutomationConsumer
    : AutomationEventConsumer<PaymentFailedIntegrationEvent>
{
    public PaymentFailedAutomationConsumer(AutomationEngine engine) : base(engine) { }

    protected override IEnumerable<AutomationTriggerContext> Translate(
        PaymentFailedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["amountAbove"] = PaymentCapturedAutomationConsumer.Major(
            integrationEvent.AmountMinorUnits);
        values["paymentKind"] = "payment";

        yield return Context(
            "payment.failed",
            integrationEvent.EventId,
            Owners(integrationEvent.PayerUserId),

            // The failure code stays out of the summary: it is a provider's vocabulary and a member
            // reading "card_velocity_exceeded" in their inbox learns nothing they can act on.
            "A payment of yours did not go through.",
            integrationEvent.OccurredAt,
            "payment",
            integrationEvent.PaymentIntentId,
            null,
            null,
            null,
            values);
    }
}

/// <summary>A refund reached the member.</summary>
public sealed class RefundIssuedAutomationConsumer
    : AutomationEventConsumer<RefundIssuedIntegrationEvent>
{
    public RefundIssuedAutomationConsumer(AutomationEngine engine) : base(engine) { }

    protected override IEnumerable<AutomationTriggerContext> Translate(
        RefundIssuedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["amountAbove"] = PaymentCapturedAutomationConsumer.Major(
            integrationEvent.AmountMinorUnits);
        values["paymentKind"] = "refund";

        yield return Context(
            "payment.refunded",
            integrationEvent.EventId,
            Owners(integrationEvent.PayerUserId),
            "A refund reached you.",
            integrationEvent.OccurredAt,
            "refund",
            integrationEvent.RefundId,
            null,
            null,
            null,
            values);
    }
}

/// <summary>A donation the member made completed.</summary>
public sealed class DonationCompletedAutomationConsumer
    : AutomationEventConsumer<DonationCompletedIntegrationEvent>
{
    public DonationCompletedAutomationConsumer(AutomationEngine engine) : base(engine) { }

    protected override IEnumerable<AutomationTriggerContext> Translate(
        DonationCompletedIntegrationEvent integrationEvent)
    {
        // An anonymous donation reaches nobody's automation. The donor asked not to be connected to
        // it, and an automation firing on their own anonymous gift would connect them to it in their
        // own inbox — which is a record somebody else may read over their shoulder.
        if (integrationEvent.IsAnonymous || integrationEvent.DonorUserId is not { } donor)
        {
            yield break;
        }

        var values = Values();
        values["amountAbove"] = PaymentCapturedAutomationConsumer.Major(
            integrationEvent.AmountMinorUnits);
        values["paymentKind"] = "donation";
        values["country"] = integrationEvent.CountryCode;

        yield return Context(
            "giving.donationCompleted",
            integrationEvent.EventId,
            Owners(donor),
            "Your donation went through.",
            integrationEvent.OccurredAt,
            "donation",
            integrationEvent.DonationId,
            null,
            null,
            null,
            values);
    }
}

/// <summary>
/// A subscription is about to end.
/// <para>
/// GAP-019, closed by the module that owns the dates. B15 publishes this with a lead time, so the
/// automation platform needed no projection over renewal dates and holds no copy of them.
/// </para>
/// </summary>
public sealed class SubscriptionExpiringAutomationConsumer
    : AutomationEventConsumer<SubscriptionExpiringIntegrationEvent>
{
    public SubscriptionExpiringAutomationConsumer(AutomationEngine engine) : base(engine) { }

    protected override IEnumerable<AutomationTriggerContext> Translate(
        SubscriptionExpiringIntegrationEvent integrationEvent)
    {
        var values = Values();

        // The entitlement state, as B15 published it. This is the one place `subscriptionActive` can
        // be answered without a read port — and the catalogue still declares the condition
        // unevaluable, because it is not answerable on any other trigger and a condition that worked
        // on one trigger and silently failed on the rest would be worse than one that never works.
        values["subscriptionActive"] = "true";
        values["creatorStatus"] = integrationEvent.Tier;

        yield return Context(
            "subscription.expiring",
            integrationEvent.EventId,
            Owners(integrationEvent.SubscriberUserId),
            integrationEvent.WillRenew
                ? "Your subscription renews soon."
                : "Your subscription ends soon.",
            integrationEvent.OccurredAt,
            "subscription",
            integrationEvent.SubscriptionId,
            null,
            integrationEvent.PeriodEndsAt,
            null,
            values);
    }
}

/// <summary>
/// A featured placement is about to end.
/// <para>
/// One consumer, two trigger keys: the frontend offers <c>placement.ending</c> to every business owner
/// and <c>provider.featuredEnding</c> to a provider, and they are two rules over one published fact.
/// </para>
/// </summary>
public sealed class PlacementExpiringAutomationConsumer
    : AutomationEventConsumer<PlacementExpiringIntegrationEvent>
{
    public PlacementExpiringAutomationConsumer(AutomationEngine engine) : base(engine) { }

    protected override IEnumerable<AutomationTriggerContext> Translate(
        PlacementExpiringIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["category"] = integrationEvent.Kind;
        values["listingStatus"] = "featured";

        var owners = Owners(integrationEvent.PurchasedByUserId, integrationEvent.SubjectId);

        var summary = integrationEvent.AutoRenewEnabled
            ? "A featured placement of yours renews soon."
            : "A featured placement of yours ends soon.";

        yield return Context(
            "placement.ending",
            integrationEvent.EventId,
            owners,
            summary,
            integrationEvent.OccurredAt,
            integrationEvent.SubjectType,
            integrationEvent.SubjectId,
            null,
            integrationEvent.EndsAt,
            null,
            values);

        if (string.Equals(integrationEvent.SubjectType, "provider", StringComparison.OrdinalIgnoreCase))
        {
            yield return Context(
                "provider.featuredEnding",
                integrationEvent.EventId,
                owners,
                summary,
                integrationEvent.OccurredAt,
                integrationEvent.SubjectType,
                integrationEvent.SubjectId,
                null,
                integrationEvent.EndsAt,
                null,
                values);
        }
    }
}

/// <summary>Reward points were granted — the frontend's <c>creator.rewardAvailable</c>.</summary>
public sealed class RewardPointsGrantedAutomationConsumer
    : AutomationEventConsumer<RewardPointsGrantedIntegrationEvent>
{
    public RewardPointsGrantedAutomationConsumer(AutomationEngine engine) : base(engine) { }

    protected override IEnumerable<AutomationTriggerContext> Translate(
        RewardPointsGrantedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["countAbove"] = integrationEvent.Points.ToString(CultureInfo.InvariantCulture);
        values["category"] = integrationEvent.Source;

        yield return Context(
            "creator.rewardAvailable",
            integrationEvent.EventId,
            Owners(integrationEvent.MemberUserId),
            "You have a reward waiting.",
            integrationEvent.OccurredAt,
            "reward",
            null,
            null,
            null,
            null,
            values);
    }
}

/// <summary>A creator reached a milestone.</summary>
public sealed class CreatorMilestoneAutomationConsumer
    : AutomationEventConsumer<CreatorMilestoneCompletedIntegrationEvent>
{
    public CreatorMilestoneAutomationConsumer(AutomationEngine engine) : base(engine) { }

    protected override IEnumerable<AutomationTriggerContext> Translate(
        CreatorMilestoneCompletedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["category"] = integrationEvent.MilestoneKey;

        yield return Context(
            "creator.milestoneReached",
            integrationEvent.EventId,
            Owners(integrationEvent.CreatorId, integrationEvent.MemberUserId),
            "You reached a creator milestone.",
            integrationEvent.OccurredAt,
            "milestone",
            integrationEvent.MilestoneId,
            null,
            null,
            null,
            values);
    }
}

/// <summary>
/// A creator completed a challenge - Phase 5.
/// <para>
/// <b>The contract was on the wire and nothing consumed it.</b> The Backend Gap Build Order counted
/// this among four events the automation triggers were waiting on; the Creator module had been
/// publishing it, once, on the transition to complete. What was missing was this class.
/// </para>
/// <para>
/// Both identities are owners, for the reason the milestone consumer above names both: an
/// automation may be owned by the creator record or by the member behind it, and
/// <c>CreatorAutomationAuthority</c> accepts either. They are the same person - an enrollment is one
/// creator's own participation - so this concerns nobody else.
/// </para>
/// <para>
/// <b>No title, and the summary does not pretend to one.</b> The contract carries the challenge's
/// identifier and not its name; inventing a name here would be a second source of truth for it.
/// </para>
/// </summary>
public sealed class CreatorChallengeAutomationConsumer
    : AutomationEventConsumer<CreatorChallengeCompletedIntegrationEvent>
{
    public CreatorChallengeAutomationConsumer(AutomationEngine engine) : base(engine) { }

    protected override IEnumerable<AutomationTriggerContext> Translate(
        CreatorChallengeCompletedIntegrationEvent integrationEvent)
    {
        yield return Context(
            "creator.challengeCompleted",
            integrationEvent.EventId,
            Owners(integrationEvent.CreatorId, integrationEvent.MemberUserId),
            "You completed a creator challenge.",
            integrationEvent.OccurredAt,
            "challenge",
            integrationEvent.ChallengeId,
            null,
            null,
            null,
            Values());
    }
}

/// <summary>Somebody a creator referred qualified.</summary>
public sealed class CreatorReferralAutomationConsumer
    : AutomationEventConsumer<CreatorReferralQualifiedIntegrationEvent>
{
    public CreatorReferralAutomationConsumer(AutomationEngine engine) : base(engine) { }

    protected override IEnumerable<AutomationTriggerContext> Translate(
        CreatorReferralQualifiedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["countAbove"] = integrationEvent.RewardPoints.ToString(CultureInfo.InvariantCulture);

        yield return Context(
            "creator.referralCompleted",
            integrationEvent.EventId,
            Owners(integrationEvent.ReferrerCreatorId, integrationEvent.ReferrerUserId),

            // The invitee is never named. Somebody joining on a referral is theirs to disclose.
            "Somebody you referred joined.",
            integrationEvent.OccurredAt,
            "referral",
            integrationEvent.ReferralId,
            null,
            null,
            null,
            values);
    }
}
