using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.Modules.Notifications.Application.Consumers;
using Diaspora.Modules.Notifications.Application.Consumers.Financial;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.Modules.Notifications.Catalog;
using Diaspora.Modules.Notifications.Catalog.Rules;
using Diaspora.Modules.Notifications.Catalog.Templates;
using Diaspora.SharedKernel.Results;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Notifications;

/// <summary>
/// PSR-1.1's Communication ↔ Financial integration.
/// <para>
/// <b>B13 seeded a <c>billing</c> category — "Payments and receipts" — and for two phases nothing
/// routed to it.</b> B15 shipped seventeen published contracts, stated that it "creates no
/// notification, ever", and named the six a communication platform would most obviously act on. Both
/// halves were right and the join between them was never made. These tests pin the join.
/// </para>
/// <para>
/// <b>Every consumer here is built with a dispatcher that does nothing</b>, exactly as B13.1's
/// recipient tests are. If a financial consumer needed anything but the payload to decide who to
/// address, it could not be constructed this way — and the two that legitimately cannot decide from
/// an older payload say so by producing nothing.
/// </para>
/// </summary>
public sealed class FinancialNotificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 9, 0, 0, TimeSpan.Zero);

    private static readonly Guid Member = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Subject = Guid.Parse("22222222-2222-2222-2222-222222222222");

    // ── Anonymity ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>An anonymous donation produces no receipt, and B15 says why: the contract "carries no
    /// identifier to send it to".</b> Honoured here even when a donor identifier is present, because
    /// anonymity is a promise about the donation and not merely an absence of data.
    /// </summary>
    [Fact]
    public void An_anonymous_donation_receipts_nobody()
    {
        var triggers = Translate(
            dispatcher => new DonationCompletedConsumer(dispatcher),
            Donation(donorUserId: Member, isAnonymous: true));

        triggers.Should().BeEmpty();
    }

    /// <summary>A donation with no donor on it cannot be receipted, flag or no flag.</summary>
    [Fact]
    public void A_donation_with_no_donor_receipts_nobody()
    {
        var triggers = Translate(
            dispatcher => new DonationCompletedConsumer(dispatcher),
            Donation(donorUserId: null, isAnonymous: false));

        triggers.Should().BeEmpty();
    }

    /// <summary>A named donor gets a receipt addressed to them, carrying the reference.</summary>
    [Fact]
    public void A_named_donor_gets_a_receipt()
    {
        var triggers = Translate(
            dispatcher => new DonationCompletedConsumer(dispatcher),
            Donation(donorUserId: Member, isAnonymous: false));

        triggers.Should().ContainSingle();
        triggers[0].RecipientUserId.Value.Should().Be(Member);
        triggers[0].Audience.Should().Be("donor");
        triggers[0].Values["receiptReference"].Should().Be("DN-2026-000481");
    }

    /// <summary>Anonymity works identically for a community contribution.</summary>
    [Fact]
    public void An_anonymous_contribution_receipts_nobody()
    {
        var triggers = Translate(
            dispatcher => new CommunityContributionCompletedConsumer(dispatcher),
            Contribution(contributorUserId: Member, isAnonymous: true));

        triggers.Should().BeEmpty();
    }

    // ── The non-monetary receipt ─────────────────────────────────────────

    /// <summary>
    /// <b>Three of B15's five contribution kinds are not money</b>, so this is the one receipt on the
    /// platform whose amount may be a duration. A currency format would have to invent a currency for
    /// the pledges that have none.
    /// </summary>
    [Fact]
    public void A_pledged_contribution_is_receipted_in_the_unit_it_was_pledged_in()
    {
        var triggers = Translate(
            dispatcher => new CommunityContributionCompletedConsumer(dispatcher),
            Contribution(contributorUserId: Member, pledgeQuantity: 12m, pledgeUnit: "hours"));

        triggers.Should().ContainSingle();
        triggers[0].Values["contribution"].Should().Be("12 hours");
    }

    /// <summary>A monetary contribution is receipted as money.</summary>
    [Fact]
    public void A_monetary_contribution_is_receipted_as_money()
    {
        var triggers = Translate(
            dispatcher => new CommunityContributionCompletedConsumer(dispatcher),
            Contribution(contributorUserId: Member, amountMinorUnits: 500_000, currency: "NGN"));

        triggers[0].Values["contribution"].Should().Be("5000.00 NGN");
    }

    /// <summary>
    /// A pledge of a skill has no quantity and no sum. Falling back to the kind is clumsy; a receipt
    /// with a blank where the contribution should be is worse.
    /// </summary>
    [Fact]
    public void A_contribution_with_neither_sum_nor_quantity_still_says_something()
    {
        var triggers = Translate(
            dispatcher => new CommunityContributionCompletedConsumer(dispatcher),
            Contribution(contributorUserId: Member, kind: "Skill"));

        triggers[0].Values["contribution"].Should().Be("Skill");
    }

    // ── The wallet owner type, and why the contract was enriched ─────────

    /// <summary>
    /// <b>The defect this integration surfaced.</b> A wallet is owned by a member, an organization, a
    /// provider, a business or the platform itself, and <c>WalletStatusChangedIntegrationEvent</c>
    /// carried an owner identifier with nothing saying which. A consumer addressing it directly would
    /// have posted "your wallet was frozen" to an organization id — resolving to nobody, silently.
    /// The platform's own revenue wallet would have been the loudest case: operations freeze and thaw
    /// it routinely.
    /// </summary>
    [Theory]
    [InlineData("Organization")]
    [InlineData("Provider")]
    [InlineData("Business")]
    [InlineData("Platform")]
    public void A_wallet_that_belongs_to_nobody_in_particular_tells_nobody(string ownerType)
    {
        var triggers = Translate(
            dispatcher => new WalletStatusChangedConsumer(dispatcher),
            Wallet(ownerType));

        triggers.Should().BeEmpty();
    }

    /// <summary>
    /// <b>An event published before the enrichment carries no owner type and produces nothing.</b>
    /// Every such event is in an outbox table and can be replayed. A receipt nobody receives is
    /// recoverable; a private financial fact delivered to the wrong account is not.
    /// </summary>
    [Fact]
    public void A_wallet_event_from_before_the_enrichment_tells_nobody()
    {
        var triggers = Translate(
            dispatcher => new WalletStatusChangedConsumer(dispatcher),
            Wallet(ownerType: null));

        triggers.Should().BeEmpty();
    }

    /// <summary>A member's own wallet is the one case that produces a message.</summary>
    [Fact]
    public void A_members_own_wallet_tells_the_member()
    {
        var triggers = Translate(
            dispatcher => new WalletStatusChangedConsumer(dispatcher),
            Wallet("Personal"));

        triggers.Should().ContainSingle();
        triggers[0].RecipientUserId.Value.Should().Be(Member);
        triggers[0].Values["status"].Should().Be("Frozen");
        triggers[0].GroupKey.Should().NotBeNull("a retry storm must collapse into one message");
    }

    // ── Renewals ─────────────────────────────────────────────────────────

    /// <summary>
    /// <b>A renewal produces nothing.</b> B15 publishes one contract for the first activation and for
    /// every monthly renewal after it. Telling a member their plan is active on the same day every
    /// month for a year is how the billing category — the category that also carries their payment
    /// failures — gets muted.
    /// </summary>
    [Fact]
    public void A_renewal_is_not_an_announcement()
    {
        var triggers = Translate(
            dispatcher => new SubscriptionActivatedConsumer(dispatcher),
            Subscription(isRenewal: true));

        triggers.Should().BeEmpty();
    }

    /// <summary>A first activation is announced, and so is a trial's.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_first_activation_is_announced(bool isTrial)
    {
        var triggers = Translate(
            dispatcher => new SubscriptionActivatedConsumer(dispatcher),
            Subscription(isRenewal: false, isTrial: isTrial));

        triggers.Should().ContainSingle();
        triggers[0].Values["tier"].Should().Be("Premium");
    }

    // ── Payments ─────────────────────────────────────────────────────────

    /// <summary>
    /// A decline with neither a message nor a code still renders a sentence. A body reading
    /// "Reason: " with nothing after it looks like a broken platform rather than an unexplained
    /// decline.
    /// </summary>
    [Fact]
    public void A_decline_with_no_explanation_still_reads_as_a_sentence()
    {
        var triggers = Translate(
            dispatcher => new PaymentFailedConsumer(dispatcher),
            new PaymentFailedIntegrationEvent
            {
                PaymentIntentId = Subject,
                PayerUserId = Member,
                AmountMinorUnits = 120_000,
                Currency = "NGN",
                IsRetryable = true,
                FailedAt = Now
            });

        triggers[0].Values["reason"].Should().Be("the payment was declined");
        triggers[0].Values["amount"].Should().Be("1200.00 NGN");
    }

    /// <summary>The provider's own words are preferred over its code, and its code over nothing.</summary>
    [Fact]
    public void A_decline_prefers_the_providers_own_words()
    {
        var triggers = Translate(
            dispatcher => new PaymentFailedConsumer(dispatcher),
            new PaymentFailedIntegrationEvent
            {
                PaymentIntentId = Subject,
                PayerUserId = Member,
                AmountMinorUnits = 120_000,
                Currency = "NGN",
                FailureCode = "insufficient_funds",
                FailureMessage = "Insufficient funds",
                IsRetryable = true,
                FailedAt = Now
            });

        triggers[0].Values["reason"].Should().Be("Insufficient funds");
    }

    // ── Placements ───────────────────────────────────────────────────────

    /// <summary>
    /// The purchaser is told, not the subject's owner. B15 allows a placement to be bought over
    /// somebody else's listing and the person who paid is the one entitled to know it started.
    /// </summary>
    [Fact]
    public void A_placement_addresses_whoever_paid_for_it()
    {
        var triggers = Translate(
            dispatcher => new PlacementActivatedConsumer(dispatcher),
            new PlacementActivatedIntegrationEvent
            {
                PlacementId = Subject,
                SubjectType = "MarketplaceListing",
                SubjectId = Guid.NewGuid(),
                Kind = "Boost",
                PurchasedByUserId = Member,
                PriorityWeight = 0.9m,
                ProductCode = "boost-7d",
                StartsAt = Now,
                ActivatedAt = Now
            });

        triggers.Should().ContainSingle();
        triggers[0].RecipientUserId.Value.Should().Be(Member);
        triggers[0].Audience.Should().Be("purchaser");
    }

    /// <summary>
    /// <b>The priority weight is not in the message.</b> It is the number B14's ranking reads; a
    /// member told their boost is worth 0.9 would reasonably ask 0.9 of what, and there is no honest
    /// answer that does not describe the ranking engine.
    /// </summary>
    [Fact]
    public void A_placement_message_does_not_quote_the_ranking_weight()
    {
        var triggers = Translate(
            dispatcher => new PlacementActivatedConsumer(dispatcher),
            new PlacementActivatedIntegrationEvent
            {
                PlacementId = Subject,
                SubjectType = "MarketplaceListing",
                SubjectId = Guid.NewGuid(),
                Kind = "Boost",
                PurchasedByUserId = Member,
                PriorityWeight = 0.9m,
                ProductCode = "boost-7d",
                StartsAt = Now,
                ActivatedAt = Now
            });

        triggers[0].Values.Keys.Should().NotContain(
            key => key.Contains("weight", StringComparison.OrdinalIgnoreCase));
    }

    // ── The catalogue ────────────────────────────────────────────────────

    /// <summary>
    /// <b>Every financial rule names a template that exists.</b> A rule pointing at a template nobody
    /// published logs loudly at dispatch time and sends nothing — a member's payment failure lost to
    /// a typo in a constant. Cheap to assert, and the assertion is the reason the keys are constants.
    /// </summary>
    [Fact]
    public void Every_financial_rule_names_a_template_that_exists()
    {
        var templates = new FinancialNotificationTemplateContributor()
            .GetTemplates().Select(template => template.Key).ToHashSet(StringComparer.Ordinal);

        var referenced = new FinancialNotificationRuleContributor()
            .GetRules().Select(rule => rule.TemplateKey);

        referenced.Should().OnlyContain(key => templates.Contains(key));
    }

    /// <summary>
    /// Every financial rule and template files under <c>billing</c> — the category B13 seeded for
    /// exactly this and which had no traffic until now.
    /// </summary>
    [Fact]
    public void Every_financial_rule_and_template_files_under_billing()
    {
        new FinancialNotificationRuleContributor().GetRules()
            .Should().OnlyContain(rule => rule.CategoryKey == NotificationCategoryKeys.Billing);

        new FinancialNotificationTemplateContributor().GetTemplates()
            .Should().OnlyContain(template => template.CategoryKey == NotificationCategoryKeys.Billing);
    }

    /// <summary>
    /// <b>Every variable a template declares is one some consumer actually supplies.</b> A template
    /// declaring a variable nothing fills renders a message with a hole in it, and the hole appears
    /// on a member's payment receipt rather than in any test that does not check this.
    /// </summary>
    [Fact]
    public void Every_template_variable_is_one_a_consumer_supplies()
    {
        var supplied = SuppliedValueNames();

        var declared = new FinancialNotificationTemplateContributor()
            .GetTemplates()
            .SelectMany(
                template => template.Variables,
                (template, variable) => $"{template.Key}:{variable.Name}");

        declared
            .Where(entry => !supplied.Contains(entry.Split(':')[1], StringComparer.OrdinalIgnoreCase))
            .Should().BeEmpty("a declared variable nothing fills renders a hole in a receipt");
    }

    /// <summary>
    /// <b>Money never deduplicates, with one stated exception.</b> Two failed payments are two facts
    /// about a member's money and the second is not a repeat of the first — unlike six reactions in
    /// an evening, which are one conversation. The wallet freeze is the exception because a retrying
    /// sweep can raise one cause several times.
    /// </summary>
    [Fact]
    public void Only_the_wallet_freeze_deduplicates()
    {
        var deduplicating = new FinancialNotificationRuleContributor()
            .GetRules()
            .Where(rule => rule.DeduplicationWindow is not null)
            .Select(rule => rule.Key)
            .ToList();

        deduplicating.Should().Equal("billing.wallet.status");
    }

    // ── Harness ──────────────────────────────────────────────────────────

    /// <summary>
    /// Every value name the nine financial consumers put on a trigger, gathered by running each of
    /// them over a fully populated event. Derived rather than listed, so a consumer that stops
    /// supplying a value fails the template test above rather than this one silently agreeing.
    /// </summary>
    private static HashSet<string> SuppliedValueNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Collect(IEnumerable<NotificationTrigger> triggers)
        {
            foreach (var trigger in triggers)
            {
                foreach (var key in trigger.Values.Keys)
                {
                    names.Add(key);
                }
            }
        }

        Collect(Translate(
            dispatcher => new PaymentFailedConsumer(dispatcher),
            new PaymentFailedIntegrationEvent
            {
                PaymentIntentId = Subject,
                PayerUserId = Member,
                AmountMinorUnits = 120_000,
                Currency = "NGN",
                FailureMessage = "Insufficient funds",
                IsRetryable = true,
                FailedAt = Now
            }));

        Collect(Translate(
            dispatcher => new RefundIssuedConsumer(dispatcher),
            new RefundIssuedIntegrationEvent
            {
                RefundId = Guid.NewGuid(),
                PaymentIntentId = Subject,
                PayerUserId = Member,
                AmountMinorUnits = 120_000,
                Currency = "NGN",
                IsFullRefund = true,
                Reason = "Order cancelled",
                IssuedAt = Now
            }));

        Collect(Translate(
            dispatcher => new SubscriptionActivatedConsumer(dispatcher),
            Subscription(isRenewal: false)));

        Collect(Translate(
            dispatcher => new SubscriptionEndedConsumer(dispatcher),
            new SubscriptionEndedIntegrationEvent
            {
                SubscriptionId = Subject,
                SubscriberUserId = Member,
                PlanCode = "premium-monthly",
                Tier = "Premium",
                Status = "Cancelled",
                EndedAt = Now
            }));

        Collect(Translate(
            dispatcher => new PlacementActivatedConsumer(dispatcher),
            new PlacementActivatedIntegrationEvent
            {
                PlacementId = Subject,
                SubjectType = "MarketplaceListing",
                SubjectId = Guid.NewGuid(),
                Kind = "Boost",
                PurchasedByUserId = Member,
                PriorityWeight = 0.9m,
                ProductCode = "boost-7d",
                StartsAt = Now,
                ActivatedAt = Now
            }));

        Collect(Translate(
            dispatcher => new PlacementEndedConsumer(dispatcher),
            new PlacementEndedIntegrationEvent
            {
                PlacementId = Subject,
                SubjectType = "MarketplaceListing",
                SubjectId = Guid.NewGuid(),
                Kind = "Boost",
                Status = "Lapsed",
                PurchasedByUserId = Member,
                EndedAt = Now
            }));

        Collect(Translate(
            dispatcher => new DonationCompletedConsumer(dispatcher),
            Donation(donorUserId: Member, isAnonymous: false)));

        Collect(Translate(
            dispatcher => new CommunityContributionCompletedConsumer(dispatcher),
            Contribution(contributorUserId: Member, pledgeQuantity: 12m, pledgeUnit: "hours")));

        Collect(Translate(
            dispatcher => new WalletStatusChangedConsumer(dispatcher), Wallet("Personal")));

        return names;
    }

    private static DonationCompletedIntegrationEvent Donation(
        Guid? donorUserId, bool isAnonymous) =>
        new()
        {
            DonationId = Subject,
            DonorUserId = donorUserId,
            IsAnonymous = isAnonymous,
            BeneficiaryType = "Organization",
            BeneficiaryId = Guid.NewGuid(),
            CauseCode = "lagos-flood-relief",
            AmountMinorUnits = 500_000,
            Currency = "NGN",
            ReceiptReference = "DN-2026-000481",
            CompletedAt = Now
        };

    private static CommunityContributionCompletedIntegrationEvent Contribution(
        Guid? contributorUserId,
        bool isAnonymous = false,
        string kind = "Pledge",
        long? amountMinorUnits = null,
        string? currency = null,
        decimal? pledgeQuantity = null,
        string? pledgeUnit = null) =>
        new()
        {
            ContributionId = Subject,
            FundId = Guid.NewGuid(),
            FundCode = "lagos-flood-relief",
            ContributorUserId = contributorUserId,
            IsAnonymous = isAnonymous,
            Kind = kind,
            AmountMinorUnits = amountMinorUnits,
            Currency = currency,
            PledgeQuantity = pledgeQuantity,
            PledgeUnit = pledgeUnit,
            ReceiptReference = "CN-2026-001204",
            CompletedAt = Now
        };

    private static SubscriptionActivatedIntegrationEvent Subscription(
        bool isRenewal, bool isTrial = false) =>
        new()
        {
            SubscriptionId = Subject,
            SubscriberUserId = Member,
            PlanCode = "premium-monthly",
            Tier = "Premium",
            Status = "Active",
            Interval = "Monthly",
            Entitlements = ["boost", "analytics"],
            IsTrial = isTrial,
            IsRenewal = isRenewal,
            PeriodStartsAt = Now,
            PeriodEndsAt = Now.AddMonths(1),
            ActivatedAt = Now
        };

    private static WalletStatusChangedIntegrationEvent Wallet(string? ownerType) =>
        new()
        {
            WalletId = Subject,
            OwnerId = Member,
            OwnerType = ownerType,
            PreviousStatus = "Active",
            Status = "Frozen",
            Currency = "NGN",
            Reason = "Under review",
            ChangedAt = Now
        };

    /// <summary>
    /// Builds a consumer around a recording dispatcher, runs the event through <c>Handle</c> — the
    /// message-bus entry point — and returns the triggers. Driven through the bus rather than the
    /// translation method so a consumer that produced triggers and failed to dispatch them would
    /// still fail here.
    /// </summary>
    private static IReadOnlyList<NotificationTrigger> Translate<TEvent>(
        Func<INotificationDispatcher, ModuleEventConsumer<TEvent>> build, TEvent integrationEvent)
        where TEvent : Diaspora.Contracts.Events.IIntegrationEvent
    {
        var dispatcher = new RecordingDispatcher();

        build(dispatcher)
            .Handle(
                new IntegrationEventNotification<TEvent>(integrationEvent),
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        return dispatcher.Triggers;
    }

    private sealed class RecordingDispatcher : INotificationDispatcher
    {
        private readonly List<NotificationTrigger> _triggers = [];

        public IReadOnlyList<NotificationTrigger> Triggers => _triggers;

        public Task<Result<NotificationDispatchResult>> DispatchAsync(
            NotificationTrigger trigger, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(trigger);

            _triggers.Add(trigger);

            return Task.FromResult(
                Result.Success(NotificationDispatchResult.Nothing(trigger.RecipientUserId)));
        }
    }
}
