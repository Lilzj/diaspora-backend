using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Requests;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Advertising;
using Diaspora.Modules.Financial.Domain.Catalog;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Advertising;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// The advertiser's own acts — MON-GAP-001.
//
// Every handler here loads through FindForOwnerAsync, so a campaign somebody else owns answers as
// absent rather than forbidden. The row carries their budget, spend, creative and targeting; a 403
// would confirm the campaign exists to a competitor who guessed an id.
//
// None of these move money. Funding goes through B15's purchase and payment-intent lifecycle,
// which is built and connected, and MarkFunded is what that path calls back into.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Drafts a campaign. Nothing is charged and nothing is booked.</summary>
[RequirePermission(FinancialPermissions.PurchasesCreate)]
public sealed record CreateAdCampaignCommand : ICommand<AdCampaignResponse>
{
    public required CreateAdCampaignRequest Request { get; init; }
}

/// <summary>Rewrites a draft. Refused once the campaign is funded.</summary>
[RequirePermission(FinancialPermissions.PurchasesCreate)]
public sealed record UpdateAdCampaignCommand : ICommand<AdCampaignResponse>
{
    public required Guid CampaignId { get; init; }

    public required UpdateAdCampaignRequest Request { get; init; }
}

/// <summary>
/// Submits for review, which is what freezes the campaign — before the money arrives, not after.
/// </summary>
[RequirePermission(FinancialPermissions.PurchasesCreate)]
public sealed record SubmitAdCampaignCommand : ICommand<AdCampaignSubmissionResponse>
{
    public required Guid CampaignId { get; init; }

    /// <summary>How the advertiser intends to pay. Passed through to the purchase unchanged.</summary>
    public string? MethodKind { get; init; }

    /// <summary>
    /// Makes a retried submission safe.
    /// <para>
    /// Submission opens a purchase, and a purchase is a charge. A double tap on a slow connection
    /// must not open two — the existing purchase path already honours this key, so it is passed
    /// straight through rather than re-implemented here.
    /// </para>
    /// </summary>
    public string? IdempotencyKey { get; init; }
}

/// <summary>Pauses or resumes a running campaign. The advertiser's own control.</summary>
[RequirePermission(FinancialPermissions.PurchasesCreate)]
public sealed record SetAdCampaignRunStateCommand : ICommand<AdCampaignResponse>
{
    public required Guid CampaignId { get; init; }

    /// <summary>True to pause, false to resume.</summary>
    public required bool Paused { get; init; }
}

/// <summary>Stops a campaign for good and records the unspent budget as owed.</summary>
[RequirePermission(FinancialPermissions.PurchasesCreate)]
public sealed record CancelAdCampaignCommand : ICommand<AdCampaignResponse>
{
    public required Guid CampaignId { get; init; }

    public CancelAdCampaignRequest? Request { get; init; }
}

/// <summary>The advertiser contests a rejection — MON-GAP-006.</summary>
[RequirePermission(FinancialPermissions.PurchasesCreate)]
public sealed record AppealAdCampaignCommand : ICommand<AdCampaignReviewResponse>
{
    public required Guid CampaignId { get; init; }

    public AppealAdCampaignRequest? Request { get; init; }
}

// ════════════════════════════════════════════════════════════════════════════════════════════════

public sealed class CreateAdCampaignCommandHandler
    : ICommandHandler<CreateAdCampaignCommand, AdCampaignResponse>
{
    private readonly IAdCampaignRepository _campaigns;
    private readonly FinancialAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAdCampaignCommandHandler(
        IAdCampaignRepository campaigns,
        FinancialAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _campaigns = campaigns;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AdCampaignResponse>> Handle(
        CreateAdCampaignCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<AdCampaignResponse>(caller.Error);
        }

        var built = AdCampaignFactory.Build(
            AdCampaignId.From(_guids.NewSequentialGuid()),
            caller.Value,
            command.Request,
            _clock.UtcNow);

        if (built.IsFailure)
        {
            return Result.Failure<AdCampaignResponse>(built.Error);
        }

        _campaigns.Add(built.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(AdCampaignMapper.ToResponse(built.Value));
    }
}

public sealed class UpdateAdCampaignCommandHandler
    : ICommandHandler<UpdateAdCampaignCommand, AdCampaignResponse>
{
    private readonly IAdCampaignRepository _campaigns;
    private readonly FinancialAuthorizationService _authorization;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateAdCampaignCommandHandler(
        IAdCampaignRepository campaigns,
        FinancialAuthorizationService authorization,
        IUnitOfWork unitOfWork)
    {
        _campaigns = campaigns;
        _authorization = authorization;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AdCampaignResponse>> Handle(
        UpdateAdCampaignCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var loaded = await AdCampaignLoader.ForOwnerAsync(
            _campaigns, _authorization, command.CampaignId, cancellationToken);

        if (loaded.IsFailure)
        {
            return Result.Failure<AdCampaignResponse>(loaded.Error);
        }

        var campaign = loaded.Value;
        var request = command.Request.AsDraft();

        var budget = AdCampaignFactory.ReadBudget(request);

        if (budget.IsFailure)
        {
            return Result.Failure<AdCampaignResponse>(budget.Error);
        }

        var schedule = AdCampaignFactory.ReadSchedule(request);

        if (schedule.IsFailure)
        {
            return Result.Failure<AdCampaignResponse>(schedule.Error);
        }

        var objective = AdCampaignFactory.ReadObjective(request.Objective);

        if (objective.IsFailure)
        {
            return Result.Failure<AdCampaignResponse>(objective.Error);
        }

        var updated = campaign.Update(
            request.Name,
            objective.Value,
            budget.Value,
            schedule.Value,
            AdCampaignFactory.ReadAudience(request.Audience));

        if (updated.IsFailure)
        {
            return Result.Failure<AdCampaignResponse>(updated.Error);
        }

        var creative = campaign.SetCreative(
            request.CreativeMediaId, request.Headline, request.Body, request.CallToActionLabel);

        if (creative.IsFailure)
        {
            return Result.Failure<AdCampaignResponse>(creative.Error);
        }

        var subject = AdCampaignFactory.ReadSubject(request);

        if (subject.IsFailure)
        {
            return Result.Failure<AdCampaignResponse>(subject.Error);
        }

        campaign.SetSubject(subject.Value, request.ProductCode);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(AdCampaignMapper.ToResponse(campaign));
    }
}

/// <summary>
/// Freezes the campaign and tells the advertiser what to buy.
/// <para>
/// <b>It does not open the purchase itself, and that is a platform rule rather than a shortcut.</b>
/// The first version dispatched B15's own purchase command from here, which the DSP1001 analyzer
/// refuses: a handler holding <c>ISender</c> can reach every handler in the process. The alternative
/// it suggests — extracting the purchase path into a shared service — is a refactor of 125 lines and
/// fifteen dependencies on the one path that takes somebody's money.
/// </para>
/// <para>
/// <b>So the campaign became a purchase subject instead</b>, which is the model this platform
/// already has for everything else it sells. The client buys the campaign's package through the
/// existing purchase route with <c>subjectType: Campaign</c>, and the capture finds its way back on
/// its own. One extra call, no second implementation of pricing, and no handler reaching into
/// another.
/// </para>
/// <para>
/// <b>The campaign still freezes here, before the money.</b> Editing a campaign somebody is halfway
/// through paying for is the thing this ordering prevents.
/// </para>
/// </summary>
public sealed class SubmitAdCampaignCommandHandler
    : ICommandHandler<SubmitAdCampaignCommand, AdCampaignSubmissionResponse>
{
    private readonly IAdCampaignRepository _campaigns;
    private readonly IMonetizationProductRepository _products;
    private readonly FinancialAuthorizationService _authorization;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public SubmitAdCampaignCommandHandler(
        IAdCampaignRepository campaigns,
        IMonetizationProductRepository products,
        FinancialAuthorizationService authorization,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _campaigns = campaigns;
        _products = products;
        _authorization = authorization;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AdCampaignSubmissionResponse>> Handle(
        SubmitAdCampaignCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var loaded = await AdCampaignLoader.ForOwnerAsync(
            _campaigns, _authorization, command.CampaignId, cancellationToken);

        if (loaded.IsFailure)
        {
            return Result.Failure<AdCampaignSubmissionResponse>(loaded.Error);
        }

        var campaign = loaded.Value;

        if (string.IsNullOrWhiteSpace(campaign.ProductCode))
        {
            return Result.Failure<AdCampaignSubmissionResponse>(
                AdCampaignErrors.ProductRequiredToSubmit);
        }

        var product = await _products.FindByCodeAsync(campaign.ProductCode, cancellationToken);

        if (product is null)
        {
            return Result.Failure<AdCampaignSubmissionResponse>(
                AdCampaignErrors.ProductRequiredToSubmit);
        }

        // The budget and the charge must be one number.
        //
        // A campaign is bought as a tier — see the catalogue seed for why the payment path cannot
        // take an arbitrary amount — so a budget that did not match the tier would give the
        // advertiser two figures: one on their dashboard and one on their statement.
        if (product.Price is not { } price
            || price.MinorUnits != campaign.BudgetMinorUnits
            || !string.Equals(price.Currency, campaign.Currency, StringComparison.Ordinal))
        {
            return Result.Failure<AdCampaignSubmissionResponse>(
                AdCampaignErrors.BudgetDoesNotMatchProduct);
        }

        var submitted = campaign.Submit(_clock.UtcNow);

        if (submitted.IsFailure)
        {
            return Result.Failure<AdCampaignSubmissionResponse>(submitted.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new AdCampaignSubmissionResponse
        {
            Campaign = AdCampaignMapper.ToResponse(campaign),
            ProductCode = product.Code,
            SubjectType = MonetizationSubjectType.Campaign.ToString(),
            SubjectId = campaign.Id.Value,
            Amount = new AdMoneyResponse
            {
                MinorUnits = price.MinorUnits,
                Currency = price.Currency,
                Exponent = price.Exponent
            }
        });
    }
}

public sealed class SetAdCampaignRunStateCommandHandler
    : ICommandHandler<SetAdCampaignRunStateCommand, AdCampaignResponse>
{
    private readonly IAdCampaignRepository _campaigns;
    private readonly FinancialAuthorizationService _authorization;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public SetAdCampaignRunStateCommandHandler(
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
        SetAdCampaignRunStateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var loaded = await AdCampaignLoader.ForOwnerAsync(
            _campaigns, _authorization, command.CampaignId, cancellationToken);

        if (loaded.IsFailure)
        {
            return Result.Failure<AdCampaignResponse>(loaded.Error);
        }

        var now = _clock.UtcNow;

        var applied = command.Paused
            ? loaded.Value.Pause(now)
            : loaded.Value.Resume(now);

        if (applied.IsFailure)
        {
            return Result.Failure<AdCampaignResponse>(applied.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(AdCampaignMapper.ToResponse(loaded.Value));
    }
}

public sealed class CancelAdCampaignCommandHandler
    : ICommandHandler<CancelAdCampaignCommand, AdCampaignResponse>
{
    private readonly IAdCampaignRepository _campaigns;
    private readonly FinancialAuthorizationService _authorization;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public CancelAdCampaignCommandHandler(
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
        CancelAdCampaignCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var loaded = await AdCampaignLoader.ForOwnerAsync(
            _campaigns, _authorization, command.CampaignId, cancellationToken);

        if (loaded.IsFailure)
        {
            return Result.Failure<AdCampaignResponse>(loaded.Error);
        }

        var cancelled = loaded.Value.Cancel(command.Request?.Reason, _clock.UtcNow);

        if (cancelled.IsFailure)
        {
            return Result.Failure<AdCampaignResponse>(cancelled.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // The unspent budget is now recorded on the campaign as owed. B15's settlement path reads
        // it and pays it; nothing here moves money.
        return Result.Success(AdCampaignMapper.ToResponse(loaded.Value));
    }
}

public sealed class AppealAdCampaignCommandHandler
    : ICommandHandler<AppealAdCampaignCommand, AdCampaignReviewResponse>
{
    private readonly IAdCampaignRepository _campaigns;
    private readonly FinancialAuthorizationService _authorization;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public AppealAdCampaignCommandHandler(
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

    public async Task<Result<AdCampaignReviewResponse>> Handle(
        AppealAdCampaignCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var loaded = await AdCampaignLoader.ForOwnerAsync(
            _campaigns, _authorization, command.CampaignId, cancellationToken);

        if (loaded.IsFailure)
        {
            return Result.Failure<AdCampaignReviewResponse>(loaded.Error);
        }

        var appealed = loaded.Value.Appeal(command.Request?.Note, _clock.UtcNow);

        if (appealed.IsFailure)
        {
            return Result.Failure<AdCampaignReviewResponse>(appealed.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(AdCampaignMapper.ToReviewResponse(loaded.Value));
    }
}

/// <summary>
/// Loads a campaign the caller owns, or explains why not.
/// <para>
/// Shared by every advertiser-facing handler so the ownership check is written once. A campaign
/// somebody else owns answers as <see cref="AdCampaignErrors.NotFound"/> — not forbidden, because a
/// 403 would confirm to a competitor that the id they guessed is real.
/// </para>
/// </summary>
internal static class AdCampaignLoader
{
    public static async Task<Result<AdCampaign>> ForOwnerAsync(
        IAdCampaignRepository campaigns,
        FinancialAuthorizationService authorization,
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        var caller = authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<AdCampaign>(caller.Error);
        }

        var campaign = await campaigns.FindForOwnerAsync(
            AdCampaignId.From(campaignId), caller.Value, cancellationToken);

        return campaign is null
            ? Result.Failure<AdCampaign>(AdCampaignErrors.NotFound)
            : Result.Success(campaign);
    }
}

/// <summary>
/// Reads a request into the aggregate's own types.
/// <para>
/// Shared between create and update because they take the same shape, and because a second copy of
/// "what does this string mean" is a second place for the two to disagree about a currency code.
/// </para>
/// </summary>
internal static class AdCampaignFactory
{
    public static Result<AdCampaign> Build(
        AdCampaignId id, Guid ownerUserId, CreateAdCampaignRequest request, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);

        var objective = ReadObjective(request.Objective);

        if (objective.IsFailure)
        {
            return Result.Failure<AdCampaign>(objective.Error);
        }

        var budget = ReadBudget(request);

        if (budget.IsFailure)
        {
            return Result.Failure<AdCampaign>(budget.Error);
        }

        var schedule = ReadSchedule(request, now);

        if (schedule.IsFailure)
        {
            return Result.Failure<AdCampaign>(schedule.Error);
        }

        var drafted = AdCampaign.Draft(
            id,
            ownerUserId,
            request.Name,
            objective.Value,
            budget.Value,
            schedule.Value,
            ReadAudience(request.Audience));

        if (drafted.IsFailure)
        {
            return drafted;
        }

        var campaign = drafted.Value;
        campaign.SetCreative(
            request.CreativeMediaId, request.Headline, request.Body, request.CallToActionLabel);

        var subject = ReadSubject(request);

        if (subject.IsFailure)
        {
            return Result.Failure<AdCampaign>(subject.Error);
        }

        campaign.SetSubject(subject.Value, request.ProductCode);

        return Result.Success(campaign);
    }

    public static Result<AdCampaignObjective> ReadObjective(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<AdCampaignObjective>(AdCampaignErrors.ObjectiveUnknown);
        }

        return Enum.TryParse<AdCampaignObjective>(value.Trim(), ignoreCase: true, out var objective)
            && Enum.IsDefined(objective)
                ? Result.Success(objective)
                : Result.Failure<AdCampaignObjective>(AdCampaignErrors.ObjectiveUnknown);
    }

    public static Result<Money> ReadBudget(CreateAdCampaignRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var currency = Money.NormalizeCurrency(request.Currency);

        if (currency is null)
        {
            return Result.Failure<Money>(AdCampaignErrors.CurrencyMismatch);
        }

        if (request.BudgetMinorUnits <= 0)
        {
            return Result.Failure<Money>(AdCampaignErrors.BudgetRequired);
        }

        return Result.Success(Money.FromMinorUnits(
            request.BudgetMinorUnits, currency, request.Exponent ?? Money.DefaultExponent));
    }

    public static Result<DateWindow> ReadSchedule(
        CreateAdCampaignRequest request, DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        // An absent start means "as soon as it is approved", which is what an advertiser filling in
        // a wizard means when they leave it blank.
        var startsAt = request.StartsAt ?? now ?? DateTimeOffset.UtcNow;

        if (request.EndsAt is not { } endsAt)
        {
            return Result.Failure<DateWindow>(AdCampaignErrors.ScheduleRequiresEnd);
        }

        return endsAt <= startsAt
            ? Result.Failure<DateWindow>(AdCampaignErrors.ScheduleAlreadyOver)
            : Result.Success(DateWindow.Create(startsAt, endsAt));
    }

    public static AdAudience ReadAudience(AdAudienceRequest? request) =>
        request is null
            ? AdAudience.Everyone()
            : AdAudience.Create(
                request.CountryCodes,
                request.CommunityIds,

                // Catalogue neighbourhoods are retired (14 Oct 2026): accepted from an older
                // client and not stored. Town names replace them.
                neighbourhoodIds: null,
                request.Interests,
                request.Categories,
                request.TownNames);

    /// <summary>
    /// What a click leads to. Both halves or neither — a subject type with no id, or an id with no
    /// type, is a reference to nothing that would be stored and never resolve.
    /// </summary>
    public static Result<SubjectReference?> ReadSubject(CreateAdCampaignRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.SubjectType) && request.SubjectId is null)
        {
            return Result.Success<SubjectReference?>(null);
        }

        if (!Enum.TryParse<MonetizationSubjectType>(
                request.SubjectType?.Trim(), ignoreCase: true, out var subjectType)
            || !Enum.IsDefined(subjectType)
            || request.SubjectId is not { } subjectId
            || subjectId == Guid.Empty)
        {
            return Result.Failure<SubjectReference?>(AdCampaignErrors.SubjectIncomplete);
        }

        return Result.Success<SubjectReference?>(
            SubjectReference.Create(subjectType, subjectId));
    }
}
