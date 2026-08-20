using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Catalog;
using Diaspora.Modules.Financial.Domain.Credits;
using Diaspora.Modules.Financial.Domain.Currencies;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Ledgers;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.Modules.Financial.Domain.Placements;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Domain.Promotions;
using Diaspora.Modules.Financial.Domain.Purchases;
using Diaspora.Modules.Financial.Domain.Revenue;
using Diaspora.Modules.Financial.Domain.Subscriptions;
using Diaspora.Modules.Financial.Domain.Transactions;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Domain.Wallets;

namespace Diaspora.Modules.Financial.Application.Mapping;

/// <summary>
/// Turns aggregates into the shapes the API serves.
/// <para>
/// <b>Hand-written and static, like every other module's mapper since B5.</b> A financial response
/// is not a property-name-matching projection: an anonymous donation must omit a field the aggregate
/// holds, a placement must not be called <c>isFeatured</c>, and a wallet's five balances each need
/// the currency's symbol and exponent attached. A convention-based mapper would do the first one
/// wrong, silently.
/// </para>
/// <para>
/// <b>The symbol is passed in rather than looked up.</b> A mapper that queried the currency
/// catalogue would put a database round trip inside a projection loop; the caller resolves the
/// handful of currencies it needs once and passes them down.
/// </para>
/// </summary>
public static class FinancialMapper
{
    public static MoneyResponse ToResponse(Money money, string? symbol = null)
    {
        ArgumentNullException.ThrowIfNull(money);

        return new MoneyResponse
        {
            Amount = money.Amount,
            Currency = money.Currency,
            MinorUnits = money.MinorUnits,
            Exponent = money.Exponent,
            Symbol = symbol
        };
    }

    public static WalletResponse ToResponse(Wallet wallet, string? symbol = null)
    {
        ArgumentNullException.ThrowIfNull(wallet);

        return new WalletResponse
        {
            WalletId = wallet.Id.Value,
            AccountId = wallet.AccountId.Value,
            WalletType = wallet.WalletType.ToString(),
            OwnerType = wallet.OwnerType.ToString(),
            Status = wallet.Status.ToString(),
            Currency = wallet.Currency,
            Available = ToResponse(wallet.Available, symbol),
            Pending = ToResponse(wallet.Pending, symbol),
            Reserved = ToResponse(wallet.Reserved, symbol),
            LifetimeCredits = ToResponse(wallet.LifetimeCredits, symbol),
            LifetimeDebits = ToResponse(wallet.LifetimeDebits, symbol),
            EntryCount = wallet.EntryCount,
            LastEntryAt = wallet.LastEntryAt,
            StatusReason = wallet.StatusReason
        };
    }

    public static LedgerEntryResponse ToResponse(LedgerEntry entry, string? symbol = null)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new LedgerEntryResponse
        {
            EntryId = entry.Id.Value,
            TransactionId = entry.TransactionId.Value,
            Sequence = entry.Sequence,
            EntryType = entry.EntryType.ToString(),
            Direction = entry.Direction.ToString(),
            Bucket = entry.Bucket.ToString(),
            Amount = ToResponse(entry.Amount, symbol),
            BalanceAfter = ToResponse(
                Money.FromMinorUnits(
                    Math.Max(0, entry.BalanceAfterMinorUnits), entry.Currency, entry.Exponent),
                symbol),
            Reason = entry.Audit.Reason,
            ActorType = entry.Audit.Actor.ActorType.ToString(),
            Source = entry.Audit.Source.ToString(),
            PolicyVersion = entry.Audit.PolicyVersion,
            CorrelationId = entry.Audit.CorrelationId,
            SubjectType = entry.SubjectType?.ToString(),
            SubjectId = entry.SubjectId,
            ExternalReference = entry.ExternalReference,
            OccurredAt = entry.OccurredAt
        };
    }

    public static TransactionResponse ToResponse(FinancialTransaction transaction, string? symbol = null)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        return new TransactionResponse
        {
            TransactionId = transaction.Id.Value,
            TransactionType = transaction.TransactionType.ToString(),
            Status = transaction.Status.ToString(),
            Amount = ToResponse(transaction.Amount, symbol),
            Description = transaction.Description,
            Reason = transaction.Audit.Reason,
            SubjectType = transaction.SubjectType?.ToString(),
            SubjectId = transaction.SubjectId,
            CountryCode = transaction.CountryCode,
            PolicyVersion = transaction.Audit.PolicyVersion,
            EntryCount = transaction.EntryCount,
            ReversedByTransactionId = transaction.ReversedByTransactionId?.Value,
            OccurredAt = transaction.OccurredAt,
            CompletedAt = transaction.CompletedAt
        };
    }

    public static PaymentIntentResponse ToResponse(PaymentIntent intent, string? symbol = null)
    {
        ArgumentNullException.ThrowIfNull(intent);

        return new PaymentIntentResponse
        {
            PaymentIntentId = intent.Id.Value,
            Status = intent.Status.ToString(),
            Amount = ToResponse(intent.Amount, symbol),
            Captured = ToResponse(intent.Captured, symbol),
            Refunded = ToResponse(intent.Refunded, symbol),
            RefundableRemaining = ToResponse(intent.RefundableRemaining, symbol),
            MethodKind = intent.MethodKind.ToString(),
            Purpose = intent.Purpose.ToString(),
            Description = intent.Description,
            SubjectType = intent.SubjectType?.ToString(),
            SubjectId = intent.SubjectId,
            Provider = intent.GatewayProvider,
            NextAction = intent.NextActionPayload,
            FailureCode = intent.FailureCode,
            FailureMessage = intent.FailureMessage,
            AttemptCount = intent.Attempts.Count,
            Refunds = [.. intent.Refunds.Select(refund => ToResponse(refund, intent, symbol))],
            ExpiresAt = intent.ExpiresAt,
            AuthorizedAt = intent.AuthorizedAt,
            CapturedAt = intent.CapturedAt,
            CreatedAt = intent.CreatedAt
        };
    }

    public static RefundResponse ToResponse(
        RefundRecord refund, PaymentIntent intent, string? symbol = null)
    {
        ArgumentNullException.ThrowIfNull(refund);
        ArgumentNullException.ThrowIfNull(intent);

        return new RefundResponse
        {
            RefundId = refund.RefundId,
            Amount = ToResponse(
                Money.FromMinorUnits(refund.AmountMinorUnits, intent.Currency, intent.Exponent),
                symbol),
            Reason = refund.Reason.ToString(),
            Status = refund.Status.ToString(),
            Note = refund.Note,
            IssuedAt = refund.IssuedAt
        };
    }

    public static PlacementResponse ToResponse(Placement placement, DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(placement);

        return new PlacementResponse
        {
            PlacementId = placement.Id.Value,
            SubjectType = placement.Subject.SubjectType.ToString(),
            SubjectId = placement.Subject.SubjectId,
            Kind = placement.Kind.ToString(),
            Status = placement.Status.ToString(),
            IsActive = placement.IsInForce(asOf),
            PriorityWeight = placement.PriorityWeight,
            ProductCode = placement.ProductCode,
            CountryCode = placement.CountryCode,
            PolicyVersion = placement.PolicyVersion,
            AutoRenewEnabled = placement.AutoRenewEnabled,
            PausedAt = placement.PausedAt,
            StartsAt = placement.Window.StartsAt,
            EndsAt = placement.Window.EndsAt
        };
    }

    public static MonetizationProductResponse ToResponse(
        MonetizationProduct product,
        bool isAvailable = true,
        string? unavailableReasonCode = null,
        string? unavailableReason = null,
        string? symbol = null)
    {
        ArgumentNullException.ThrowIfNull(product);

        return new MonetizationProductResponse
        {
            ProductId = product.Id.Value,
            Code = product.Code,
            Name = product.Name,
            Description = product.Description,
            Category = product.Category.ToString(),
            SubjectType = product.SubjectType.ToString(),
            Status = product.Status.ToString(),
            Price = product.Price is { } price ? ToResponse(price, symbol) : null,
            DurationDays = product.DurationDays,
            PlacementKind = product.PlacementKind?.ToString(),
            PriorityWeight = product.PriorityWeight,
            IsRenewable = product.IsRenewable,
            IsAvailable = isAvailable,
            UnavailableReasonCode = unavailableReasonCode,
            UnavailableReason = unavailableReason,
            SortOrder = product.SortOrder
        };
    }

    public static MonetizationPurchaseResponse ToResponse(
        MonetizationPurchase purchase,
        Placement? placement = null,
        PaymentIntent? payment = null,
        DateTimeOffset asOf = default,
        string? symbol = null)
    {
        ArgumentNullException.ThrowIfNull(purchase);

        return new MonetizationPurchaseResponse
        {
            PurchaseId = purchase.Id.Value,
            ProductCode = purchase.ProductCode,
            Category = purchase.Category.ToString(),
            Status = purchase.Status.ToString(),
            SubjectType = purchase.Subject.SubjectType.ToString(),
            SubjectId = purchase.Subject.SubjectId,
            ListPrice = ToResponse(purchase.ListPrice, symbol),
            Discount = ToResponse(purchase.Discount, symbol),
            Paid = ToResponse(purchase.Paid, symbol),
            PromotionCode = purchase.PromotionCode,
            PolicyVersion = purchase.PolicyVersion,
            PaymentIntentId = purchase.PaymentIntentId?.Value,
            Placement = placement is null ? null : ToResponse(placement, asOf),
            Payment = payment is null ? null : ToResponse(payment, symbol),
            CreatedAt = purchase.CreatedAt,
            CompletedAt = purchase.CompletedAt
        };
    }

    public static SubscriptionPlanResponse ToResponse(SubscriptionPlan plan, string? symbol = null)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return new SubscriptionPlanResponse
        {
            PlanId = plan.Id.Value,
            Code = plan.Code,
            Name = plan.Name,
            Description = plan.Description,
            Tier = plan.Tier.ToString(),
            Interval = plan.Interval.ToString(),
            Price = ToResponse(plan.Price, symbol),
            IsFree = plan.IsFree,
            TrialDays = plan.TrialDays,
            AudienceType = plan.AudienceType.ToString(),
            CountryCode = plan.CountryCode,
            Entitlements = [.. plan.Entitlements.Select(ToResponse)],
            SortOrder = plan.SortOrder
        };
    }

    public static EntitlementResponse ToResponse(Entitlement entitlement)
    {
        ArgumentNullException.ThrowIfNull(entitlement);

        return new EntitlementResponse { Key = entitlement.Key, Value = entitlement.Value };
    }

    public static SubscriptionResponse ToResponse(Subscription subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        return new SubscriptionResponse
        {
            SubscriptionId = subscription.Id.Value,
            PlanCode = subscription.PlanCode,
            Tier = subscription.Tier.ToString(),
            Status = subscription.Status.ToString(),
            Interval = subscription.Interval.ToString(),
            GrantsAccess = subscription.GrantsAccess,
            Entitlements = [.. subscription.Entitlements.Select(ToResponse)],
            CurrentPeriodStart = subscription.CurrentPeriod.StartsAt,
            CurrentPeriodEnd = subscription.CurrentPeriod.EndsAt,
            TrialEndsAt = subscription.TrialEndsAt,
            GraceEndsAt = subscription.GraceEndsAt,
            CancelAtPeriodEnd = subscription.CancelAtPeriodEnd,
            RenewalCount = subscription.RenewalCount
        };
    }

    public static PromotionRedemptionResponse ToResponse(
        PromotionRedemption redemption, int? remainingUses = null, string? symbol = null)
    {
        ArgumentNullException.ThrowIfNull(redemption);

        return new PromotionRedemptionResponse
        {
            RedemptionId = redemption.Id.Value,
            Code = redemption.PromotionCode,
            Discount = ToResponse(redemption.Discount, symbol),
            ProductCode = redemption.ProductCode,
            RemainingUses = remainingUses,
            RedeemedAt = redemption.RedeemedAt
        };
    }

    public static CreditGrantResponse ToResponse(CreditGrant grant, string? symbol = null)
    {
        ArgumentNullException.ThrowIfNull(grant);

        return new CreditGrantResponse
        {
            GrantId = grant.Id.Value,
            WalletId = grant.WalletId.Value,
            Kind = grant.Kind.ToString(),
            Amount = ToResponse(grant.Amount, symbol),
            Reason = grant.Reason,
            ExpiresAt = grant.ExpiresAt,
            IsLive = grant.IsLive,
            GrantedAt = grant.GrantedAt
        };
    }

    public static DonationCauseResponse ToResponse(DonationCause cause, string? symbol = null)
    {
        ArgumentNullException.ThrowIfNull(cause);

        var acceptsMoney = cause.Accepts(ContributionKind.Monetary);

        return new DonationCauseResponse
        {
            CauseId = cause.Id.Value,
            Code = cause.Code,
            Title = cause.Title,
            Description = cause.Description,
            BeneficiaryType = cause.BeneficiaryType.ToString(),
            BeneficiaryId = cause.BeneficiaryId,
            OrganizerName = cause.OrganizerName,
            Urgency = cause.Urgency.ToString(),
            AcceptedKinds = SplitList(cause.AcceptedKinds),

            // A coat drive that takes no money reports no total. A zero would read as a failure.
            Raised = acceptsMoney ? ToResponse(cause.Raised, symbol) : null,
            Target = cause.Target is { } target ? ToResponse(target, symbol) : null,
            SupporterCount = cause.SupporterCount,
            IsVerified = cause.IsVerified,
            IsActive = cause.IsActive,
            CountryCode = cause.CountryCode,
            LocationLabel = cause.LocationLabel,
            ContactPhone = cause.ContactPhone,
            ContactWhatsapp = cause.ContactWhatsapp,
            MediaReference = cause.MediaReference,

            // The client's own mock carried a "posted 2 days ago" line and the response had no
            // timestamp of any kind, so the field was removed from the card rather than filled with
            // a plausible date. This is the field it was waiting for.
            CreatedAt = cause.CreatedAt
        };
    }

    public static DonationResponse ToResponse(
        Donation donation, PaymentIntent? payment = null, string? symbol = null)
    {
        ArgumentNullException.ThrowIfNull(donation);

        return new DonationResponse
        {
            DonationId = donation.Id.Value,

            // Absent rather than hidden. The aggregate stores nothing when anonymous.
            DonorUserId = donation.DonorUserId,
            IsAnonymous = donation.IsAnonymous,
            BeneficiaryType = donation.BeneficiaryType.ToString(),
            BeneficiaryId = donation.BeneficiaryId,
            CauseCode = donation.CauseCode,
            Amount = ToResponse(donation.Amount, symbol),
            Status = donation.Status.ToString(),
            Message = donation.Message,
            ReceiptReference = donation.ReceiptReference,
            CountryCode = donation.CountryCode,
            Payment = payment is null ? null : ToResponse(payment, symbol),
            CreatedAt = donation.CreatedAt,
            CompletedAt = donation.CompletedAt
        };
    }

    public static ContributionFundResponse ToResponse(ContributionFund fund, string? symbol = null)
    {
        ArgumentNullException.ThrowIfNull(fund);

        return new ContributionFundResponse
        {
            FundId = fund.Id.Value,
            Code = fund.Code,
            Name = fund.Name,
            Description = fund.Description,
            Urgency = fund.Urgency.ToString(),
            AcceptedKinds = SplitList(fund.AcceptedKinds),
            Raised = ToResponse(fund.Raised, symbol),
            Target = fund.Target is { } target ? ToResponse(target, symbol) : null,
            ContributorCount = fund.ContributorCount,
            IsActive = fund.IsActive,
            CountryCode = fund.CountryCode,
            MediaReference = fund.MediaReference,
            SortOrder = fund.SortOrder
        };
    }

    public static ContributionResponse ToResponse(
        CommunityContribution contribution, PaymentIntent? payment = null, string? symbol = null)
    {
        ArgumentNullException.ThrowIfNull(contribution);

        return new ContributionResponse
        {
            ContributionId = contribution.Id.Value,
            FundCode = contribution.FundCode,
            ContributorUserId = contribution.ContributorUserId,
            IsAnonymous = contribution.IsAnonymous,
            Kind = contribution.Kind.ToString(),
            Status = contribution.Status.ToString(),

            // Null for the four non-monetary kinds. Not a zero — see the response's own summary.
            Amount = contribution.Amount is { } amount ? ToResponse(amount, symbol) : null,
            PledgeQuantity = contribution.PledgeQuantity,
            PledgeUnit = contribution.PledgeUnit,
            PledgeDescription = contribution.PledgeDescription,
            Message = contribution.Message,
            ReceiptReference = contribution.ReceiptReference,
            CountryCode = contribution.CountryCode,
            Payment = payment is null ? null : ToResponse(payment, symbol),
            CreatedAt = contribution.CreatedAt,
            CompletedAt = contribution.CompletedAt
        };
    }

    public static FinancialPolicyResponse ToResponse(FinancialPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return new FinancialPolicyResponse
        {
            PolicyId = policy.Id.Value,
            PolicyKey = policy.PolicyKey,
            Kind = policy.Kind.ToString(),
            Scope = policy.Scope.ToString(),
            ScopeValue = policy.ScopeValue,
            Version = policy.Version,
            VersionLabel = policy.VersionLabel,
            Description = policy.Description,
            IsActive = policy.IsActive,
            IsSystem = policy.IsSystem,
            Parameters = policy.Parameters.ToDictionary(
                parameter => parameter.Key, parameter => parameter.Value, StringComparer.Ordinal),
            EffectiveFrom = policy.Effective.StartsAt,
            EffectiveTo = policy.Effective.EndsAt
        };
    }

    public static CurrencyResponse ToResponse(CurrencyDefinition currency)
    {
        ArgumentNullException.ThrowIfNull(currency);

        return new CurrencyResponse
        {
            Code = currency.Code,
            Name = currency.Name,
            Symbol = currency.Symbol,
            Exponent = currency.Exponent,
            IsActive = currency.IsActive
        };
    }

    public static RevenueSummaryResponse ToResponse(RevenueRecord record, string? symbol = null)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new RevenueSummaryResponse
        {
            Currency = record.Currency,
            Dimension = record.Dimension.ToString(),
            DimensionValue = record.DimensionValue,
            Gross = ToResponse(record.Gross, symbol),
            Commission = ToResponse(record.Commission, symbol),
            Fees = ToResponse(record.Fees, symbol),
            Refunds = ToResponse(record.Refunds, symbol),
            Net = ToResponse(record.Net, symbol),
            TransactionCount = record.TransactionCount,
            PeriodStart = record.PeriodStart,
            PeriodEnd = record.PeriodEnd
        };
    }

    private static IReadOnlyList<string> SplitList(string value) =>
        [.. value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
}
