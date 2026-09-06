using Diaspora.Application.Abstractions;
using Diaspora.Modules.Creator.Application.Services;
using Diaspora.Modules.Creator.Contracts.V1.Responses;
using Diaspora.Modules.Creator.Domain.Earnings;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Modules.Creator.Options;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Creator.Application.Earnings;

/// <summary>The caller's own earnings, newest first — CREATOR-GAP-008.</summary>
[Authorize(Permission = CreatorPermissions.ProfileRead)]
public sealed record GetMyEarningsQuery : IQuery<CreatorEarningPageResponse>
{
    /// <summary>A <c>CreatorEarningState</c> name. Absent means every state.</summary>
    public string? State { get; init; }

    /// <summary>A <c>CreatorEarningSource</c> name. Absent means every source.</summary>
    public string? Source { get; init; }

    public int? Take { get; init; }

    /// <summary>
    /// A <c>NextCursor</c> from the previous page. Absent starts at the newest.
    /// <para>
    /// <b>This route pages rather than capping.</b> A list that answers the most recent fifty and
    /// says nothing is a ledger with rows missing that a creator has no way to notice — the same
    /// shape as a filter that can only come back empty, and worse, because the rows it hides are
    /// money.
    /// </para>
    /// </summary>
    public string? Cursor { get; init; }
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
    : IQueryHandler<GetMyEarningsQuery, CreatorEarningPageResponse>
{
    private const int DefaultTake = 25;

    private readonly CreatorContext _context;
    private readonly ICreatorEarningRepository _earnings;
    private readonly CreatorRevenueShareOptions _shares;

    public GetMyEarningsQueryHandler(
        CreatorContext context,
        ICreatorEarningRepository earnings,
        IOptions<CreatorRevenueShareOptions> shares)
    {
        ArgumentNullException.ThrowIfNull(shares);

        _context = context;
        _earnings = earnings;
        _shares = shares.Value;
    }

    public async Task<Result<CreatorEarningPageResponse>> Handle(
        GetMyEarningsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var creator = await _context.RequireCreatorAsync(cancellationToken);

        if (creator.IsFailure)
        {
            return Result.Failure<CreatorEarningPageResponse>(creator.Error);
        }

        // An unparseable filter narrows to nothing rather than being ignored. Silently returning
        // everything for `?state=Availabel` would show a creator their whole ledger under a heading
        // that says otherwise.
        if (!TryParseFilter<CreatorEarningState>(query.State, out var state)
            || !TryParseFilter<CreatorEarningSource>(query.Source, out var source))
        {
            return Result.Failure<CreatorEarningPageResponse>(
                CreatorEarningQueryErrors.FilterUnknown);
        }

        // Refused, not ignored. Ignoring a bad cursor serves page one again, so a "load more"
        // button would reload the rows already on screen and look to a creator like a ledger that
        // ends where it does not.
        CreatorEarningCursor? after = null;

        if (!string.IsNullOrWhiteSpace(query.Cursor))
        {
            if (!CreatorEarningCursor.TryParse(query.Cursor, out var parsed))
            {
                return Result.Failure<CreatorEarningPageResponse>(
                    CreatorEarningQueryErrors.CursorInvalid);
            }

            after = parsed;
        }

        var take = Math.Clamp(query.Take ?? DefaultTake, 1, 100);

        // One extra row, so `hasMore` is known without a second count query.
        var rows = await _earnings.ListForCreatorAsync(
            creator.Value.Id, state, source, after, take + 1, cancellationToken);

        var hasMore = rows.Count > take;
        var page = hasMore ? rows.Take(take).ToList() : rows;

        return Result.Success(new CreatorEarningPageResponse
        {
            Items = [.. page.Select(earning =>
                CreatorEarningMapper.ToResponse(earning, _shares.ClearingDays))],

            NextCursor = hasMore
                ? new CreatorEarningCursor(page[^1].OccurredAt, page[^1].Id.Value).ToString()
                : null,

            HasMore = hasMore,
        });
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

        // ── What the window applies to, and what it must not ─────────────────────────
        //
        // **The balances are all-time.** Pending, available, processing and paid answer "what do I
        // hold", and holdings do not have a date range — an available balance that shrank when a
        // creator asked about the last thirty days would be them watching money disappear from a
        // filter. Only the period figure and the breakdown are windowed, because those answer "what
        // did this window earn me".
        //
        // Two queries rather than one for exactly that reason, and both are database sums.
        var allTimeByState = await _earnings.SumByStateAsync(
            creator.Value.Id, since: null, cancellationToken);

        var periodByState = since is null
            ? allTimeByState
            : await _earnings.SumByStateAsync(creator.Value.Id, since, cancellationToken);

        var bySource = await _earnings.SumBySourceAsync(creator.Value.Id, since, cancellationToken);

        // The currency with the most rows behind it. Deterministic, and the flag below says whether
        // choosing was necessary at all.
        var currency = allTimeByState
            .GroupBy(row => row.Currency)
            .OrderByDescending(group => group.Sum(row => row.Count))
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.Key)
            .FirstOrDefault() ?? "GBP";

        var currencyCount = allTimeByState.Select(row => row.Currency).Distinct().Count();

        long TotalFor(CreatorEarningState state) => allTimeByState
            .Where(row => row.State == state && row.Currency == currency)
            .Sum(row => row.MinorUnits);

        long PeriodTotalFor(CreatorEarningState state) => periodByState
            .Where(row => row.State == state && row.Currency == currency)
            .Sum(row => row.MinorUnits);

        // Everything earned, ever, and not taken back.
        var lifetimeTotal =
            TotalFor(CreatorEarningState.Pending)
            + TotalFor(CreatorEarningState.Available)
            + TotalFor(CreatorEarningState.Processing)
            + TotalFor(CreatorEarningState.Paid);

        // The same sum over the window, and the denominator for every share below — so a breakdown
        // adds to a hundred rather than to whatever the states happen to sum.
        var periodTotal =
            PeriodTotalFor(CreatorEarningState.Pending)
            + PeriodTotalFor(CreatorEarningState.Available)
            + PeriodTotalFor(CreatorEarningState.Processing)
            + PeriodTotalFor(CreatorEarningState.Paid);

        return Result.Success(new CreatorRevenueSummaryResponse
        {
            Currency = currency,
            Exponent = 2,

            // Lifetime is what was earned, not what survived. Reversed and refunded earnings are
            // excluded, because a creator reading "£400 earned" beside "£40 available" would
            // otherwise be looking at £360 that was taken back.
            LifetimeMinorUnits = lifetimeTotal,

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

            // The window this summary covers. With no `days` the window is everything, and the
            // period figure is the lifetime figure rather than a second number that looks different
            // for no reason a creator could name.
            CurrentPeriodMinorUnits = periodTotal,
            CurrentPeriodLabel = since is null ? "All time" : $"The last {query.Days} days",

            GeneratedAt = DateTimeOffset.UtcNow,

            BySource =
            [
                .. bySource
                    .Where(row => row.Currency == currency)
                    .OrderByDescending(row => row.MinorUnits)
                    .Select(row => new CreatorRevenueSourceResponse
                    {
                        Source = row.Source.ToString(),
                        Label = CreatorEarningMapper.LabelFor(row.Source),
                        MinorUnits = row.MinorUnits,
                        Count = row.Count,

                        // Divided here rather than on the device. A zero total divides to zero
                        // rather than throwing, and rounds to one decimal so four sources do not
                        // display as 33.3 three times and leave a member hunting the missing tenth.
                        SharePercentage = periodTotal <= 0
                            ? 0m
                            : Math.Round(row.MinorUnits * 100m / periodTotal, 1),
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

    public static readonly Error CursorInvalid = Error.Validation(
        "Creator.Earning.CursorInvalid",
        "That is not a page marker this platform issued.");
}
