using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Invoices;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Invoices;

/// <summary>
/// Reads invoices. Owner only: an invoice somebody else paid is reported as not existing, so an
/// identifier cannot be used to learn whether a stranger bought something.
/// </summary>
public sealed class InvoiceQueryHandlers
    : IQueryHandler<GetMyInvoicesQuery, IReadOnlyList<InvoiceResponse>>,
        IQueryHandler<GetInvoiceQuery, InvoiceResponse>,
        IQueryHandler<GetInvoiceDocumentQuery, string>
{
    private readonly IInvoiceRepository _invoices;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;

    public InvoiceQueryHandlers(
        IInvoiceRepository invoices,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies)
    {
        _invoices = invoices;
        _authorization = authorization;
        _currencies = currencies;
    }

    public async Task<Result<IReadOnlyList<InvoiceResponse>>> Handle(
        GetMyInvoicesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<InvoiceResponse>>(caller.Error);
        }

        int? beforeYear = null;
        long? beforeSequence = null;

        if (!string.IsNullOrWhiteSpace(query.Before))
        {
            if (!Invoice.TryParseNumber(query.Before, out var year, out var sequence))
            {
                return Result.Failure<IReadOnlyList<InvoiceResponse>>(Error.Validation(
                    FinancialErrorCodes.InvoiceCursorInvalid,
                    "'before' must be an invoice number such as DSP-2026-000123."));
            }

            beforeYear = year;
            beforeSequence = sequence;
        }

        var invoices = await _invoices.ListByOwnerAsync(
            caller.Value, beforeYear, beforeSequence, query.Take, cancellationToken);

        var payments = await _invoices.ListPaymentsAsync(
            [.. invoices.Select(invoice => invoice.PaymentIntentId)], cancellationToken);

        var facts = await _currencies.AllAsync(cancellationToken);

        return Result.Success<IReadOnlyList<InvoiceResponse>>(
        [
            .. invoices.Select(invoice => InvoiceView.ToResponse(
                invoice,
                payments.TryGetValue(invoice.PaymentIntentId, out var payment) ? payment : null,
                facts.TryGetValue(invoice.Currency, out var currency) ? currency.Symbol : null))
        ]);
    }

    public async Task<Result<InvoiceResponse>> Handle(
        GetInvoiceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await ReadOwnAsync(query.InvoiceId, cancellationToken);
    }

    public async Task<Result<string>> Handle(
        GetInvoiceDocumentQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var invoice = await ReadOwnAsync(query.InvoiceId, cancellationToken);

        return invoice.IsFailure
            ? Result.Failure<string>(invoice.Error)
            : Result.Success(InvoiceDocument.Render(invoice.Value));
    }

    private async Task<Result<InvoiceResponse>> ReadOwnAsync(
        Guid invoiceId, CancellationToken cancellationToken)
    {
        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<InvoiceResponse>(caller.Error);
        }

        var invoice = await _invoices.FindAsync(InvoiceId.From(invoiceId), cancellationToken);

        // Deliberately no staff override: an invoice is the member's document, and support reads the
        // payment it was issued for through the payment routes, which already have one.
        if (invoice is null || invoice.OwnerUserId != caller.Value)
        {
            return Result.Failure<InvoiceResponse>(Error.NotFound(
                FinancialErrorCodes.InvoiceNotFound, "That invoice does not exist."));
        }

        var payments = await _invoices.ListPaymentsAsync([invoice.PaymentIntentId], cancellationToken);
        var symbol = await _currencies.SymbolAsync(invoice.Currency, cancellationToken);

        return Result.Success(InvoiceView.ToResponse(
            invoice,
            payments.TryGetValue(invoice.PaymentIntentId, out var payment) ? payment : null,
            symbol));
    }
}
