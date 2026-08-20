using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Catalog;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Placements;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Domain.Revenue;
using Diaspora.Modules.Financial.Domain.Subscriptions;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Operations;

/// <summary>
/// Assembles the operator's overview.
/// <para>
/// <b>Every money figure is grouped by currency and none is totalled across them.</b> Producing a
/// single figure would need a rate, a rate has a date, and a number nobody can reproduce next week is
/// worse than two numbers side by side.
/// </para>
/// <para>
/// <b>The non-monetary contribution count sits beside the money on purpose.</b> Most giving on this
/// platform is not money, and a giving summary that counted only cash would understate what the
/// community actually did by a wide margin.
/// </para>
/// </summary>
public sealed class GetFinancialDashboardQueryHandler
    : IQueryHandler<GetFinancialDashboardQuery, FinancialDashboardResponse>
{
    private readonly IRevenueRecordRepository _revenue;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IPlacementRepository _placements;
    private readonly IMonetizationProductRepository _products;
    private readonly IFinancialPolicyRepository _policies;
    private readonly IContributionFundRepository _funds;
    private readonly ICommunityContributionRepository _contributions;
    private readonly IDonationCauseRepository _causes;
    private readonly CurrencyCatalog _currencies;
    private readonly IClock _clock;

    public GetFinancialDashboardQueryHandler(
        IRevenueRecordRepository revenue,
        ISubscriptionRepository subscriptions,
        IPlacementRepository placements,
        IMonetizationProductRepository products,
        IFinancialPolicyRepository policies,
        IContributionFundRepository funds,
        ICommunityContributionRepository contributions,
        IDonationCauseRepository causes,
        CurrencyCatalog currencies,
        IClock clock)
    {
        _revenue = revenue;
        _subscriptions = subscriptions;
        _placements = placements;
        _products = products;
        _policies = policies;
        _funds = funds;
        _contributions = contributions;
        _causes = causes;
        _currencies = currencies;
        _clock = clock;
    }

    public async Task<Result<FinancialDashboardResponse>> Handle(
        GetFinancialDashboardQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var now = _clock.UtcNow;
        var to = query.To ?? now;
        var from = query.From ?? to.AddDays(-30);

        var facts = await _currencies.AllAsync(cancellationToken);

        var revenue = await _revenue.ListAsync(
            from, to, RevenueDimension.Total, null, cancellationToken);

        var products = await _products.ListAvailableAsync(null, null, cancellationToken);
        var policies = await _policies.ListActiveAsync(cancellationToken);
        var funds = await _funds.ListActiveAsync(null, cancellationToken);
        var causes = await _causes.ListActiveAsync(null, cancellationToken);

        return Result.Success(new FinancialDashboardResponse
        {
            Revenue =
            [
                .. revenue.Select(record => FinancialMapper.ToResponse(
                    record,
                    facts.TryGetValue(record.Currency, out var currency) ? currency.Symbol : null))
            ],
            ActiveSubscriptionCount =
                await _subscriptions.CountByStatusAsync(SubscriptionStatus.Active, cancellationToken),
            TrialSubscriptionCount =
                await _subscriptions.CountByStatusAsync(SubscriptionStatus.Trial, cancellationToken),
            GracePeriodSubscriptionCount = await _subscriptions.CountByStatusAsync(
                SubscriptionStatus.GracePeriod, cancellationToken),

            ActivePlacementCount = await _placements.CountActiveAsync(now, cancellationToken),
            ActiveProductCount = products.Count,
            ActivePolicyCount = policies.Count,
            Giving = BuildGiving(
                funds,
                causes,
                facts,
                await _contributions.CountNonMonetaryAsync(from, to, cancellationToken)),
            PeriodStart = from,
            PeriodEnd = to,
            GeneratedAt = now
        });
    }

    /// <summary>
    /// Totals what has been given, per currency, from the funds' and causes' own running figures.
    /// <para>
    /// Those are maintained counters reconciled nightly, so this is a projection of a projection —
    /// correct for a dashboard and deliberately not the number anybody reconciles against. The ledger
    /// is where an exact answer lives.
    /// </para>
    /// </summary>
    private static List<GivingSummaryResponse> BuildGiving(
        IReadOnlyList<ContributionFund> funds,
        IReadOnlyList<DonationCause> causes,
        IReadOnlyDictionary<string, CurrencyCatalog.CurrencyFacts> facts,
        int nonMonetaryContributions)
    {
        var currencies = funds.Select(fund => fund.Currency)
            .Concat(causes.Select(cause => cause.Currency))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        var summaries = new List<GivingSummaryResponse>(currencies.Count);

        foreach (var currency in currencies)
        {
            var symbol = facts.TryGetValue(currency, out var money) ? money.Symbol : null;
            var exponent = money?.Exponent ?? Money.DefaultExponent;

            var causeTotal = causes
                .Where(cause => string.Equals(cause.Currency, currency, StringComparison.Ordinal))
                .Sum(cause => cause.RaisedMinorUnits);

            var causeSupporters = causes
                .Where(cause => string.Equals(cause.Currency, currency, StringComparison.Ordinal))
                .Sum(cause => cause.SupporterCount);

            var fundTotal = funds
                .Where(fund => string.Equals(fund.Currency, currency, StringComparison.Ordinal))
                .Sum(fund => fund.RaisedMinorUnits);

            var fundContributors = funds
                .Where(fund => string.Equals(fund.Currency, currency, StringComparison.Ordinal))
                .Sum(fund => fund.ContributorCount);

            summaries.Add(new GivingSummaryResponse
            {
                Currency = currency,
                DonationTotal = FinancialMapper.ToResponse(
                    Money.FromMinorUnits(causeTotal, currency, exponent), symbol),
                DonationCount = causeSupporters,
                ContributionTotal = FinancialMapper.ToResponse(
                    Money.FromMinorUnits(fundTotal, currency, exponent), symbol),
                MonetaryContributionCount = fundContributors,

                // Counted from the contributions themselves rather than derived from a fund's
                // running totals, which count every contributor and total only the money. The figure
                // is currency-independent — a pledge of six Saturdays is denominated in nothing — so
                // the same number appears against every currency, which is honest rather than
                // apportioned.
                NonMonetaryContributionCount = nonMonetaryContributions
            });
        }

        return summaries;
    }
}
