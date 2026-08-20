using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Giving;

/// <summary>Reads the active causes.</summary>
public sealed class GetDonationCausesQueryHandler
    : IQueryHandler<GetDonationCausesQuery, IReadOnlyList<DonationCauseResponse>>
{
    private readonly IDonationCauseRepository _causes;
    private readonly CurrencyCatalog _currencies;

    public GetDonationCausesQueryHandler(
        IDonationCauseRepository causes, CurrencyCatalog currencies)
    {
        _causes = causes;
        _currencies = currencies;
    }

    public async Task<Result<IReadOnlyList<DonationCauseResponse>>> Handle(
        GetDonationCausesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var causes = await _causes.ListActiveAsync(query.CountryCode, cancellationToken);
        var facts = await _currencies.AllAsync(cancellationToken);

        var filtered = Enum.TryParse<ContributionKind>(query.SupportKind, true, out var kind)
            ? causes.Where(cause => cause.Accepts(kind)).ToList()
            : causes;

        return Result.Success<IReadOnlyList<DonationCauseResponse>>(
        [
            .. filtered.Select(cause => FinancialMapper.ToResponse(
                cause, facts.TryGetValue(cause.Currency, out var currency) ? currency.Symbol : null))
        ]);
    }
}
