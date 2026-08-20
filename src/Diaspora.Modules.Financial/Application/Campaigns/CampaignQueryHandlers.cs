using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Campaigns;

/// <summary>
/// One campaign.
/// <para>
/// <b>A campaign nobody has published is visible only to the person who wrote it.</b> Answering 404
/// rather than 403 for everybody else is deliberate: a 403 confirms the identifier names something.
/// </para>
/// </summary>
public sealed class GetCampaignQueryHandler : IQueryHandler<GetCampaignQuery, CampaignResponse>
{
    private readonly ICampaignRepository _campaigns;
    private readonly IDonationCauseRepository _causes;
    private readonly CampaignAccessService _access;
    private readonly CurrencyCatalog _currencies;
    private readonly IClock _clock;

    public GetCampaignQueryHandler(
        ICampaignRepository campaigns,
        IDonationCauseRepository causes,
        CampaignAccessService access,
        CurrencyCatalog currencies,
        IClock clock)
    {
        _campaigns = campaigns;
        _causes = causes;
        _access = access;
        _currencies = currencies;
        _clock = clock;
    }

    public async Task<Result<CampaignResponse>> Handle(
        GetCampaignQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var campaign = await _campaigns.FindAsync(
            CampaignId.From(query.CampaignId), cancellationToken);

        if (campaign is null)
        {
            return Result.Failure<CampaignResponse>(Error.NotFound(
                FinancialErrorCodes.CampaignNotFound, "No campaign with that identifier."));
        }

        var caller = _access.CallerOrNull();
        var canManage = await _access.CanManageAsync(campaign, caller, cancellationToken);

        if (!campaign.IsPubliclyVisible && !canManage)
        {
            return Result.Failure<CampaignResponse>(Error.NotFound(
                FinancialErrorCodes.CampaignNotFound, "No campaign with that identifier."));
        }

        var cause = campaign.CauseId is { } causeId
            ? await _causes.FindAsync(causeId, cancellationToken)
            : null;

        return Result.Success(CampaignMapper.ToResponse(
            campaign,
            cause,
            canManage,
            _clock.UtcNow,
            await CampaignMapper.SymbolAsync(_currencies, campaign.Currency, cancellationToken)));
    }
}

/// <summary>
/// Campaigns a member can browse, cursor-paged.
/// <para>
/// <b>The cause figures are fetched for the page, not per campaign.</b> One read of the causes named
/// by the page rather than one read each — the difference is a single query against twenty.
/// </para>
/// </summary>
public sealed class SearchCampaignsQueryHandler
    : IQueryHandler<SearchCampaignsQuery, GivingPageResponse<CampaignResponse>>
{
    private readonly ICampaignRepository _campaigns;
    private readonly IDonationCauseRepository _causes;
    private readonly CampaignAccessService _access;
    private readonly CurrencyCatalog _currencies;
    private readonly IClock _clock;

    public SearchCampaignsQueryHandler(
        ICampaignRepository campaigns,
        IDonationCauseRepository causes,
        CampaignAccessService access,
        CurrencyCatalog currencies,
        IClock clock)
    {
        _campaigns = campaigns;
        _causes = causes;
        _access = access;
        _currencies = currencies;
        _clock = clock;
    }

    public async Task<Result<GivingPageResponse<CampaignResponse>>> Handle(
        SearchCampaignsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _access.CallerOrNull();
        var cursor = GivingCursor.Parse(query.Cursor);
        var take = Math.Clamp(query.Take ?? 20, 1, FinancialLimits.MaxLedgerPageSize);

        // One more than asked for: the extra row answers "is there another page" without a second
        // scan of the same predicate.
        var rows = await _campaigns.SearchAsync(
            new CampaignFilter
            {
                Status = CampaignMapper.ParseStatus(query.State),
                Category = string.IsNullOrWhiteSpace(query.Category)
                    ? null
                    : CampaignMapper.ParseCategory(query.Category),
                CountryCode = query.CountryCode,
                OrganizationId = query.OrganizationId,
                ViewerUserId = caller,
                OrganizedByViewerOnly = query.MineOnly,
                Before = cursor?.FilterBoundary,
                ExcludeIds = cursor?.ExcludedIds ?? [],
                Take = take + 1
            },
            cancellationToken);

        var hasMore = rows.Count > take;
        var page = hasMore ? rows.Take(take).ToList() : [.. rows];

        var causes = new Dictionary<string, DonationCause>(StringComparer.Ordinal);

        foreach (var campaign in page.Where(campaign => campaign.CauseId is not null))
        {
            var cause = await _causes.FindAsync(campaign.CauseId!, cancellationToken);

            if (cause is not null)
            {
                causes[cause.Code] = cause;
            }
        }

        var symbols = await _currencies.AllAsync(cancellationToken);
        var now = _clock.UtcNow;
        var items = new List<CampaignResponse>(page.Count);

        foreach (var campaign in page)
        {
            var cause = campaign.CauseCode is { } code && causes.TryGetValue(code, out var found)
                ? found
                : null;

            items.Add(CampaignMapper.ToResponse(
                campaign,
                cause,
                await _access.CanManageAsync(campaign, caller, cancellationToken),
                now,
                symbols.TryGetValue(campaign.Currency, out var facts) ? facts.Symbol : null));
        }

        return Result.Success(new GivingPageResponse<CampaignResponse>
        {
            Items = items,
            HasMore = hasMore,
            NextCursor = hasMore && page.Count > 0
                ? GivingCursor.Encode(
                    page, campaign => campaign.CreatedAt, campaign => campaign.Id.Value, cursor)
                : null
        });
    }
}
