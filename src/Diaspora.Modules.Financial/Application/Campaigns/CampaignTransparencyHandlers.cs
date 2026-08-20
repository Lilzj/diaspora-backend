using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Domain.Withdrawals;
using Diaspora.Modules.Financial.Options;
using Diaspora.Modules.Financial.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Financial.Application.Campaigns;

/// <summary>Publishes an update to a campaign's supporters. COMMUNITY-LIFE-GAP-007.</summary>
public sealed class PublishCampaignUpdateCommandHandler
    : ICommandHandler<PublishCampaignUpdateCommand, CampaignUpdateResponse>
{
    private readonly ICampaignRepository _campaigns;
    private readonly ICampaignUpdateRepository _updates;
    private readonly CampaignAccessService _access;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public PublishCampaignUpdateCommandHandler(
        ICampaignRepository campaigns,
        ICampaignUpdateRepository updates,
        CampaignAccessService access,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _campaigns = campaigns;
        _updates = updates;
        _access = access;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CampaignUpdateResponse>> Handle(
        PublishCampaignUpdateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CampaignUpdateResponse>(caller.Error);
        }

        if (!string.IsNullOrWhiteSpace(command.Request.IdempotencyKey)
            && await _updates.FindByIdempotencyKeyAsync(
                caller.Value, command.Request.IdempotencyKey, cancellationToken) is { } replayed)
        {
            return replayed.AuthorUserId == caller.Value
                ? Result.Success(CampaignMapper.ToResponse(replayed))
                : Result.Failure<CampaignUpdateResponse>(Error.Conflict(
                    FinancialErrorCodes.CampaignUpdateNotFound,
                    "That idempotency key belongs to another request."));
        }

        var campaign = await _campaigns.FindAsync(
            CampaignId.From(command.CampaignId), cancellationToken);

        if (campaign is null)
        {
            return Result.Failure<CampaignUpdateResponse>(Error.NotFound(
                FinancialErrorCodes.CampaignNotFound, "No campaign with that identifier."));
        }

        if (!await _access.CanManageAsync(campaign, caller.Value, cancellationToken))
        {
            return Result.Failure<CampaignUpdateResponse>(Error.Forbidden(
                FinancialErrorCodes.CampaignOrganizerForbidden,
                "That campaign is not yours to post to."));
        }

        var update = CampaignUpdate.Publish(
            CampaignUpdateId.From(_guids.NewSequentialGuid()),
            campaign,
            caller.Value,
            command.Request.Title,
            command.Request.Body,
            _clock.UtcNow,
            command.Request.MediaReferences,
            command.Request.IdempotencyKey);

        if (update.IsFailure)
        {
            return Result.Failure<CampaignUpdateResponse>(update.Error);
        }

        _updates.Add(update.Value);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(CampaignMapper.ToResponse(update.Value));
    }
}

/// <summary>
/// A campaign's timeline, newest first.
/// <para>
/// <b>A withdrawn update is absent for everybody except the person who wrote it.</b> The organizer
/// sees what they took down; a supporter sees what stands.
/// </para>
/// </summary>
public sealed class GetCampaignUpdatesQueryHandler
    : IQueryHandler<GetCampaignUpdatesQuery, GivingPageResponse<CampaignUpdateResponse>>
{
    private readonly ICampaignRepository _campaigns;
    private readonly ICampaignUpdateRepository _updates;
    private readonly CampaignAccessService _access;

    public GetCampaignUpdatesQueryHandler(
        ICampaignRepository campaigns,
        ICampaignUpdateRepository updates,
        CampaignAccessService access)
    {
        _campaigns = campaigns;
        _updates = updates;
        _access = access;
    }

    public async Task<Result<GivingPageResponse<CampaignUpdateResponse>>> Handle(
        GetCampaignUpdatesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var campaign = await _campaigns.FindAsync(
            CampaignId.From(query.CampaignId), cancellationToken);

        if (campaign is null)
        {
            return Result.Failure<GivingPageResponse<CampaignUpdateResponse>>(Error.NotFound(
                FinancialErrorCodes.CampaignNotFound, "No campaign with that identifier."));
        }

        var caller = _access.CallerOrNull();
        var canManage = await _access.CanManageAsync(campaign, caller, cancellationToken);

        if (!campaign.IsPubliclyVisible && !canManage)
        {
            return Result.Failure<GivingPageResponse<CampaignUpdateResponse>>(Error.NotFound(
                FinancialErrorCodes.CampaignNotFound, "No campaign with that identifier."));
        }

        var cursor = GivingCursor.Parse(query.Cursor);
        var take = Math.Clamp(query.Take ?? 20, 1, FinancialLimits.MaxLedgerPageSize);

        var rows = await _updates.ListByCampaignAsync(
            campaign.Id,
            canManage,
            cursor?.FilterBoundary,
            cursor?.ExcludedIds ?? [],
            take + 1,
            cancellationToken);

        var hasMore = rows.Count > take;
        var page = hasMore ? rows.Take(take).ToList() : [.. rows];

        return Result.Success(new GivingPageResponse<CampaignUpdateResponse>
        {
            Items = [.. page.Select(CampaignMapper.ToResponse)],
            HasMore = hasMore,
            NextCursor = hasMore && page.Count > 0
                ? GivingCursor.Encode(
                    page, update => update.PublishedAt, update => update.Id.Value, cursor)
                : null
        });
    }
}

/// <summary>
/// Declares a campaign's milestones, and evaluates them immediately.
/// <para>
/// <b>Evaluated here as well as on every donation.</b> A milestone declared below a total the cause
/// already passed is reached the moment it is declared — otherwise it would sit unreached until the
/// next donation, or for ever if there is not one.
/// </para>
/// </summary>
public sealed class SetCampaignMilestonesCommandHandler
    : ICommandHandler<SetCampaignMilestonesCommand, IReadOnlyList<CampaignMilestoneResponse>>
{
    private readonly ICampaignRepository _campaigns;
    private readonly IDonationCauseRepository _causes;
    private readonly CampaignAccessService _access;
    private readonly CurrencyCatalog _currencies;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public SetCampaignMilestonesCommandHandler(
        ICampaignRepository campaigns,
        IDonationCauseRepository causes,
        CampaignAccessService access,
        CurrencyCatalog currencies,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _campaigns = campaigns;
        _causes = causes;
        _access = access;
        _currencies = currencies;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IReadOnlyList<CampaignMilestoneResponse>>> Handle(
        SetCampaignMilestonesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<CampaignMilestoneResponse>>(caller.Error);
        }

        var campaign = await _campaigns.FindAsync(
            CampaignId.From(command.CampaignId), cancellationToken);

        if (campaign is null)
        {
            return Result.Failure<IReadOnlyList<CampaignMilestoneResponse>>(Error.NotFound(
                FinancialErrorCodes.CampaignNotFound, "No campaign with that identifier."));
        }

        if (!await _access.CanManageAsync(campaign, caller.Value, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<CampaignMilestoneResponse>>(Error.Forbidden(
                FinancialErrorCodes.CampaignOrganizerForbidden,
                "That campaign is not yours to manage."));
        }

        var now = _clock.UtcNow;

        var declared = command.Request.Milestones
            .Select(milestone => (
                Id: milestone.MilestoneId ?? _guids.NewSequentialGuid(),
                milestone.Label,
                TargetMinorUnits: Money
                    .FromAmount(milestone.TargetAmount, campaign.Currency, campaign.Exponent)
                    .MinorUnits))
            .ToList();

        var set = campaign.SetMilestones(declared, now);

        if (set.IsFailure)
        {
            return Result.Failure<IReadOnlyList<CampaignMilestoneResponse>>(set.Error);
        }

        var cause = campaign.CauseId is { } causeId
            ? await _causes.FindAsync(causeId, cancellationToken)
            : null;

        campaign.EvaluateMilestones(cause?.RaisedMinorUnits ?? 0L, now);

        _campaigns.Update(campaign);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var symbol = await CampaignMapper.SymbolAsync(
            _currencies, campaign.Currency, cancellationToken);

        return Result.Success<IReadOnlyList<CampaignMilestoneResponse>>(
        [
            .. campaign.Milestones.Select(milestone => CampaignMapper.ToResponse(
                milestone, campaign.Currency, campaign.Exponent, symbol))
        ]);
    }
}

/// <summary>A campaign's milestones, with the server's own reached-ness.</summary>
public sealed class GetCampaignMilestonesQueryHandler
    : IQueryHandler<GetCampaignMilestonesQuery, IReadOnlyList<CampaignMilestoneResponse>>
{
    private readonly ICampaignRepository _campaigns;
    private readonly CampaignAccessService _access;
    private readonly CurrencyCatalog _currencies;

    public GetCampaignMilestonesQueryHandler(
        ICampaignRepository campaigns, CampaignAccessService access, CurrencyCatalog currencies)
    {
        _campaigns = campaigns;
        _access = access;
        _currencies = currencies;
    }

    public async Task<Result<IReadOnlyList<CampaignMilestoneResponse>>> Handle(
        GetCampaignMilestonesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var campaign = await _campaigns.FindAsync(
            CampaignId.From(query.CampaignId), cancellationToken);

        if (campaign is null)
        {
            return Result.Failure<IReadOnlyList<CampaignMilestoneResponse>>(Error.NotFound(
                FinancialErrorCodes.CampaignNotFound, "No campaign with that identifier."));
        }

        if (!campaign.IsPubliclyVisible
            && !await _access.CanManageAsync(campaign, _access.CallerOrNull(), cancellationToken))
        {
            return Result.Failure<IReadOnlyList<CampaignMilestoneResponse>>(Error.NotFound(
                FinancialErrorCodes.CampaignNotFound, "No campaign with that identifier."));
        }

        var symbol = await CampaignMapper.SymbolAsync(
            _currencies, campaign.Currency, cancellationToken);

        return Result.Success<IReadOnlyList<CampaignMilestoneResponse>>(
        [
            .. campaign.Milestones.Select(milestone => CampaignMapper.ToResponse(
                milestone, campaign.Currency, campaign.Exponent, symbol))
        ]);
    }
}

/// <summary>
/// A campaign's supporters, anonymised before they leave the server.
/// <para>
/// <b>The anonymisation is not a filter over a fuller record.</b> B15 stores no donor identifier on
/// an anonymous donation, so there is nothing here to withhold — and the mapper drops the message
/// too, because free text somebody wrote is one of the commonest ways an identity travels.
/// </para>
/// </summary>
public sealed class GetCampaignSupportersQueryHandler
    : IQueryHandler<GetCampaignSupportersQuery, GivingPageResponse<CampaignSupporterResponse>>
{
    private readonly ICampaignRepository _campaigns;
    private readonly IDonationRepository _donations;
    private readonly CampaignAccessService _access;
    private readonly CurrencyCatalog _currencies;

    public GetCampaignSupportersQueryHandler(
        ICampaignRepository campaigns,
        IDonationRepository donations,
        CampaignAccessService access,
        CurrencyCatalog currencies)
    {
        _campaigns = campaigns;
        _donations = donations;
        _access = access;
        _currencies = currencies;
    }

    public async Task<Result<GivingPageResponse<CampaignSupporterResponse>>> Handle(
        GetCampaignSupportersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var campaign = await _campaigns.FindAsync(
            CampaignId.From(query.CampaignId), cancellationToken);

        if (campaign is null)
        {
            return Result.Failure<GivingPageResponse<CampaignSupporterResponse>>(Error.NotFound(
                FinancialErrorCodes.CampaignNotFound, "No campaign with that identifier."));
        }

        if (!campaign.IsPubliclyVisible
            && !await _access.CanManageAsync(campaign, _access.CallerOrNull(), cancellationToken))
        {
            return Result.Failure<GivingPageResponse<CampaignSupporterResponse>>(Error.NotFound(
                FinancialErrorCodes.CampaignNotFound, "No campaign with that identifier."));
        }

        if (campaign.CauseId is not { } causeId)
        {
            // Not yet published: no cause, so nobody has given. An empty page rather than an error,
            // because "nobody has supported this yet" is a true and renderable answer.
            return Result.Success(new GivingPageResponse<CampaignSupporterResponse>
            {
                Items = [],
                HasMore = false,
                NextCursor = null
            });
        }

        var cursor = GivingCursor.Parse(query.Cursor);
        var take = Math.Clamp(query.Take ?? 20, 1, FinancialLimits.MaxLedgerPageSize);

        var rows = await _donations.ListSupportersAsync(
            causeId,
            cursor?.FilterBoundary,
            cursor?.ExcludedIds ?? [],
            take + 1,
            cancellationToken);

        var hasMore = rows.Count > take;
        var page = hasMore ? rows.Take(take).ToList() : [.. rows];
        var symbols = await _currencies.AllAsync(cancellationToken);

        return Result.Success(new GivingPageResponse<CampaignSupporterResponse>
        {
            Items =
            [
                .. page.Select(donation => CampaignMapper.ToSupporter(
                    donation,
                    symbols.TryGetValue(donation.Currency, out var facts) ? facts.Symbol : null))
            ],
            HasMore = hasMore,

            // Paged on CreatedAt, which is what ListSupportersAsync orders and filters by. Using
            // CompletedAt here would build a boundary the query does not use, and the page would
            // skip and repeat rows the moment a donation was pledged and captured on different days.
            NextCursor = hasMore && page.Count > 0
                ? GivingCursor.Encode(
                    page, donation => donation.CreatedAt, donation => donation.Id.Value, cursor)
                : null
        });
    }
}

/// <summary>Declares how a campaign's money is allocated, and reports what has been spent.</summary>
public sealed class SetCampaignFundUsageCommandHandler
    : ICommandHandler<SetCampaignFundUsageCommand, CampaignFundUsageResponse>
{
    private readonly ICampaignRepository _campaigns;
    private readonly IDonationCauseRepository _causes;
    private readonly IWithdrawalRepository _withdrawals;
    private readonly CampaignAccessService _access;
    private readonly CurrencyCatalog _currencies;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public SetCampaignFundUsageCommandHandler(
        ICampaignRepository campaigns,
        IDonationCauseRepository causes,
        IWithdrawalRepository withdrawals,
        CampaignAccessService access,
        CurrencyCatalog currencies,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _campaigns = campaigns;
        _causes = causes;
        _withdrawals = withdrawals;
        _access = access;
        _currencies = currencies;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CampaignFundUsageResponse>> Handle(
        SetCampaignFundUsageCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CampaignFundUsageResponse>(caller.Error);
        }

        var campaign = await _campaigns.FindAsync(
            CampaignId.From(command.CampaignId), cancellationToken);

        if (campaign is null)
        {
            return Result.Failure<CampaignFundUsageResponse>(Error.NotFound(
                FinancialErrorCodes.CampaignNotFound, "No campaign with that identifier."));
        }

        if (!await _access.CanManageAsync(campaign, caller.Value, cancellationToken))
        {
            return Result.Failure<CampaignFundUsageResponse>(Error.Forbidden(
                FinancialErrorCodes.CampaignOrganizerForbidden,
                "That campaign is not yours to manage."));
        }

        var lines = new List<(Guid, string, long, long?, string?, Guid?)>();

        foreach (var line in command.Request.Lines)
        {
            Guid? withdrawalId = null;

            if (line.WithdrawalId is { } candidate)
            {
                // Verification is only verification if the platform checked it. A payout reference
                // the caller does not own, or that has not settled, is refused rather than recorded
                // as proof — a false "platform verified" badge is worse than no badge.
                var withdrawal = await _withdrawals.FindAsync(
                    WithdrawalId.From(candidate), cancellationToken);

                if (withdrawal is null
                    || withdrawal.RequestedByUserId != caller.Value
                    || withdrawal.Status != Domain.Enums.WithdrawalStatus.Completed)
                {
                    return Result.Failure<CampaignFundUsageResponse>(Error.Validation(
                        FinancialErrorCodes.CampaignAllocationInvalid,
                        "That payout reference is not a completed payout of yours."));
                }

                withdrawalId = candidate;
            }

            lines.Add((
                line.Id ?? _guids.NewSequentialGuid(),
                line.Label,
                Money.FromAmount(line.Allocated, campaign.Currency, campaign.Exponent).MinorUnits,
                line.Spent is { } spent
                    ? Money.FromAmount(spent, campaign.Currency, campaign.Exponent).MinorUnits
                    : null,
                line.Note,
                withdrawalId));
        }

        var set = campaign.SetFundUsage(lines, _clock.UtcNow);

        if (set.IsFailure)
        {
            return Result.Failure<CampaignFundUsageResponse>(set.Error);
        }

        _campaigns.Update(campaign);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var cause = campaign.CauseId is { } causeId
            ? await _causes.FindAsync(causeId, cancellationToken)
            : null;

        return Result.Success(CampaignFundUsage.Compose(
            campaign,
            cause,
            await CampaignMapper.SymbolAsync(_currencies, campaign.Currency, cancellationToken)));
    }
}

/// <summary>Where a campaign's money is.</summary>
public sealed class GetCampaignFundUsageQueryHandler
    : IQueryHandler<GetCampaignFundUsageQuery, CampaignFundUsageResponse>
{
    private readonly ICampaignRepository _campaigns;
    private readonly IDonationCauseRepository _causes;
    private readonly CampaignAccessService _access;
    private readonly CurrencyCatalog _currencies;

    public GetCampaignFundUsageQueryHandler(
        ICampaignRepository campaigns,
        IDonationCauseRepository causes,
        CampaignAccessService access,
        CurrencyCatalog currencies)
    {
        _campaigns = campaigns;
        _causes = causes;
        _access = access;
        _currencies = currencies;
    }

    public async Task<Result<CampaignFundUsageResponse>> Handle(
        GetCampaignFundUsageQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var campaign = await _campaigns.FindAsync(
            CampaignId.From(query.CampaignId), cancellationToken);

        if (campaign is null)
        {
            return Result.Failure<CampaignFundUsageResponse>(Error.NotFound(
                FinancialErrorCodes.CampaignNotFound, "No campaign with that identifier."));
        }

        if (!campaign.IsPubliclyVisible
            && !await _access.CanManageAsync(campaign, _access.CallerOrNull(), cancellationToken))
        {
            return Result.Failure<CampaignFundUsageResponse>(Error.NotFound(
                FinancialErrorCodes.CampaignNotFound, "No campaign with that identifier."));
        }

        var cause = campaign.CauseId is { } causeId
            ? await _causes.FindAsync(causeId, cancellationToken)
            : null;

        return Result.Success(CampaignFundUsage.Compose(
            campaign,
            cause,
            await CampaignMapper.SymbolAsync(_currencies, campaign.Currency, cancellationToken)));
    }
}

/// <summary>
/// Composes the fund-usage answer from the four figures and their four authorities.
/// <para>
/// Written once because two handlers return it and a figure computed twice is a figure that will
/// differ in one of them.
/// </para>
/// </summary>
internal static class CampaignFundUsage
{
    public static CampaignFundUsageResponse Compose(
        Campaign campaign, DonationCause? cause, string? symbol)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        var raised = cause?.RaisedMinorUnits ?? 0L;
        var allocated = campaign.AllocatedMinorUnits;
        var disbursed = campaign.DisbursedMinorUnits;

        var verified = campaign.FundUsage
            .Where(line => line.IsPlatformVerified)
            .Sum(line => line.SpentMinorUnits ?? 0);

        return new CampaignFundUsageResponse
        {
            CampaignId = campaign.Id.Value,
            CauseCode = campaign.CauseCode,
            Raised = Amount(raised, campaign, symbol),
            Allocated = Amount(allocated, campaign, symbol),
            Disbursed = Amount(disbursed, campaign, symbol),
            DisbursedVerified = Amount(verified, campaign, symbol),

            // Clamped at zero rather than reported negative. A negative "remaining" is not a fact
            // about money; it is an organizer's arithmetic, and IsOverReported is where that is said.
            Remaining = Amount(Math.Max(0, raised - disbursed), campaign, symbol),
            Lines =
            [
                .. campaign.FundUsage.Select(line => new FundUsageLineResponse
                {
                    Id = line.Id,
                    Label = line.Label,
                    Allocated = Amount(line.AllocatedMinorUnits, campaign, symbol),
                    Spent = line.SpentMinorUnits is { } spent
                        ? Amount(spent, campaign, symbol)
                        : null,
                    Note = line.Note,
                    IsPlatformVerified = line.IsPlatformVerified
                })
            ],
            IsOverReported = disbursed > raised
        };
    }

    private static MoneyResponse Amount(long minorUnits, Campaign campaign, string? symbol) =>
        FinancialMapper.ToResponse(
            Money.FromMinorUnits(minorUnits, campaign.Currency, campaign.Exponent), symbol);
}

/// <summary>
/// A cause's giving over a window, bucketed by UTC day.
/// <para>
/// <b>Every day in the window is present, including the empty ones.</b> A series that omitted them
/// would let a client draw a line straight from Monday to Friday and call the middle of it
/// Wednesday's giving — which is the client inventing a financial figure by interpolation, and §17
/// forbids it however it happens.
/// </para>
/// </summary>
public sealed class GetDonationTimeSeriesQueryHandler
    : IQueryHandler<GetDonationTimeSeriesQuery, DonationTimeSeriesResponse>
{
    private readonly IDonationCauseRepository _causes;
    private readonly IDonationRepository _donations;
    private readonly CurrencyCatalog _currencies;
    private readonly IClock _clock;
    private readonly FinancialOptions _options;

    public GetDonationTimeSeriesQueryHandler(
        IDonationCauseRepository causes,
        IDonationRepository donations,
        CurrencyCatalog currencies,
        IClock clock,
        IOptions<FinancialOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _causes = causes;
        _donations = donations;
        _currencies = currencies;
        _clock = clock;
        _options = options.Value;
    }

    public async Task<Result<DonationTimeSeriesResponse>> Handle(
        GetDonationTimeSeriesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var cause = await _causes.FindByCodeAsync(query.CauseCode, cancellationToken);

        if (cause is null)
        {
            return Result.Failure<DonationTimeSeriesResponse>(Error.NotFound(
                FinancialErrorCodes.CauseNotFound, "No cause with that code."));
        }

        var now = _clock.UtcNow;

        // Whole UTC days at both ends, so the first and last buckets are complete days rather than
        // a partial one whose figure would look like a collapse in giving.
        var to = Floor(query.To ?? now).AddDays(1).AddTicks(-1);
        var from = Floor(query.From ?? to.AddDays(-29));

        if (from > to)
        {
            (from, to) = (Floor(to), Floor(from).AddDays(1).AddTicks(-1));
        }

        // Clamped rather than refused: a browse screen asking for "all time" should get a chart, and
        // the response says which window it actually covers.
        if ((to - from).TotalDays > _options.MaxTimeSeriesDays)
        {
            from = Floor(to.AddDays(-_options.MaxTimeSeriesDays));
        }

        var buckets = await _donations.TimeSeriesForCauseAsync(cause.Id, from, to, cancellationToken);
        var summary = await _donations.SummariseForCauseAsync(cause.Id, from, to, cancellationToken);
        var symbol = await _currencies.SymbolAsync(cause.Currency, cancellationToken);
        var byDay = buckets.ToDictionary(bucket => bucket.PeriodStart);
        var points = new List<DonationTimeSeriesPointResponse>();

        for (var day = Floor(from); day <= to; day = day.AddDays(1))
        {
            var found = byDay.TryGetValue(day, out var bucket);

            points.Add(new DonationTimeSeriesPointResponse
            {
                PeriodStart = day,
                Amount = FinancialMapper.ToResponse(
                    Money.FromMinorUnits(
                        found ? bucket!.AmountMinorUnits : 0L, cause.Currency, cause.Exponent),
                    symbol),
                DonationCount = found ? bucket!.DonationCount : 0
            });
        }

        return Result.Success(new DonationTimeSeriesResponse
        {
            CauseCode = cause.Code,
            Currency = cause.Currency,
            From = from,
            To = to,
            TimeZone = "UTC",
            Granularity = "Day",
            Points = points,
            Total = FinancialMapper.ToResponse(
                Money.FromMinorUnits(
                    summary.CompletedMinorUnits, cause.Currency, cause.Exponent), symbol),
            Refunded = FinancialMapper.ToResponse(
                Money.FromMinorUnits(
                    summary.RefundedMinorUnits, cause.Currency, cause.Exponent), symbol),
            DonationCount = summary.CompletedCount,
            RefundedCount = summary.RefundedCount,
            FailedCount = summary.FailedCount,
            SupporterCount = summary.SupporterCount
        });
    }

    private static DateTimeOffset Floor(DateTimeOffset moment) =>
        new(moment.UtcDateTime.Date, TimeSpan.Zero);
}
