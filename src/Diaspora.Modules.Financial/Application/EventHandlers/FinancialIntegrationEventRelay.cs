using Diaspora.Contracts.Events;
using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.Modules.Financial.Application.Campaigns;
using Diaspora.Modules.Financial.Domain.Events;
using Diaspora.Platform.Messaging;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Financial.Application.EventHandlers;

/// <summary>
/// Turns B15's domain events into the published contracts other modules consume.
/// <para>
/// <b>The one translation seam, and the reason this module's domain stays private.</b> A domain
/// event carries strongly typed identifiers, domain enums and <c>Money</c>; a published contract
/// carries flat primitives — minor units and a currency string — that a consumer can parse without
/// referencing this module. Doing it in a single class means the two cannot drift and a refactor
/// inside an aggregate cannot change somebody else's contract.
/// </para>
/// <para>
/// <b>Nothing published from here says what anybody's balance is.</b> A balance is the most
/// inferential financial thing the platform holds; putting it on a bus every module reads would make
/// this the largest unnecessary disclosure surface on the platform. What travels is shape: an
/// amount that moved, a status that changed, a placement that started. B14 made the same call about
/// personalization profiles and B13 about notification text.
/// </para>
/// <para>
/// <b>An anonymous donation publishes no donor.</b> Not a null flag beside a real identifier — the
/// aggregate never stored one.
/// </para>
/// <para>
/// <b>A publish failure never fails the write.</b> The aggregate is already saved by the time this
/// runs; throwing would roll back somebody's payment because an analytics consumer was unreachable.
/// At-least-once is the contract, and <c>EventId</c> is the idempotency key — which is why no payload
/// here declares a property called <c>EventId</c>. The base <c>IntegrationEvent</c> owns that name,
/// and shadowing it compiles with a warning while silently breaking every consumer's de-duplication.
/// </para>
/// </summary>
public sealed class FinancialIntegrationEventRelay
{
    private readonly IIntegrationEventPublisher _publisher;
    private readonly ILogger<FinancialIntegrationEventRelay> _logger;

    public FinancialIntegrationEventRelay(
        IIntegrationEventPublisher publisher, ILogger<FinancialIntegrationEventRelay> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public Task PublishAsync(
        WalletCreatedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new WalletCreatedIntegrationEvent
            {
                WalletId = domainEvent.WalletId.Value,
                AccountId = domainEvent.AccountId.Value,
                OwnerId = domainEvent.OwnerId,
                OwnerType = domainEvent.OwnerType.ToString(),
                WalletType = domainEvent.WalletType.ToString(),
                Currency = domainEvent.Currency,
                CreatedAt = domainEvent.CreatedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        WalletStatusChangedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new WalletStatusChangedIntegrationEvent
            {
                WalletId = domainEvent.WalletId.Value,
                OwnerId = domainEvent.OwnerId,
                OwnerType = domainEvent.OwnerType.ToString(),
                PreviousStatus = domainEvent.PreviousStatus.ToString(),
                Status = domainEvent.Status.ToString(),
                Currency = domainEvent.Currency,
                Reason = domainEvent.Reason,
                ChangedAt = domainEvent.ChangedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        TransactionRecordedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new TransactionRecordedIntegrationEvent
            {
                TransactionId = domainEvent.TransactionId.Value,
                TransactionType = domainEvent.TransactionType.ToString(),
                Status = domainEvent.Status.ToString(),
                AmountMinorUnits = domainEvent.AmountMinorUnits,
                Currency = domainEvent.Currency,
                EntryCount = domainEvent.EntryCount,
                InitiatedByUserId = domainEvent.InitiatedByUserId,
                SubjectType = domainEvent.SubjectType?.ToString(),
                SubjectId = domainEvent.SubjectId,
                RecordedAt = domainEvent.RecordedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        PaymentIntentCreatedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new PaymentIntentCreatedIntegrationEvent
            {
                PaymentIntentId = domainEvent.PaymentIntentId.Value,
                PayerUserId = domainEvent.PayerUserId,
                AmountMinorUnits = domainEvent.AmountMinorUnits,
                Currency = domainEvent.Currency,
                MethodKind = domainEvent.MethodKind.ToString(),
                SubjectType = domainEvent.SubjectType?.ToString(),
                SubjectId = domainEvent.SubjectId,
                CountryCode = domainEvent.CountryCode,
                ExpiresAt = domainEvent.ExpiresAt,
                CreatedAt = domainEvent.CreatedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        PaymentAuthorizedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new PaymentAuthorizedIntegrationEvent
            {
                PaymentIntentId = domainEvent.PaymentIntentId.Value,
                PayerUserId = domainEvent.PayerUserId,
                AmountMinorUnits = domainEvent.AmountMinorUnits,
                Currency = domainEvent.Currency,
                Provider = domainEvent.Provider,
                AuthorizedAt = domainEvent.AuthorizedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        PaymentCapturedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new PaymentCapturedIntegrationEvent
            {
                PaymentIntentId = domainEvent.PaymentIntentId.Value,
                PayerUserId = domainEvent.PayerUserId,
                AmountMinorUnits = domainEvent.AmountMinorUnits,
                Currency = domainEvent.Currency,
                SubjectType = domainEvent.SubjectType?.ToString(),
                SubjectId = domainEvent.SubjectId,
                Provider = domainEvent.Provider,
                CountryCode = domainEvent.CountryCode,
                CapturedAt = domainEvent.CapturedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        PaymentFailedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new PaymentFailedIntegrationEvent
            {
                PaymentIntentId = domainEvent.PaymentIntentId.Value,
                PayerUserId = domainEvent.PayerUserId,
                AmountMinorUnits = domainEvent.AmountMinorUnits,
                Currency = domainEvent.Currency,
                FailureCode = domainEvent.FailureCode,
                FailureMessage = domainEvent.FailureMessage,
                IsRetryable = domainEvent.IsRetryable,
                FailedAt = domainEvent.FailedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        RefundIssuedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new RefundIssuedIntegrationEvent
            {
                RefundId = domainEvent.RefundId.Value,
                PaymentIntentId = domainEvent.PaymentIntentId.Value,
                PayerUserId = domainEvent.PayerUserId,
                AmountMinorUnits = domainEvent.AmountMinorUnits,
                Currency = domainEvent.Currency,
                IsFullRefund = domainEvent.IsFullRefund,
                Reason = domainEvent.Reason.ToString(),
                SubjectType = domainEvent.SubjectType?.ToString(),
                SubjectId = domainEvent.SubjectId,
                IssuedAt = domainEvent.IssuedAt,
                PayeeUserId = domainEvent.PayeeUserId
            },
            cancellationToken);
    }

    public Task PublishAsync(
        MonetizationProductPurchasedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new MonetizationProductPurchasedIntegrationEvent
            {
                PurchaseId = domainEvent.PurchaseId.Value,
                TransactionId = domainEvent.TransactionId?.Value,
                BuyerUserId = domainEvent.BuyerUserId,
                ProductCode = domainEvent.ProductCode,
                Category = domainEvent.Category.ToString(),
                SubjectType = domainEvent.SubjectType.ToString(),
                SubjectId = domainEvent.SubjectId,
                PaidMinorUnits = domainEvent.PaidMinorUnits,
                ListMinorUnits = domainEvent.ListMinorUnits,
                Currency = domainEvent.Currency,
                PromotionCode = domainEvent.PromotionCode,
                PolicyVersion = domainEvent.PolicyVersion,
                CountryCode = domainEvent.CountryCode,
                PurchasedAt = domainEvent.PurchasedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        PlacementActivatedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new PlacementActivatedIntegrationEvent
            {
                PlacementId = domainEvent.PlacementId.Value,
                SubjectType = domainEvent.SubjectType.ToString(),
                SubjectId = domainEvent.SubjectId,
                Kind = domainEvent.Kind.ToString(),
                PurchasedByUserId = domainEvent.PurchasedByUserId,
                PriorityWeight = domainEvent.PriorityWeight,
                ProductCode = domainEvent.ProductCode,
                CountryCode = domainEvent.CountryCode,
                StartsAt = domainEvent.StartsAt,
                EndsAt = domainEvent.EndsAt,
                ActivatedAt = domainEvent.ActivatedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        PlacementEndedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new PlacementEndedIntegrationEvent
            {
                PlacementId = domainEvent.PlacementId.Value,
                SubjectType = domainEvent.SubjectType.ToString(),
                SubjectId = domainEvent.SubjectId,
                Kind = domainEvent.Kind.ToString(),
                Status = domainEvent.Status.ToString(),
                PurchasedByUserId = domainEvent.PurchasedByUserId,
                EndedAt = domainEvent.EndedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        SubscriptionActivatedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new SubscriptionActivatedIntegrationEvent
            {
                SubscriptionId = domainEvent.SubscriptionId.Value,
                SubscriberUserId = domainEvent.SubscriberUserId,
                PlanCode = domainEvent.PlanCode,
                Tier = domainEvent.Tier.ToString(),
                Status = domainEvent.Status.ToString(),
                Interval = domainEvent.Interval.ToString(),
                Entitlements = domainEvent.Entitlements,
                IsTrial = domainEvent.IsTrial,
                IsRenewal = domainEvent.IsRenewal,
                PeriodStartsAt = domainEvent.PeriodStartsAt,
                PeriodEndsAt = domainEvent.PeriodEndsAt,
                ActivatedAt = domainEvent.ActivatedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        SubscriptionEndedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new SubscriptionEndedIntegrationEvent
            {
                SubscriptionId = domainEvent.SubscriptionId.Value,
                SubscriberUserId = domainEvent.SubscriberUserId,
                PlanCode = domainEvent.PlanCode,
                Tier = domainEvent.Tier.ToString(),
                Status = domainEvent.Status.ToString(),
                Reason = domainEvent.Reason,
                EndedAt = domainEvent.EndedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        DonationCompletedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new DonationCompletedIntegrationEvent
            {
                // The campaign is resolved here, where the encoding is owned, so a consumer never
                // has to know that a cause code spells one.
                CampaignId = CampaignMapper.CampaignIdFrom(domainEvent.CauseCode),

                DonationId = domainEvent.DonationId.Value,
                TransactionId = domainEvent.TransactionId?.Value,

                // Absent, not hidden. The aggregate stored nothing when anonymous.
                DonorUserId = domainEvent.DonorUserId,
                IsAnonymous = domainEvent.IsAnonymous,
                BeneficiaryType = domainEvent.BeneficiaryType.ToString(),
                BeneficiaryId = domainEvent.BeneficiaryId,
                CauseCode = domainEvent.CauseCode,
                AmountMinorUnits = domainEvent.AmountMinorUnits,
                Currency = domainEvent.Currency,
                ReceiptReference = domainEvent.ReceiptReference,
                CountryCode = domainEvent.CountryCode,
                CompletedAt = domainEvent.CompletedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        CommunityContributionCompletedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new CommunityContributionCompletedIntegrationEvent
            {
                ContributionId = domainEvent.ContributionId.Value,
                FundId = domainEvent.FundId.Value,
                FundCode = domainEvent.FundCode,
                ContributorUserId = domainEvent.ContributorUserId,
                IsAnonymous = domainEvent.IsAnonymous,
                Kind = domainEvent.Kind.ToString(),
                AmountMinorUnits = domainEvent.AmountMinorUnits,
                Currency = domainEvent.Currency,
                PledgeQuantity = domainEvent.PledgeQuantity,
                PledgeUnit = domainEvent.PledgeUnit,
                ReceiptReference = domainEvent.ReceiptReference,
                CountryCode = domainEvent.CountryCode,
                CompletedAt = domainEvent.CompletedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        FinancialPolicyChangedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new FinancialPolicyChangedIntegrationEvent
            {
                PolicyId = domainEvent.PolicyId.Value,
                PolicyKey = domainEvent.PolicyKey,
                Kind = domainEvent.Kind.ToString(),
                Scope = domainEvent.Scope.ToString(),
                ScopeValue = domainEvent.ScopeValue,
                Version = domainEvent.Version,
                ChangeType = domainEvent.ChangeType,
                ChangedByUserId = domainEvent.ChangedByUserId,
                EffectiveFrom = domainEvent.EffectiveFrom,
                ChangedAt = domainEvent.ChangedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        CreditGrantedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new CreditGrantedIntegrationEvent
            {
                GrantId = domainEvent.GrantId.Value,
                OwnerId = domainEvent.OwnerId,
                OwnerType = domainEvent.OwnerType.ToString(),
                Kind = domainEvent.Kind.ToString(),
                AmountMinorUnits = domainEvent.AmountMinorUnits,
                Currency = domainEvent.Currency,
                Reason = domainEvent.Reason,
                ExpiresAt = domainEvent.ExpiresAt,
                GrantedAt = domainEvent.GrantedAt
            },
            cancellationToken);
    }

    // ── B15.1 ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A member's own balance went up because they put money into it.
    /// <para>
    /// Published from the capture handler alongside the capture itself, because both are true and a
    /// consumer wanting one should not have to infer it from the other. B13 sends a top-up receipt
    /// off this and a payment receipt off the capture; sending the wrong one tells a member they have
    /// been charged.
    /// </para>
    /// </summary>
    public Task PublishDepositAsync(
        PaymentCapturedDomainEvent domainEvent,
        Guid walletId,
        string? methodKind,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new DepositCompletedIntegrationEvent
            {
                PaymentIntentId = domainEvent.PaymentIntentId.Value,
                DepositorUserId = domainEvent.PayerUserId,
                WalletId = walletId,
                AmountMinorUnits = domainEvent.AmountMinorUnits,
                Currency = domainEvent.Currency,
                MethodKind = methodKind,
                CountryCode = domainEvent.CountryCode,
                CompletedAt = domainEvent.CapturedAt
            },
            cancellationToken);
    }

    /// <summary>
    /// A new payout destination (BSEC-007). Published verified or not: a member who did not add it
    /// needs to hear before it becomes payable.
    /// </summary>
    public Task PublishAsync(
        PayoutDestinationRegisteredDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new PayoutDestinationRegisteredIntegrationEvent
            {
                DestinationId = domainEvent.DestinationId.Value,
                OwnerUserId = domainEvent.OwnerUserId,
                Kind = domainEvent.Kind.ToString(),

                // The masked label the member would recognise, never the provider token.
                Label = domainEvent.Label,
                Currency = domainEvent.Currency,
                Provider = domainEvent.Provider,
                CountryCode = domainEvent.CountryCode,
                RegisteredAt = domainEvent.RegisteredAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        WithdrawalStatusChangedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new WithdrawalStatusChangedIntegrationEvent
            {
                WithdrawalId = domainEvent.WithdrawalId.Value,
                RequestedByUserId = domainEvent.RequestedByUserId,
                Status = domainEvent.Status.ToString(),
                PreviousStatus = domainEvent.PreviousStatus?.ToString(),
                AmountMinorUnits = domainEvent.GrossMinorUnits,
                FeeMinorUnits = domainEvent.FeeMinorUnits,
                Currency = domainEvent.Currency,

                // The masked label. No credential has ever been on this bus and none should be.
                DestinationLabel = domainEvent.DestinationLabel,
                FailureCode = domainEvent.FailureCode,
                ChangedAt = domainEvent.ChangedAt
            },
            cancellationToken);
    }

    /// <summary>
    /// A withdrawal's current state, announced from a sweep rather than from a domain event.
    /// <para>
    /// The sweep saves before it publishes, so the aggregate's own events have already been dispatched
    /// by the time the outcome is known. Reading the aggregate is the same fact by a shorter route.
    /// </para>
    /// </summary>
    public Task PublishAsync(
        Domain.Withdrawals.Withdrawal withdrawal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(withdrawal);

        return PublishSafelyAsync(
            new WithdrawalStatusChangedIntegrationEvent
            {
                WithdrawalId = withdrawal.Id.Value,
                RequestedByUserId = withdrawal.RequestedByUserId,
                Status = withdrawal.Status.ToString(),
                PreviousStatus = Domain.Enums.WithdrawalStatus.Processing.ToString(),
                AmountMinorUnits = withdrawal.GrossMinorUnits,
                FeeMinorUnits = withdrawal.FeeMinorUnits,
                Currency = withdrawal.Currency,
                DestinationLabel = withdrawal.DestinationLabel,
                FailureCode = withdrawal.FailureCode,
                ChangedAt = withdrawal.ModifiedAt ?? withdrawal.RequestedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        Domain.Payments.RefundRequest request,
        bool isDecision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!isDecision)
        {
            return PublishSafelyAsync(
                new RefundRequestedIntegrationEvent
                {
                    RefundRequestId = request.Id.Value,
                    PaymentIntentId = request.PaymentIntentId.Value,
                    RequestedByUserId = request.RequestedByUserId,
                    AmountMinorUnits = request.AmountMinorUnits,
                    Currency = request.Currency,
                    Reason = request.Reason.ToString(),
                    RequestedAt = request.RequestedAt
                },
                cancellationToken);
        }

        return PublishSafelyAsync(
            new RefundRequestDecidedIntegrationEvent
            {
                RefundRequestId = request.Id.Value,
                PaymentIntentId = request.PaymentIntentId.Value,
                RequestedByUserId = request.RequestedByUserId,
                Decision = request.Status.ToString(),
                AmountMinorUnits = request.AmountMinorUnits,
                Currency = request.Currency,
                DecisionNote = request.DecisionNote,
                DecidedAt = request.DecidedAt ?? request.RequestedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        Domain.Rewards.RewardConversion conversion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversion);

        return PublishSafelyAsync(
            new RewardPointsConvertedIntegrationEvent
            {
                ConversionId = conversion.Id.Value,
                MemberUserId = conversion.MemberUserId,
                Points = conversion.Points,
                CreditedMinorUnits = conversion.CreditedMinorUnits,
                Currency = conversion.Currency,

                // The rate as applied, never recomputed from today's policy.
                RateMinorUnitsPerPoint = conversion.RateMinorUnitsPerPoint,
                PolicyVersion = conversion.PolicyVersion,
                WalletId = conversion.WalletId.Value,
                ConvertedAt = conversion.ConvertedAt
            },
            cancellationToken);
    }

    public Task PublishExpiringAsync(
        Domain.Placements.Placement placement,
        int leadDays,
        DateTimeOffset announcedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(placement);

        return placement.Window.EndsAt is not { } endsAt
            ? Task.CompletedTask
            : PublishSafelyAsync(
                new PlacementExpiringIntegrationEvent
                {
                    PlacementId = placement.Id.Value,
                    SubjectType = placement.Subject.SubjectType.ToString(),
                    SubjectId = placement.Subject.SubjectId,
                    Kind = placement.Kind.ToString(),
                    PurchasedByUserId = placement.PurchasedByUserId,
                    ProductCode = placement.ProductCode,
                    AutoRenewEnabled = placement.AutoRenewEnabled,
                    EndsAt = endsAt,
                    LeadDays = leadDays,
                    AnnouncedAt = announcedAt
                },
                cancellationToken);
    }

    public Task PublishExpiringAsync(
        Domain.Subscriptions.Subscription subscription,
        int leadDays,
        DateTimeOffset announcedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        return subscription.CurrentPeriod.EndsAt is not { } endsAt
            ? Task.CompletedTask
            : PublishSafelyAsync(
                new SubscriptionExpiringIntegrationEvent
                {
                    SubscriptionId = subscription.Id.Value,
                    SubscriberUserId = subscription.SubscriberUserId,
                    PlanCode = subscription.PlanCode,
                    Tier = subscription.Tier.ToString(),
                    WillRenew = !subscription.CancelAtPeriodEnd,
                    PeriodEndsAt = endsAt,
                    LeadDays = leadDays,
                    AnnouncedAt = announcedAt
                },
                cancellationToken);
    }

    // ── B15.1 Giving Extension ───────────────────────────────────────────────────────────────

    public Task PublishAsync(
        MembershipPaymentSettledDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new MembershipPaymentSettledIntegrationEvent
            {
                BindingId = domainEvent.BindingId.Value,
                MembershipId = domainEvent.MembershipId,
                PaymentIntentId = domainEvent.PaymentIntentId.Value,
                PayerUserId = domainEvent.PayerUserId,
                OrganizationId = domainEvent.OrganizationId,
                AmountMinorUnits = domainEvent.AmountMinorUnits,
                Currency = domainEvent.Currency,
                SettledAt = domainEvent.SettledAt,
                BoundAt = domainEvent.BoundAt,
                IsReplay = domainEvent.IsReplay
            },
            cancellationToken);
    }

    /// <summary>
    /// An *advertising* campaign moved between states. MON-GAP-001.
    /// <para>
    /// Both states travel, so a consumer decides which transitions are worth telling somebody
    /// about. The register wanted approval and rejection; keeping that rule in B13 means the next
    /// one needs no change here.
    /// </para>
    /// </summary>
    public Task PublishAsync(
        AdCampaignStatusChangedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new AdCampaignStatusChangedIntegrationEvent
            {
                AdCampaignId = domainEvent.AdCampaignId.Value,
                OwnerUserId = domainEvent.OwnerUserId,
                CampaignName = domainEvent.CampaignName,
                PreviousStatus = domainEvent.PreviousStatus,
                CurrentStatus = domainEvent.CurrentStatus,
                Reason = domainEvent.Reason
            },
            cancellationToken);
    }

    /// <summary>
    /// A *fundraising* campaign moved. Not to be confused with the advertising one above — two
    /// entities share the noun and the two events are deliberately different types.
    /// </summary>
    public Task PublishAsync(
        CampaignStatusChangedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new CampaignStatusChangedIntegrationEvent
            {
                CampaignId = domainEvent.CampaignId.Value,
                OrganizerUserId = domainEvent.OrganizerUserId,
                OrganizationId = domainEvent.OrganizationId,
                PreviousStatus = domainEvent.PreviousStatus,
                CurrentStatus = domainEvent.CurrentStatus,
                CauseCode = domainEvent.CauseCode,
                Reason = domainEvent.Reason,
                ChangedAt = domainEvent.ChangedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        CampaignUpdatePublishedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new CampaignUpdatePublishedIntegrationEvent
            {
                CampaignId = domainEvent.CampaignId.Value,
                UpdateId = domainEvent.UpdateId.Value,
                AuthorUserId = domainEvent.AuthorUserId,
                Title = domainEvent.Title,
                CauseCode = domainEvent.CauseCode,
                PublishedAt = domainEvent.PublishedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        CampaignMilestoneReachedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new CampaignMilestoneReachedIntegrationEvent
            {
                CampaignId = domainEvent.CampaignId.Value,
                MilestoneId = domainEvent.MilestoneId,
                Label = domainEvent.Label,
                TargetMinorUnits = domainEvent.TargetMinorUnits,
                RaisedMinorUnits = domainEvent.RaisedMinorUnits,
                Currency = domainEvent.Currency,
                CauseCode = domainEvent.CauseCode,
                ReachedAt = domainEvent.ReachedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        RecurringDonationChargedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new RecurringDonationChargedIntegrationEvent
            {
                RecurringDonationId = domainEvent.ScheduleId.Value,
                DonorUserId = domainEvent.DonorUserId,
                DonationId = domainEvent.DonationId.Value,
                CauseCode = domainEvent.CauseCode,
                AmountMinorUnits = domainEvent.AmountMinorUnits,
                Currency = domainEvent.Currency,
                OccurrenceNumber = domainEvent.OccurrenceNumber,
                NextChargeAt = domainEvent.NextChargeAt,
                ChargedAt = domainEvent.ChargedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        RecurringDonationFailedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new RecurringDonationFailedIntegrationEvent
            {
                RecurringDonationId = domainEvent.ScheduleId.Value,
                DonorUserId = domainEvent.DonorUserId,
                CauseCode = domainEvent.CauseCode,
                AmountMinorUnits = domainEvent.AmountMinorUnits,
                Currency = domainEvent.Currency,
                FailureReason = domainEvent.FailureReason,
                WillRetry = domainEvent.WillRetry,
                ConsecutiveFailures = domainEvent.ConsecutiveFailures,
                NextAttemptAt = domainEvent.NextAttemptAt,
                FailedAt = domainEvent.FailedAt
            },
            cancellationToken);
    }

    /// <summary>
    /// Enqueues, and never lets a failure reach the caller.
    /// <para>
    /// See the class summary. The aggregate is already saved; failing here would roll back somebody's
    /// payment because a consumer was unreachable.
    /// </para>
    /// </summary>
    /// <summary>
    /// Staff lifted a hold (CS-D4): the review a system hold waits for (D-13). The hold's note stays
    /// here, as an operator's note always does.
    /// </summary>
    public Task PublishHoldLiftedAsync(
        Guid memberUserId,
        Guid? liftedByUserId,
        DateTimeOffset liftedAt,
        CancellationToken cancellationToken = default) =>
        PublishSafelyAsync(
            new RewardConversionHoldLiftedIntegrationEvent
            {
                MemberUserId = memberUserId,
                LiftedByUserId = liftedByUserId,
                LiftedAt = liftedAt
            },
            cancellationToken);

    private async Task PublishSafelyAsync(
        IIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        try
        {
            await _publisher.PublishAsync(integrationEvent, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031
        catch (Exception exception)
#pragma warning restore CA1031
        {
            _logger.LogError(
                exception,
                "Failed to enqueue {EventType}. The aggregate was saved and the announcement was "
                + "lost.",
                integrationEvent.GetType().Name);
        }
    }
}
