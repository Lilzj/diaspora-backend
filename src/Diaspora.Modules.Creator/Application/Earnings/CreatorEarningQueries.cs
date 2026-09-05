using Diaspora.Application.Abstractions;
using Diaspora.Modules.Creator.Application.Services;
using Diaspora.Modules.Creator.Contracts.V1.Responses;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Modules.Creator.Options;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Creator.Application.Earnings;

/// <summary>The caller's own earnings, newest first — CREATOR-GAP-008.</summary>
[Authorize(Permission = CreatorPermissions.ProfileRead)]
public sealed record GetMyEarningsQuery : IQuery<IReadOnlyList<CreatorEarningResponse>>
{
    /// <summary>A <c>CreatorEarningState</c> name. Absent means every state.</summary>
    public string? State { get; init; }

    /// <summary>A <c>CreatorEarningSource</c> name. Absent means every source.</summary>
    public string? Source { get; init; }

    public int? Take { get; init; }
}

/// <summary>What the caller has earned, totalled — CREATOR-GAP-008.</summary>
[Authorize(Permission = CreatorPermissions.ProfileRead)]
public sealed record GetMyRevenueSummaryQuery : IQuery<CreatorRevenueSummaryResponse>
{
    /// <summary>Days to look back over. Absent means the whole history.</summary>
    public int? Days { get; init; }
}

// ════════════════════════════════════════════════════════════════════════════════════════════════

public sealed class GetMyEarningsQueryHandler
    : IQueryHandler<GetMyEarningsQuery, IReadOnlyList<CreatorEarningResponse>>
{
    private readonly CreatorContext _context;
    private readonly ICreatorEarningRepository _earnings;

    public GetMyEarningsQueryHandler(CreatorContext context, ICreatorEarningRepository earnings)
    {
        _context = context;
        _earnings = earnings;
    }

    public async Task<Result<IReadOnlyList<CreatorEarningResponse>>> Handle(
        GetMyEarningsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var creator = await _context.RequireCreatorAsync(cancellationToken);

        if (creator.IsFailure)
        {
            return Result.Failure<IReadOnlyList<CreatorEarningResponse>>(creator.Error);
        }

        // An unparseable filter narrows to nothing rather than being ignored. Silently returning
        // everything for `?state=Availabel` would show a creator their whole ledger under a heading
        // that says otherwise.
        if (!TryParseFilter<CreatorEarningState>(query.State, out var state)
            || !TryParseFilter<CreatorEarningSource>(query.Source, out var source))
        {
            return Result.Failure<IReadOnlyList<CreatorEarningResponse>>(
                CreatorEarningQueryErrors.FilterUnknown);
        }

        var earnings = await _earnings.ListForCreatorAsync(
            creator.Value.Id, state, source, query.Take ?? 50, cancellationToken);

        return Result.Success<IReadOnlyList<CreatorEarningResponse>>(
            [.. earnings.Select(CreatorEarningMapper.ToResponse)]);
    }

    private static bool TryParseFilter<TEnum>(string? value, out TEnum? parsed)
        where TEnum : struct, Enum
    {
        parsed = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (Enum.TryParse<TEnum>(value.Trim(), ignoreCase: true, out var result)
            && Enum.IsDefined(result))
        {
            parsed = result;
            return true;
        }

        return false;
    }
}

/// <summary>
/// Lifetime, available, pending and the per-source breakdown.
/// <para>
/// <b>Every total is summed by the database</b>, not by loading the ledger and adding it up. A
/// creator's history only grows, and a summary that walks it gets slower every month somebody earns
/// anything.
/// </para>
/// <para>
/// <b>One currency, or the summary says it cannot answer.</b> A creator paid in two currencies has
/// two totals and no single number; adding them would produce a figure that is not money in any
/// currency. Rather than pick one and be quietly wrong, the response carries the currency it
/// summed and a flag saying whether anything was left out — §29's rule against inventing an
/// authoritative figure applies to arithmetic as much as to metrics.
/// </para>
/// </summary>
public sealed class GetMyRevenueSummaryQueryHandler
    : IQueryHandler<GetMyRevenueSummaryQuery, CreatorRevenueSummaryResponse>
{
    private readonly CreatorContext _context;
    private readonly ICreatorEarningRepository _earnings;
    private readonly CreatorRevenueShareOptions _shares;

    public GetMyRevenueSummaryQueryHandler(
        CreatorContext context,
        ICreatorEarningRepository earnings,
        IOptions<CreatorRevenueShareOptions> shares)
    {
        ArgumentNullException.ThrowIfNull(shares);

        _context = context;
        _earnings = earnings;
        _shares = shares.Value;
    }

    public async Task<Result<CreatorRevenueSummaryResponse>> Handle(
        GetMyRevenueSummaryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var creator = await _context.RequireCreatorAsync(cancellationToken);

        if (creator.IsFailure)
        {
            return Result.Failure<CreatorRevenueSummaryResponse>(creator.Error);
        }

        DateTimeOffset? since = query.Days is { } days and > 0
            ? DateTimeOffset.UtcNow.AddDays(-days)
            : null;

        var byState = await _earnings.SumByStateAsync(creator.Value.Id, since, cancellationToken);
        var bySource = await _earnings.SumBySourceAsync(creator.Value.Id, since, cancellationToken);

        // The currency with the most rows behind it. Deterministic, and the flag below says whether
        // choosing was necessary at all.
        var currency = byState
            .GroupBy(row => row.Currency)
            .OrderByDescending(group => group.Sum(row => row.Count))
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.Key)
            .FirstOrDefault() ?? "GBP";

        var currencyCount = byState.Select(row => row.Currency).Distinct().Count();

        long TotalFor(CreatorEarningState state) => byState
            .Where(row => row.State == state && row.Currency == currency)
            .Sum(row => row.MinorUnits);

        return Result.Success(new CreatorRevenueSummaryResponse
        {
            Currency = currency,
            Exponent = 2,

            // Lifetime is what was earned, not what survived. Reversed and refunded earnings are
            // excluded, because a creator reading "£400 earned" beside "£40 available" would
            // otherwise be looking at £360 that was taken back.
            LifetimeMinorUnits =
                TotalFor(CreatorEarningState.Pending)
                + TotalFor(CreatorEarningState.Available)
                + TotalFor(CreatorEarningState.Processing)
                + TotalFor(CreatorEarningState.Paid),

            PendingMinorUnits = TotalFor(CreatorEarningState.Pending),
            AvailableMinorUnits = TotalFor(CreatorEarningState.Available),
            ProcessingMinorUnits = TotalFor(CreatorEarningState.Processing),
            PaidMinorUnits = TotalFor(CreatorEarningState.Paid),

            ReversedMinorUnits =
                TotalFor(CreatorEarningState.Reversed)
                + TotalFor(CreatorEarningState.Refunded)
                + TotalFor(CreatorEarningState.Cancelled),

            ClearingDays = _shares.ClearingDays,

            // Says so rather than hiding it. A creator paid in two currencies is shown the totals
            // for one and told the other exists.
            HasOtherCurrencies = currencyCount > 1,

            BySource =
            [
                .. bySource
                    .Where(row => row.Currency == currency)
                    .OrderByDescending(row => row.MinorUnits)
                    .Select(row => new CreatorRevenueSourceResponse
                    {
                        Source = row.Source.ToString(),
                        MinorUnits = row.MinorUnits,
                        Count = row.Count,
                    })
            ],
        });
    }
}

public static class CreatorEarningQueryErrors
{
    public static readonly Error FilterUnknown = Error.Validation(
        "Creator.Earning.FilterUnknown",
        "That is not a state or a source this platform records.");
}
