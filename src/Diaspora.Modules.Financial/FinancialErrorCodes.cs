namespace Diaspora.Modules.Financial;

/// <summary>
/// The error codes this module returns, in one place.
/// <para>
/// Declared once rather than inline because a code is a contract the mobile client switches on, and
/// a code spelled two ways in two handlers is a client branch that silently stops matching.
/// </para>
/// <para>
/// <b>A financial refusal has to say which refusal it was.</b> "Insufficient funds", "wallet
/// frozen", "account restricted" and "currency mismatch" all mean "no" and mean four entirely
/// different things to the person holding the phone — one of them is their problem to fix, one is a
/// support ticket, one is a bug and one is a fraud hold. A single <c>payment.failed</c> would be
/// the cheapest possible contract and the most expensive support queue.
/// </para>
/// </summary>
public static class FinancialErrorCodes
{
    // ── Authorisation ────────────────────────────────────────────────────────────────────────

    public const string NotAuthenticated = "financial.not_authenticated";
    public const string Forbidden = "financial.forbidden";

    // ── Financial identity ───────────────────────────────────────────────────────────────────

    public const string AccountNotFound = "financial.account.not_found";
    public const string AccountExists = "financial.account.already_exists";
    public const string AccountRestricted = "financial.account.restricted";
    public const string AccountClosed = "financial.account.closed";
    public const string AccountImmutable = "financial.account.immutable";
    public const string AccountNotEligible = "financial.account.not_eligible";

    // ── Wallets ──────────────────────────────────────────────────────────────────────────────

    public const string WalletNotFound = "financial.wallet.not_found";
    public const string WalletExists = "financial.wallet.already_exists";
    public const string WalletFrozen = "financial.wallet.frozen";
    public const string WalletClosed = "financial.wallet.closed";
    public const string WalletHasHeldFunds = "financial.wallet.has_held_funds";
    public const string InsufficientFunds = "financial.wallet.insufficient_funds";

    // ── Money ────────────────────────────────────────────────────────────────────────────────

    public const string AmountInvalid = "financial.amount.invalid";
    public const string CurrencyInvalid = "financial.currency.invalid";
    public const string CurrencyMismatch = "financial.currency.mismatch";
    public const string CurrencyNotSupported = "financial.currency.not_supported";

    // ── Ledger and transactions ──────────────────────────────────────────────────────────────

    public const string LedgerUnbalanced = "financial.ledger.unbalanced";
    public const string LedgerEmptyPosting = "financial.ledger.empty_posting";
    public const string TransactionNotFound = "financial.transaction.not_found";
    public const string TransactionNotPending = "financial.transaction.not_pending";
    public const string TransactionNotReversible = "financial.transaction.not_reversible";
    public const string TransactionDuplicate = "financial.transaction.duplicate";

    // ── Payments ─────────────────────────────────────────────────────────────────────────────

    public const string PaymentIntentNotFound = "financial.payment.not_found";
    public const string PaymentIntentExpired = "financial.payment.expired";
    public const string PaymentTransitionInvalid = "financial.payment.transition_invalid";
    public const string PaymentAuthorizationFailed = "financial.payment.authorization_failed";
    public const string PaymentCaptureFailed = "financial.payment.capture_failed";
    public const string PaymentGatewayUnavailable = "financial.payment.gateway_unavailable";
    public const string PaymentGatewayUnknown = "financial.payment.gateway_unknown";

    /// <summary>The method is real, and wrong for what is being paid for.</summary>
    public const string PaymentMethodNotSupported = "financial.payment.method_not_supported";
    public const string PaymentMethodUnsupported = "financial.payment.method_unsupported";
    public const string WebhookSignatureInvalid = "financial.webhook.signature_invalid";
    public const string WebhookUnrecognized = "financial.webhook.unrecognized";
    public const string WebhookProviderUnknown = "financial.webhook.provider_unknown";
    public const string WebhookPayloadTooLarge = "financial.webhook.payload_too_large";

    // ── Deposits ─────────────────────────────────────────────────────────────────────────────

    public const string DepositNotFound = "financial.deposit.not_found";
    public const string DepositBelowMinimum = "financial.deposit.below_minimum";
    public const string DepositAboveMaximum = "financial.deposit.above_maximum";
    public const string DepositDailyLimitReached = "financial.deposit.daily_limit_reached";
    public const string DepositNotSupported = "financial.deposit.not_supported";

    // ── Withdrawals and payout destinations ──────────────────────────────────────────────────

    public const string WithdrawalNotFound = "financial.withdrawal.not_found";
    public const string WithdrawalBelowMinimum = "financial.withdrawal.below_minimum";
    public const string WithdrawalAboveMaximum = "financial.withdrawal.above_maximum";
    public const string WithdrawalDailyLimitReached = "financial.withdrawal.daily_limit_reached";
    public const string WithdrawalNotCancellable = "financial.withdrawal.not_cancellable";
    public const string WithdrawalTransitionInvalid = "financial.withdrawal.transition_invalid";
    public const string DestinationNotFound = "financial.payout_destination.not_found";
    public const string DestinationNotVerified = "financial.payout_destination.not_verified";
    public const string DestinationCurrencyMismatch = "financial.payout_destination.currency_mismatch";

    // ── Refunds ──────────────────────────────────────────────────────────────────────────────

    public const string RefundNotAllowed = "financial.refund.not_allowed";
    public const string RefundExceedsCaptured = "financial.refund.exceeds_captured";
    public const string RefundRequestNotFound = "financial.refund_request.not_found";
    public const string RefundRequestExists = "financial.refund_request.already_open";
    public const string RefundRequestNotDecidable = "financial.refund_request.not_decidable";
    public const string RefundWindowClosed = "financial.refund_request.window_closed";

    // ── Invoices and saved payment methods ───────────────────────────────────────────────────

    public const string InvoiceNotFound = "financial.invoice.not_found";
    public const string InvoiceCursorInvalid = "financial.invoice.cursor_invalid";

    /// <summary>
    /// Every forget request answers this today: the platform stores no reusable payment method.
    /// </summary>
    public const string PaymentMethodNotFound = "financial.payment_method.not_found";

    // ── Reward points ────────────────────────────────────────────────────────────────────────

    public const string RewardAccountNotFound = "financial.rewards.account_not_found";
    public const string RewardInsufficientPoints = "financial.rewards.insufficient_points";
    public const string RewardBelowMinimum = "financial.rewards.below_minimum";
    public const string RewardAboveMaximum = "financial.rewards.above_maximum";
    public const string RewardDailyLimitReached = "financial.rewards.daily_limit_reached";
    public const string RewardNotEligible = "financial.rewards.not_eligible";
    public const string RewardConversionNotSupported = "financial.rewards.conversion_not_supported";
    public const string RewardGrantDuplicate = "financial.rewards.grant_duplicate";

    // ── Policy ───────────────────────────────────────────────────────────────────────────────

    public const string PolicyNotFound = "financial.policy.not_found";
    public const string PolicyDuplicate = "financial.policy.duplicate";
    public const string PolicyImmutable = "financial.policy.immutable";
    public const string PolicyUnresolved = "financial.policy.unresolved";
    public const string PolicyParameterMissing = "financial.policy.parameter_missing";

    // ── Monetization catalogue ───────────────────────────────────────────────────────────────

    public const string ProductNotFound = "financial.product.not_found";
    public const string ProductDuplicate = "financial.product.duplicate";
    public const string ProductRetired = "financial.product.retired";
    public const string ProductNotEligible = "financial.product.not_eligible";
    public const string ProductSubjectMismatch = "financial.product.subject_mismatch";
    public const string PurchaseNotFound = "financial.purchase.not_found";
    public const string PurchaseAlreadySettled = "financial.purchase.already_settled";

    // ── Placements ───────────────────────────────────────────────────────────────────────────

    public const string PlacementNotFound = "financial.placement.not_found";
    public const string PlacementOverlaps = "financial.placement.overlaps";
    public const string PlacementNotActive = "financial.placement.not_active";
    public const string PlacementDurationInvalid = "financial.placement.duration_invalid";
    public const string PlacementNotPausable = "financial.placement.not_pausable";
    public const string PlacementNotPaused = "financial.placement.not_paused";
    public const string PlacementNotRenewable = "financial.placement.not_renewable";

    // ── Subscriptions ────────────────────────────────────────────────────────────────────────

    public const string PlanNotFound = "financial.plan.not_found";
    public const string PlanDuplicate = "financial.plan.duplicate";
    public const string SubscriptionNotFound = "financial.subscription.not_found";
    public const string SubscriptionAlreadyActive = "financial.subscription.already_active";
    public const string SubscriptionNotCancellable = "financial.subscription.not_cancellable";
    public const string SubscriptionTrialUsed = "financial.subscription.trial_used";
    public const string SubscriptionChangeNotPermitted = "financial.subscription.change_not_permitted";
    public const string SubscriptionSamePlan = "financial.subscription.same_plan";

    // ── Promotions ───────────────────────────────────────────────────────────────────────────

    public const string PromotionNotFound = "financial.promotion.not_found";
    public const string PromotionDuplicate = "financial.promotion.duplicate";
    public const string PromotionExpired = "financial.promotion.expired";
    public const string PromotionExhausted = "financial.promotion.exhausted";
    public const string PromotionNotApplicable = "financial.promotion.not_applicable";
    public const string PromotionAlreadyRedeemed = "financial.promotion.already_redeemed";
    public const string PromotionCountryRestricted = "financial.promotion.country_restricted";

    // ── Credit ───────────────────────────────────────────────────────────────────────────────

    public const string CreditGrantNotFound = "financial.credit.not_found";
    public const string CreditGrantRevoked = "financial.credit.revoked";
    public const string CreditGrantExpired = "financial.credit.expired";

    // ── Giving ───────────────────────────────────────────────────────────────────────────────

    public const string CauseNotFound = "financial.cause.not_found";
    public const string CauseInactive = "financial.cause.inactive";
    public const string CauseDuplicate = "financial.cause.duplicate";
    public const string DonationNotFound = "financial.donation.not_found";
    public const string FundNotFound = "financial.fund.not_found";
    public const string FundInactive = "financial.fund.inactive";
    public const string FundDuplicate = "financial.fund.duplicate";
    public const string ContributionNotFound = "financial.contribution.not_found";
    public const string ContributionKindNotAccepted = "financial.contribution.kind_not_accepted";
    public const string ContributionAmountRequired = "financial.contribution.amount_required";
    public const string ContributionPledgeRequired = "financial.contribution.pledge_required";

    // ── B15.1 Giving: the membership binding ─────────────────────────────────────────────────

    /// <summary>A payment subject that is half-supplied, unrecognised, or a number nobody named.</summary>
    public const string SubjectInvalid = "financial.subject.invalid";

    /// <summary>The intent names a different subject than the membership being paid for.</summary>
    public const string MembershipSubjectMismatch = "financial.membership.subject_mismatch";

    /// <summary>The payment has not settled. Authorised, pending, failed and cancelled all land here.</summary>
    public const string MembershipPaymentNotSettled = "financial.membership.payment_not_settled";

    /// <summary>The payment is already bound, and to a different membership.</summary>
    public const string MembershipPaymentAlreadyBound = "financial.membership.payment_already_bound";

    /// <summary>Nothing can describe this membership: no seam is registered, or it does not exist.</summary>
    public const string MembershipNotFound = "financial.membership.not_found";

    // ── B15.1 Giving: campaigns ──────────────────────────────────────────────────────────────

    public const string CampaignNotFound = "financial.campaign.not_found";
    public const string CampaignTransitionInvalid = "financial.campaign.transition_invalid";
    public const string CampaignNotEditable = "financial.campaign.not_editable";
    public const string CampaignTermsNotAccepted = "financial.campaign.terms_not_accepted";
    public const string CampaignOrganizerForbidden = "financial.campaign.organizer_forbidden";
    public const string CampaignUpdateNotFound = "financial.campaign.update_not_found";
    public const string CampaignMilestoneInvalid = "financial.campaign.milestone_invalid";
    public const string CampaignAllocationInvalid = "financial.campaign.allocation_invalid";

    // ── B15.1 Giving: recurring donations ────────────────────────────────────────────────────

    public const string RecurringDonationNotFound = "financial.recurring_donation.not_found";
    public const string RecurringDonationIntervalInvalid = "financial.recurring_donation.interval_invalid";

    // ── Fraud and rate limiting ──────────────────────────────────────────────────────────────

    public const string FraudRefused = "financial.fraud.refused";
    public const string FraudReview = "financial.fraud.review";
    public const string RateLimited = "financial.rate_limited";

    // ── Not implemented here, on purpose ─────────────────────────────────────────────────────

    /// <summary>
    /// Returned by any surface that would need an exchange rate. The platform never converts, and
    /// saying so explicitly is better than an empty result somebody reads as "no data".
    /// </summary>
    public const string ConversionNotSupported = "financial.conversion.not_supported";

    /// <summary>
    /// Returned by the payout surface. The seams exist; no settlement engine ships in this phase.
    /// </summary>
    public const string PayoutNotSupported = "financial.payout.not_supported";

    // ── Payout routing ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// One code per reason the router declined, because "unavailable" is not something a member can
    /// act on. Somebody told their country is unsupported stops; somebody told their <em>bank</em> is
    /// unsupported tries mobile money. The distinction is <c>PayoutRefusalReason</c>'s whole purpose
    /// and it survives to the client only if the codes are distinct.
    /// </summary>
    public const string PayoutCountryUnsupported = "financial.payout.country_unsupported";

    public const string PayoutMethodUnsupported = "financial.payout.method_unsupported";

    public const string PayoutCurrencyUnsupported = "financial.payout.currency_unsupported";

    /// <summary>
    /// Rows match and none of them has been confirmed against provider documentation. Distinct from
    /// <see cref="PayoutCountryUnsupported"/> on purpose: this is a gap in what the platform knows,
    /// not a gap in what the provider does, and the two are fixed by different people.
    /// </summary>
    public const string PayoutCapabilityUnknown = "financial.payout.capability_unknown";

    /// <summary>A supported route this deployment has switched off. A setting, not a limitation.</summary>
    public const string PayoutRouteDisabled = "financial.payout.route_disabled";

    public const string PayoutAmountOutOfRange = "financial.payout.amount_out_of_range";

    /// <summary>The route requires a verified payee and this one is not. See <c>PayoutRefusalReason</c>.</summary>
    public const string PayoutVerificationRequired = "financial.payout.verification_required";

    /// <summary>
    /// The matrix names an adapter nobody registered. The platform's own misconfiguration rather
    /// than a capability gap, and reported separately so it does not read as a country problem.
    /// </summary>
    public const string PayoutProviderNotRegistered = "financial.payout.provider_not_registered";

    /// <summary>
    /// The payee has not completed the chosen provider's own onboarding. Separate from
    /// <see cref="PayoutVerificationRequired"/>: that one is the platform's identity check, this one
    /// is a provider's, and they are finished in different places.
    /// </summary>
    public const string PayoutProviderOnboardingRequired = "financial.payout.provider_onboarding_required";

    /// <summary>
    /// The saved payout destination belongs to a provider that can no longer carry this. The member
    /// registers a new destination; the platform has not lost coverage of their country.
    /// </summary>
    public const string PayoutDestinationProviderUnavailable =
        "financial.payout.destination_provider_unavailable";

    // -- Advertising campaigns -----------------------------------------------------------------
    //
    // Prefixed `financial.ad_campaign` and not `financial.campaign`, which is taken: the giving
    // campaign has held that prefix since B15.1 and a client switching on `financial.campaign.*`
    // would start matching an entirely different entity. Two things called a campaign live in this
    // module; only one of them can own the short name, and it is the one that had it first.

    public const string AdCampaignNotFound = "financial.ad_campaign.not_found";
    public const string AdCampaignOwnerRequired = "financial.ad_campaign.owner_required";
    public const string AdCampaignNameLength = "financial.ad_campaign.name.length";
    public const string AdCampaignObjectiveUnknown = "financial.ad_campaign.objective.unknown";
    public const string AdCampaignBudgetRequired = "financial.ad_campaign.budget.required";
    public const string AdCampaignScheduleRequiresEnd = "financial.ad_campaign.schedule.requires_end";
    public const string AdCampaignScheduleAlreadyOver = "financial.ad_campaign.schedule.already_over";
    public const string AdCampaignNotEditable = "financial.ad_campaign.not_editable";
    public const string AdCampaignNotSubmittable = "financial.ad_campaign.not_submittable";
    public const string AdCampaignNotAwaitingPayment = "financial.ad_campaign.not_awaiting_payment";
    public const string AdCampaignNotReviewable = "financial.ad_campaign.not_reviewable";
    public const string AdCampaignRejectionNeedsReason = "financial.ad_campaign.rejection.needs_reason";
    public const string AdCampaignNothingToAppeal = "financial.ad_campaign.nothing_to_appeal";
    public const string AdCampaignAlreadyAppealed = "financial.ad_campaign.already_appealed";
    public const string AdCampaignAppealNeedsReason = "financial.ad_campaign.appeal.needs_reason";
    public const string AdCampaignNotSchedulable = "financial.ad_campaign.not_schedulable";
    public const string AdCampaignNotActivatable = "financial.ad_campaign.not_activatable";
    public const string AdCampaignScheduleNotStarted = "financial.ad_campaign.schedule.not_started";
    public const string AdCampaignNotPausable = "financial.ad_campaign.not_pausable";
    public const string AdCampaignNotResumable = "financial.ad_campaign.not_resumable";
    public const string AdCampaignNotDelivering = "financial.ad_campaign.not_delivering";
    public const string AdCampaignCurrencyMismatch = "financial.ad_campaign.currency_mismatch";
    public const string AdCampaignSpendMustBePositive = "financial.ad_campaign.spend.must_be_positive";
    public const string AdCampaignAlreadyFinished = "financial.ad_campaign.already_finished";
    public const string AdCampaignPlacementAlreadyBooked = "financial.ad_campaign.placement.already_booked";
    public const string AdCampaignSubjectIncomplete = "financial.ad_campaign.subject.incomplete";
    public const string AdCampaignDecisionUnknown = "financial.ad_campaign.decision.unknown";
    public const string AdCampaignCannotReviewOwn = "financial.ad_campaign.cannot_review_own";
    public const string AdCampaignDimensionUnknown = "financial.ad_campaign.dimension.unknown";
    public const string AdCampaignProductRequired = "financial.ad_campaign.product.required";
    public const string AdCampaignBudgetMismatch = "financial.ad_campaign.budget.mismatch";
}
