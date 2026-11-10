using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Advertising;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Extensibility.Currency;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Advertising;

/// <summary>One campaign's delivery — MON-GAP-002.</summary>
[RequirePermission(FinancialPermissions.PurchasesRead)]
public sealed record GetAdCampaignPerformanceQuery : IQuery<AdPerformanceResponse>
{
    public required Guid CampaignId { get; init; }

    /// <summary>Days back from today, UTC. Defaults to 30, capped at 365.</summary>
    public int? Days { get; init; }
}

/// <summary>One placement's delivery — MON-GAP-002.</summary>
[RequirePermission(FinancialPermissions.PurchasesRead)]
public sealed record GetPlacementPerformanceQuery : IQuery<AdPerformanceResponse>
{
    public required Guid PlacementId { get; init; }

    public int? Days { get; init; }
}

/// <summary>The monetization dashboard's rollup across everything the caller runs.</summary>
[RequirePermission(FinancialPermissions.PurchasesRead)]
public sealed record GetMyAdPerformanceSummaryQuery : IQuery<AdPerformanceResponse>
{
    public int? Days { get; init; }
}

// ════════════════════════════════════════════════════════════════════════════════════════════════

public sealed class GetAdCampaignPerformanceQueryHandler
    : IQueryHandler<GetAdCampaignPerformanceQuery, AdPerformanceResponse>
{
    private readonly IAdCampaignRepository _campaigns;
    private readonly IAdPerformanceRepository _performance;
    private readonly FinancialAuthorizationService _authorization;
    private readonly IClock _clock;

    public GetAdCampaignPerformanceQueryHandler(
        IAdCampaignRepository campaigns,
        IAdPerformanceRepository performance,
        FinancialAuthorizationService authorization,
        IClock clock)
    {
        _campaigns = campaigns;
        _performance = performance;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<AdPerformanceResponse>> Handle(
        GetAdCampaignPerformanceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Loaded through the ownership check first. Performance is the most commercially sensitive
        // read on the campaign: it is what a competitor would want most.
        var loaded = await AdCampaignLoader.ForOwnerAsync(
            _campaigns, _authorization, query.CampaignId, cancellationToken);

        if (loaded.IsFailure)
        {
            return Result.Failure<AdPerformanceResponse>(loaded.Error);
        }

        var campaign = loaded.Value;
        var (from, to) = AdPerformanceWindow.Resolve(_clock.UtcNow, query.Days);

        var rows = await _performance.ListForCampaignAsync(
            campaign.Id, from, to, cancellationToken);

        return Result.Success(AdPerformanceMapper.ToResponse(rows, from, to, campaign, campaign.Currency));
    }
}

public sealed class GetPlacementPerformanceQueryHandler
    : IQueryHandler<GetPlacementPerformanceQuery, AdPerformanceResponse>
{
    private readonly IPlacementRepositoryAccessor _placements;
    private readonly IAdPerformanceRepository _performance;
    private readonly FinancialAuthorizationService _authorization;
    private readonly IMemberCurrency _memberCurrency;
    private readonly IClock _clock;

    public GetPlacementPerformanceQueryHandler(
        IPlacementRepositoryAccessor placements,
        IAdPerformanceRepository performance,
        FinancialAuthorizationService authorization,
        IMemberCurrency memberCurrency,
        IClock clock)
    {
        _placements = placements;
        _performance = performance;
        _authorization = authorization;
        _memberCurrency = memberCurrency;
        _clock = clock;
    }

    public async Task<Result<AdPerformanceResponse>> Handle(
        GetPlacementPerformanceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<AdPerformanceResponse>(caller.Error);
        }

        // A placement's numbers belong to whoever bought it. Checked here rather than trusted from
        // the route, and answered as absent rather than forbidden for the same reason a campaign is.
        var placement = await _placements.FindAsync(
            PlacementId.From(query.PlacementId), cancellationToken);

        if (placement is null || placement.PurchasedByUserId != caller.Value)
        {
            return Result.Failure<AdPerformanceResponse>(AdCampaignErrors.NotFound);
        }

        var (from, to) = AdPerformanceWindow.Resolve(_clock.UtcNow, query.Days);

        var rows = await _performance.ListForPlacementAsync(
            placement.Id, from, to, cancellationToken);

        // Only asked when nothing was served: rows carry their own currency otherwise.
        var fallback = rows.Count > 0
            ? rows[0].Currency
            : await _memberCurrency.ForMemberAsync(caller.Value, cancellationToken);

        return Result.Success(AdPerformanceMapper.ToResponse(rows, from, to, campaign: null, fallback));
    }
}

public sealed class GetMyAdPerformanceSummaryQueryHandler
    : IQueryHandler<GetMyAdPerformanceSummaryQuery, AdPerformanceResponse>
{
    private readonly IAdCampaignRepository _campaigns;
    private readonly IAdPerformanceRepository _performance;
    private readonly FinancialAuthorizationService _authorization;
    private readonly IMemberCurrency _memberCurrency;
    private readonly IClock _clock;

    public GetMyAdPerformanceSummaryQueryHandler(
        IAdCampaignRepository campaigns,
        IAdPerformanceRepository performance,
        FinancialAuthorizationService authorization,
        IMemberCurrency memberCurrency,
        IClock clock)
    {
        _campaigns = campaigns;
        _performance = performance;
        _authorization = authorization;
        _memberCurrency = memberCurrency;
        _clock = clock;
    }

    public async Task<Result<AdPerformanceResponse>> Handle(
        GetMyAdPerformanceSummaryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<AdPerformanceResponse>(caller.Error);
        }

        // Ownership is resolved once, by the campaign repository, and the performance read takes
        // the ids it produced. Answering "whose is this" in two places is how the two come to
        // disagree.
        var mine = await _campaigns.ListForOwnerAsync(
            caller.Value, statuses: null, take: 200, cancellationToken);

        var (from, to) = AdPerformanceWindow.Resolve(_clock.UtcNow, query.Days);

        var rows = await _performance.ListForCampaignsAsync(
            [.. mine.Select(campaign => campaign.Id)], from, to, cancellationToken);

        // Only asked when nothing was served: rows carry their own currency otherwise.
        var fallback = rows.Count > 0
            ? rows[0].Currency
            : await _memberCurrency.ForMemberAsync(caller.Value, cancellationToken);

        return Result.Success(AdPerformanceMapper.ToResponse(rows, from, to, campaign: null, fallback));
    }
}

/// <summary>
/// The reporting window.
/// <para>
/// Bounded rather than open, because an unbounded read over a counter table is a scan that gets
/// slower every day the platform runs. Thirty days is what the dashboard shows; a year is as far
/// back as anybody can ask in one call.
/// </para>
/// </summary>
internal static class AdPerformanceWindow
{
    public const int DefaultDays = 30;
    public const int MaximumDays = 365;

    public static (DateOnly From, DateOnly To) Resolve(DateTimeOffset now, int? days)
    {
        var span = Math.Clamp(days ?? DefaultDays, 1, MaximumDays);
        var to = DateOnly.FromDateTime(now.UtcDateTime);

        return (to.AddDays(-(span - 1)), to);
    }
}

/// <summary>
/// Reads a placement, for the one query that needs one.
/// <para>
/// A narrow seam rather than taking a dependency on the whole placement repository, so the
/// performance handler cannot quietly grow the ability to write one.
/// </para>
/// </summary>
public interface IPlacementRepositoryAccessor
{
    Task<Domain.Placements.Placement?> FindAsync(
        PlacementId placementId, CancellationToken cancellationToken = default);
}
