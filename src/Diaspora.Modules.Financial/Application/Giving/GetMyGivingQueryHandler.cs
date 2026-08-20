using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Giving;

/// <summary>Reads the caller's donations and contributions.</summary>
public sealed class GetMyGivingQueryHandler
    : IQueryHandler<GetMyGivingQuery, GivingHistoryResponse>
{
    private readonly IDonationRepository _donations;
    private readonly ICommunityContributionRepository _contributions;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;

    public GetMyGivingQueryHandler(
        IDonationRepository donations,
        ICommunityContributionRepository contributions,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies)
    {
        _donations = donations;
        _contributions = contributions;
        _authorization = authorization;
        _currencies = currencies;
    }

    public async Task<Result<GivingHistoryResponse>> Handle(
        GetMyGivingQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<GivingHistoryResponse>(caller.Error);
        }

        var allDonations = await _donations.ListByDonorAsync(
            caller.Value, query.Take, cancellationToken);

        var allContributions = await _contributions.ListByContributorAsync(
            caller.Value, query.Take, cancellationToken);

        // Filtered here rather than in two repository signatures: giving history is two sequences
        // merged, so the boundary is applied once to both and the client de-duplicates the overlap
        // it has to handle anyway.
        var donations = query.Before is { } before
            ? allDonations.Where(donation => donation.CreatedAt <= before).ToList()
            : [.. allDonations];

        var contributions = query.Before is { } boundary
            ? allContributions.Where(contribution => contribution.CreatedAt <= boundary).ToList()
            : [.. allContributions];

        var facts = await _currencies.AllAsync(cancellationToken);

        return Result.Success(new GivingHistoryResponse
        {
            Donations =
            [
                .. donations.Select(donation => FinancialMapper.ToResponse(
                    donation,
                    symbol: facts.TryGetValue(donation.Currency, out var currency)
                        ? currency.Symbol
                        : null))
            ],
            Contributions =
            [
                .. contributions.Select(contribution => FinancialMapper.ToResponse(
                    contribution,
                    symbol: contribution.Currency is { } code
                        && facts.TryGetValue(code, out var money)
                            ? money.Symbol
                            : null))
            ],
            NonMonetaryContributionCount =
                contributions.Count(contribution => contribution.Kind != ContributionKind.Monetary)
        });
    }
}
