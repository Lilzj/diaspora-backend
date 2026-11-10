using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Operations;

/// <summary>
/// The currencies the platform knows how to format.
/// <para>
/// Served so a client has one formatter reading a symbol and an exponent, instead of the
/// two-currency table currently hard-coded in its job card.
/// </para>
/// </summary>
[RequirePermission(FinancialPermissions.WalletRead)]
public sealed record GetCurrenciesQuery : IQuery<IReadOnlyList<CurrencyResponse>>;
