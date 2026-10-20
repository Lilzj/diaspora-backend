using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Invoices;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.Modules.Financial.Domain.Placements;
using Diaspora.Modules.Financial.Domain.Purchases;
using Diaspora.Modules.Financial.Domain.Subscriptions;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Financial.Application.Invoices;

/// <summary>
/// Issues the caller's outstanding invoices, oldest capture first, one save per invoice.
/// <para>
/// <b>Where the lines come from.</b> A payment that paid for a catalogue purchase is described by
/// the purchase — its list price, its discount and the promotion code that produced it — but only
/// when the purchase's paid amount is exactly what was captured; a partial capture is described as
/// what was captured and nothing else, because lines that do not add up to the total are worse than
/// one honest line. A subscription charge is described by the payment, with the subscription's
/// current period. Anything else is one line in the payment's own words.
/// </para>
/// <para>
/// <b>The number race is settled at the database.</b> The next sequence is read as one more than the
/// highest in the year, and (year, sequence) is uniquely indexed. A concurrent issuer that took the
/// number first makes this save fail; the refused row is forgotten, and either the payment turns out
/// to have been invoiced by the winner (done) or the next number is read again.
/// </para>
/// </summary>
public sealed class IssueMyInvoicesCommandHandler : ICommandHandler<IssueMyInvoicesCommand, int>
{
    /// <summary>How many outstanding payments one call will number. The rest wait for the next read.</summary>
    private const int BatchSize = 50;

    private const int MaxAttempts = 5;

    private readonly IInvoiceRepository _invoices;
    private readonly IMonetizationPurchaseRepository _purchases;
    private readonly IPlacementRepository _placements;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly FinancialAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<IssueMyInvoicesCommandHandler> _logger;

    public IssueMyInvoicesCommandHandler(
        IInvoiceRepository invoices,
        IMonetizationPurchaseRepository purchases,
        IPlacementRepository placements,
        ISubscriptionRepository subscriptions,
        FinancialAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork,
        ILogger<IssueMyInvoicesCommandHandler> logger)
    {
        _invoices = invoices;
        _purchases = purchases;
        _placements = placements;
        _subscriptions = subscriptions;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<int>> Handle(
        IssueMyInvoicesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<int>(caller.Error);
        }

        var outstanding = await _invoices.ListUninvoicedPaymentsAsync(
            caller.Value, BatchSize, cancellationToken);

        var issued = 0;

        foreach (var payment in outstanding)
        {
            if (await IssueAsync(payment, cancellationToken))
            {
                issued++;
            }
        }

        return Result.Success(issued);
    }

    private async Task<bool> IssueAsync(PaymentIntent payment, CancellationToken cancellationToken)
    {
        var draft = await DescribeAsync(payment, cancellationToken);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var now = _clock.UtcNow;
            var year = now.UtcDateTime.Year;
            var sequence = await _invoices.NextSequenceAsync(year, cancellationToken);

            var invoice = Invoice.Issue(
                InvoiceId.From(_guids.NewSequentialGuid()),
                year,
                sequence,
                payment.PayerUserId,
                payment.Id,
                draft.Kind,
                draft.SourceId,
                draft.Description,
                draft.ListPrice,
                draft.Discount,
                draft.Subject,
                draft.PromotionCode,
                draft.PeriodStart,
                draft.PeriodEnd,
                draft.PaidAt,
                now);

            _invoices.Add(invoice);

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return true;
            }
            catch (Exception exception) when (IsUniquenessConflict(exception))
            {
                // The refused row is still tracked as Added, and the next save would retry it.
                _invoices.Forget(invoice);

                if (await _invoices.FindByPaymentIntentAsync(payment.Id, cancellationToken) is not null)
                {
                    // Another request invoiced this payment first. Its number stands.
                    return false;
                }

                _logger.LogInformation(
                    "Invoice number {Number} was taken concurrently; retrying payment {Payment} (attempt {Attempt}).",
                    invoice.Number,
                    payment.Id.Value,
                    attempt);
            }
        }

        _logger.LogWarning(
            "Payment {Payment} could not be numbered after {Attempts} attempts; it will be retried on the next read.",
            payment.Id.Value,
            MaxAttempts);

        return false;
    }

    private async Task<InvoiceDraft> DescribeAsync(
        PaymentIntent payment, CancellationToken cancellationToken)
    {
        var captured = payment.Captured;
        var paidAt = payment.CapturedAt ?? payment.SettledAt ?? payment.CreatedAt;
        var zero = Money.Zero(captured.Currency, captured.Exponent);

        var purchase = await _purchases.FindByPaymentIntentAsync(payment.Id, cancellationToken);

        if (purchase is not null)
        {
            var (start, end) = await PlacementPeriodAsync(purchase, paidAt, cancellationToken);

            // The purchase's own breakdown only when it describes exactly what was taken.
            var whole = purchase.PaidMinorUnits == captured.MinorUnits
                && string.Equals(purchase.Currency, captured.Currency, StringComparison.Ordinal);

            return new InvoiceDraft(
                InvoiceSourceKind.Purchase,
                purchase.Id.Value,
                payment.Description,
                whole ? purchase.ListPrice : captured,
                whole ? purchase.Discount : zero,
                purchase.Subject,
                whole ? purchase.PromotionCode : null,
                start,
                end,
                paidAt);
        }

        var subscription = await _subscriptions.FindByLastPaymentIntentAsync(
            payment.Id, cancellationToken);

        if (subscription is not null)
        {
            var period = subscription.CurrentPeriod;

            return new InvoiceDraft(
                InvoiceSourceKind.Subscription,
                subscription.Id.Value,
                payment.Description,
                captured,
                zero,
                payment.Subject,
                null,
                period.StartsAt,
                period.EndsAt ?? period.StartsAt,
                paidAt);
        }

        return new InvoiceDraft(
            InvoiceSourceKind.Payment,
            null,
            payment.Description,
            captured,
            zero,
            payment.Subject,
            null,
            paidAt,
            paidAt,
            paidAt);
    }

    /// <summary>The window a placement purchase paid for, or the payment's own instant.</summary>
    private async Task<(DateTimeOffset Start, DateTimeOffset End)> PlacementPeriodAsync(
        MonetizationPurchase purchase, DateTimeOffset paidAt, CancellationToken cancellationToken)
    {
        if (purchase.PlacementId is not { } placementId)
        {
            return (paidAt, paidAt);
        }

        var placement = await _placements.FindAsync(placementId, cancellationToken);

        if (placement is null)
        {
            return (paidAt, paidAt);
        }

        // A renewal extended the window that exists, so its period is the days it added, ending
        // where the window now ends — not the placement's first day.
        if (purchase.RenewalDays is { } days && placement.Window.EndsAt is { } renewedEnd)
        {
            return (renewedEnd.AddDays(-days), renewedEnd);
        }

        return (placement.Window.StartsAt, placement.Window.EndsAt ?? placement.Window.StartsAt);
    }

    /// <summary>
    /// Whether the database refused the write because something was already there. Matched on the
    /// provider's text, as <c>MembershipPaymentBinder</c> does, because this module names no provider.
    /// </summary>
    private static bool IsUniquenessConflict(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            var message = current.Message;

            if (message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private sealed record InvoiceDraft(
        InvoiceSourceKind Kind,
        Guid? SourceId,
        string Description,
        Money ListPrice,
        Money Discount,
        SubjectReference? Subject,
        string? PromotionCode,
        DateTimeOffset PeriodStart,
        DateTimeOffset PeriodEnd,
        DateTimeOffset PaidAt);
}
