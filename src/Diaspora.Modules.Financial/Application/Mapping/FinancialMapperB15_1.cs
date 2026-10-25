using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.Modules.Financial.Domain.Rewards;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Domain.Withdrawals;

namespace Diaspora.Modules.Financial.Application.Mapping;

/// <summary>
/// The B15.1 half of the module's one translation point.
/// <para>
/// A partial file rather than a second mapper would have been tidier still, but
/// <c>FinancialMapper</c> is a static class in a file the frozen phase owns and this is additive —
/// separate file, same rule: an aggregate never leaves this module, and there is exactly one place
/// that decides what its contract looks like.
/// </para>
/// </summary>
public static class FinancialMapperExtensions
{
    public static WithdrawalResponse ToResponse(Withdrawal withdrawal, string? symbol = null)
    {
        ArgumentNullException.ThrowIfNull(withdrawal);

        return new WithdrawalResponse
        {
            WithdrawalId = withdrawal.Id.Value,
            Status = withdrawal.Status.ToString(),
            Gross = FinancialMapper.ToResponse(withdrawal.Gross, symbol),
            Fee = FinancialMapper.ToResponse(withdrawal.Fee, symbol),
            Net = FinancialMapper.ToResponse(withdrawal.Net, symbol),
            DestinationId = withdrawal.DestinationId.Value,
            DestinationLabel = withdrawal.DestinationLabel,
            IsCancellable = withdrawal.Status == Domain.Enums.WithdrawalStatus.Requested,
            FailureCode = withdrawal.FailureCode,
            FailureMessage = withdrawal.FailureMessage,
            RequestedAt = withdrawal.RequestedAt,
            ProcessingStartedAt = withdrawal.ProcessingStartedAt,
            CompletedAt = withdrawal.CompletedAt
        };
    }

    public static PayoutDestinationResponse ToResponse(PayoutDestination destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        return new PayoutDestinationResponse
        {
            DestinationId = destination.Id.Value,
            Kind = destination.Kind.ToString(),

            // The label and nothing else. There is no credential on the aggregate to leak.
            Label = destination.Label,
            Currency = destination.Currency,
            IsVerified = destination.IsVerified,
            IsUsable = destination.IsUsable,
            CountryCode = destination.CountryCode,
            CreatedAt = destination.CreatedAt,
            VerifiedAt = destination.VerifiedAt
        };
    }

    public static RefundRequestResponse ToResponse(RefundRequest request, string? symbol = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new RefundRequestResponse
        {
            RefundRequestId = request.Id.Value,
            PaymentIntentId = request.PaymentIntentId.Value,
            Status = request.Status.ToString(),
            Amount = FinancialMapper.ToResponse(request.Amount, symbol),
            Reason = request.Reason.ToString(),
            Note = request.Note,
            DecisionNote = request.DecisionNote,
            RefundId = request.RefundId,
            RequestedAt = request.RequestedAt,
            DecidedAt = request.DecidedAt
        };
    }

    public static RewardBalanceResponse ToResponse(RewardAccount? account, DateTimeOffset resolvedAt) =>
        new()
        {
            Balance = account?.Balance ?? 0,
            PendingPoints = account?.PendingBalance ?? 0,
            NextVestsAt = account?.NextVestsAt,
            LifetimeEarned = account?.LifetimeEarned ?? 0,
            LifetimeConverted = account?.LifetimeConverted ?? 0,
            LifetimeSpent = account?.LifetimeSpent ?? 0,

            // An account that does not exist yet can convert nothing, which is true rather than a
            // refusal — and says nothing about why, like every other conversion block.
            CanConvert = account is { IsConversionBlocked: false, Balance: > 0 },
            ResolvedAt = resolvedAt
        };

    /// <summary>The staff view of a hold. An account that does not exist yet is simply not held.</summary>
    public static RewardConversionHoldResponse ToHoldResponse(Guid userId, RewardAccount? account) =>
        new()
        {
            UserId = userId,
            IsHeld = account is { IsConversionBlocked: true },
            Reason = account?.BlockReason,
            Decisions = account is null
                ? []
                :
                [
                    .. account.HoldDecisions
                        .OrderByDescending(decision => decision.DecidedAt)
                        .ThenByDescending(decision => decision.DecisionId)
                        .Select(decision => new RewardHoldDecisionResponse
                        {
                            IsHeld = decision.IsHeld,
                            Reason = decision.Reason,
                            DecidedByUserId = decision.DecidedByUserId,
                            DecidedAt = decision.DecidedAt
                        })
                ]
        };

    public static RewardEntryResponse ToResponse(RewardEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new RewardEntryResponse
        {
            EntryId = entry.EntryId,
            Points = entry.Points,
            IsConversion = entry.IsConversion,
            IsSpend = entry.IsSpend,
            Source = entry.Source,
            OccurredAt = entry.OccurredAt
        };
    }

    public static RewardConversionResponse ToResponse(
        RewardConversion conversion, string? symbol = null)
    {
        ArgumentNullException.ThrowIfNull(conversion);

        return new RewardConversionResponse
        {
            ConversionId = conversion.Id.Value,
            Points = conversion.Points,
            Credited = FinancialMapper.ToResponse(conversion.Credited, symbol),
            MinorUnitsPerPoint = conversion.RateMinorUnitsPerPoint,
            PolicyVersion = conversion.PolicyVersion,
            TransactionId = conversion.TransactionId?.Value,
            ConvertedAt = conversion.ConvertedAt,
            Status = conversion.IsHolding ? "Holding" : "Available",
            AvailableAt = conversion.AvailableAt
        };
    }

    /// <summary>
    /// Resolved limits, with what is left of the member's daily allowance filled in.
    /// <para>
    /// <c>usedToday</c> is passed in rather than read here, because the mapper does not query and the
    /// figure is a sum over rows only the handler has.
    /// </para>
    /// </summary>
    public static MovementLimitsResponse ToResponse(
        MovementLimits limits,
        bool isEnabled,
        string? disabledReason,
        long usedTodayMinorUnits,
        Money? available,
        string? symbol = null)
    {
        ArgumentNullException.ThrowIfNull(limits);

        var remaining = limits.DailyMaximum is { } daily
            ? Money.FromMinorUnits(
                Math.Max(0, daily.MinorUnits - usedTodayMinorUnits),
                limits.Currency,
                limits.Exponent)
            : null;

        return new MovementLimitsResponse
        {
            IsEnabled = isEnabled,
            DisabledReason = disabledReason,
            Currency = limits.Currency,
            Minimum = Map(limits.Minimum, symbol),
            Maximum = Map(limits.Maximum, symbol),
            DailyMaximum = Map(limits.DailyMaximum, symbol),
            DailyRemaining = Map(remaining, symbol),
            FlatFee = Map(limits.FlatFee, symbol),
            FeePercentage = limits.FeePercentage,
            Available = Map(available, symbol)
        };
    }

    private static MoneyResponse? Map(Money? money, string? symbol) =>
        money is null ? null : FinancialMapper.ToResponse(money, symbol);
}
