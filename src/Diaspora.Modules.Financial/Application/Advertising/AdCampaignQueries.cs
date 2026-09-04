using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Requests;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Advertising;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Advertising;

/// <summary>The caller's own campaigns — MON-GAP-001.</summary>
[Authorize(Permission = FinancialPermissions.PurchasesRead)]
public sealed record GetMyAdCampaignsQuery : IQuery<IReadOnlyList<AdCampaignResponse>>
{
    /// <summary>
    /// Comma-separated <c>AdCampaignStatus</c> names. Absent means every state, terminal ones
    /// included — a completed campaign is what an advertiser goes back to when they want to run it
    /// again, and a dashboard that hid it would hide their own history.
    /// </summary>
    public string? Status { get; init; }

    public int? Take { get; init; }
}

/// <summary>One campaign, with its authoritative status.</summary>
[Authorize(Permission = FinancialPermissions.PurchasesRead)]
public sealed record GetAdCampaignQuery : IQuery<AdCampaignResponse>
{
    public required Guid CampaignId { get; init; }
}

/// <summary>The decision, the note and the appeal route — MON-GAP-006.</summary>
[Authorize(Permission = FinancialPermissions.PurchasesRead)]
public sealed record GetAdCampaignReviewQuery : IQuery<AdCampaignReviewResponse>
{
    public required Guid CampaignId { get; init; }
}

/// <summary>
/// The review queue. Staff only, and gated on a different permission from the advertiser's own
/// reads — a queue of everybody's creative and targeting is not something a purchase permission
/// should open.
/// </summary>
[Authorize(Permission = FinancialPermissions.ReportingRead)]
public sealed record GetAdCampaignReviewQueueQuery : IQuery<IReadOnlyList<AdCampaignResponse>>
{
    public int? Take { get; init; }
}

// ════════════════════════════════════════════════════════════════════════════════════════════════

public sealed class GetMyAdCampaignsQueryHandler
    : IQueryHandler<GetMyAdCampaignsQuery, IReadOnlyList<AdCampaignResponse>>
{
    private readonly IAdCampaignRepository _campaigns;
    private readonly FinancialAuthorizationService _authorization;

    public GetMyAdCampaignsQueryHandler(
        IAdCampaignRepository campaigns, FinancialAuthorizationService authorization)
    {
        _campaigns = campaigns;
        _authorization = authorization;
    }

    public async Task<Result<IReadOnlyList<AdCampaignResponse>>> Handle(
        GetMyAdCampaignsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<AdCampaignResponse>>(caller.Error);
        }

        var campaigns = await _campaigns.ListForOwnerAsync(
            caller.Value, ReadStatuses(query.Status), query.Take ?? 50, cancellationToken);

        return Result.Success<IReadOnlyList<AdCampaignResponse>>(
            [.. campaigns.Select(AdCampaignMapper.ToResponse)]);
    }

    /// <summary>
    /// Reads the comma-separated status filter.
    /// <para>
    /// An unrecognised name is dropped rather than refused — a newer client naming a state this
    /// deployment does not have should get the states it does, not a 400 on the whole dashboard. A
    /// filter that names only unknown states is treated as no filter, for the same reason.
    /// </para>
    /// </summary>
    private static List<AdCampaignStatus>? ReadStatuses(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parsed = value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(name => Enum.TryParse<AdCampaignStatus>(name, ignoreCase: true, out var status)
                && Enum.IsDefined(status)
                    ? status
                    : (AdCampaignStatus?)null)
            .OfType<AdCampaignStatus>()
            .Distinct()
            .ToList();

        return parsed.Count > 0 ? parsed : null;
    }
}

public sealed class GetAdCampaignQueryHandler
    : IQueryHandler<GetAdCampaignQuery, AdCampaignResponse>
{
    private readonly IAdCampaignRepository _campaigns;
    private readonly FinancialAuthorizationService _authorization;

    public GetAdCampaignQueryHandler(
        IAdCampaignRepository campaigns, FinancialAuthorizationService authorization)
    {
        _campaigns = campaigns;
        _authorization = authorization;
    }

    public async Task<Result<AdCampaignResponse>> Handle(
        GetAdCampaignQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var loaded = await AdCampaignLoader.ForOwnerAsync(
            _campaigns, _authorization, query.CampaignId, cancellationToken);

        return loaded.IsFailure
            ? Result.Failure<AdCampaignResponse>(loaded.Error)
            : Result.Success(AdCampaignMapper.ToResponse(loaded.Value));
    }
}

public sealed class GetAdCampaignReviewQueryHandler
    : IQueryHandler<GetAdCampaignReviewQuery, AdCampaignReviewResponse>
{
    private readonly IAdCampaignRepository _campaigns;
    private readonly FinancialAuthorizationService _authorization;

    public GetAdCampaignReviewQueryHandler(
        IAdCampaignRepository campaigns, FinancialAuthorizationService authorization)
    {
        _campaigns = campaigns;
        _authorization = authorization;
    }

    public async Task<Result<AdCampaignReviewResponse>> Handle(
        GetAdCampaignReviewQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var loaded = await AdCampaignLoader.ForOwnerAsync(
            _campaigns, _authorization, query.CampaignId, cancellationToken);

        return loaded.IsFailure
            ? Result.Failure<AdCampaignReviewResponse>(loaded.Error)
            : Result.Success(AdCampaignMapper.ToReviewResponse(loaded.Value));
    }
}

public sealed class GetAdCampaignReviewQueueQueryHandler
    : IQueryHandler<GetAdCampaignReviewQueueQuery, IReadOnlyList<AdCampaignResponse>>
{
    private readonly IAdCampaignRepository _campaigns;

    public GetAdCampaignReviewQueueQueryHandler(IAdCampaignRepository campaigns) =>
        _campaigns = campaigns;

    public async Task<Result<IReadOnlyList<AdCampaignResponse>>> Handle(
        GetAdCampaignReviewQueueQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var campaigns = await _campaigns.ListAwaitingReviewAsync(
            query.Take ?? 50, cancellationToken);

        return Result.Success<IReadOnlyList<AdCampaignResponse>>(
            [.. campaigns.Select(AdCampaignMapper.ToResponse)]);
    }
}

/// <summary>
/// A reviewer's decision — MON-GAP-006.
/// <para>
/// <b>Gated on a staff permission and loaded without an ownership check</b>, which is the one place
/// in this file that reads across owners. The advertiser's own commands cannot reach it: there is no
/// request shape that lets somebody approve their own campaign.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.ReportingRead)]
public sealed record DecideAdCampaignCommand : ICommand<AdCampaignResponse>
{
    public required Guid CampaignId { get; init; }

    public DecideAdCampaignRequest? Request { get; init; }
}

public sealed class DecideAdCampaignCommandHandler
    : ICommandHandler<DecideAdCampaignCommand, AdCampaignResponse>
{
    private readonly IAdCampaignRepository _campaigns;
    private readonly FinancialAuthorizationService _authorization;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public DecideAdCampaignCommandHandler(
        IAdCampaignRepository campaigns,
        FinancialAuthorizationService authorization,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _campaigns = campaigns;
        _authorization = authorization;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AdCampaignResponse>> Handle(
        DecideAdCampaignCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var reviewer = _authorization.RequireUserId();

        if (reviewer.IsFailure)
        {
            return Result.Failure<AdCampaignResponse>(reviewer.Error);
        }

        var campaign = await _campaigns.FindAsync(
            AdCampaignId.From(command.CampaignId), cancellationToken);

        if (campaign is null)
        {
            return Result.Failure<AdCampaignResponse>(AdCampaignErrors.NotFound);
        }

        // A reviewer must not be the advertiser. Somebody approving their own creative is the one
        // conflict this queue exists to prevent, and it costs one comparison to refuse.
        if (campaign.OwnerUserId == reviewer.Value)
        {
            return Result.Failure<AdCampaignResponse>(AdCampaignErrors.CannotReviewOwn);
        }

        var decision = command.Request?.Decision?.Trim().ToLowerInvariant();
        var now = _clock.UtcNow;

        var applied = decision switch
        {
            "approve" => campaign.Approve(reviewer.Value, command.Request?.Note, now),
            "reject" => campaign.Reject(reviewer.Value, command.Request?.Note, now),
            _ => Result.Failure(AdCampaignErrors.DecisionUnknown)
        };

        if (applied.IsFailure)
        {
            return Result.Failure<AdCampaignResponse>(applied.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(AdCampaignMapper.ToResponse(campaign));
    }
}
