using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Transactions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Wallets;

/// <summary>Reads the caller's transaction history.</summary>
public sealed class GetTransactionsQueryHandler
    : IQueryHandler<GetTransactionsQuery, IReadOnlyList<TransactionResponse>>
{
    private readonly IFinancialTransactionRepository _transactions;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;

    public GetTransactionsQueryHandler(
        IFinancialTransactionRepository transactions,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies)
    {
        _transactions = transactions;
        _authorization = authorization;
        _currencies = currencies;
    }

    public async Task<Result<IReadOnlyList<TransactionResponse>>> Handle(
        GetTransactionsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<TransactionResponse>>(caller.Error);
        }

        var transactions = await _transactions.PageAsync(
            new TransactionQuery
            {
                UserId = caller.Value,
                TransactionType =
                    Enum.TryParse<TransactionType>(query.TransactionType, true, out var kind)
                        ? kind
                        : null,
                From = query.From,
                To = query.To,
                Before = query.Before,
                ExcludeIds = query.ExcludeIds,
                PageSize = query.PageSize
            },
            cancellationToken);

        var facts = await _currencies.AllAsync(cancellationToken);

        return Result.Success<IReadOnlyList<TransactionResponse>>(
        [
            .. transactions.Select(transaction => FinancialMapper.ToResponse(
                transaction,
                facts.TryGetValue(transaction.Currency, out var currency) ? currency.Symbol : null))
        ]);
    }
}
