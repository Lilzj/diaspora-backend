using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Financial.Application.Campaigns;

/// <summary>
/// Creates a campaign in draft. COMMUNITY-LIFE-GAP-006.
/// <para>
/// <b>Nothing financial exists yet, and that is the design.</b> A draft has no cause, so there is
/// nowhere to collect and no donate button anywhere on the platform. The cause arrives at
/// publication and not one moment earlier.
/// </para>
/// <para>
/// <b>The idempotency key is looked up before the write and the unique index catches the race.</b>
/// B15's own primitive, copied rather than reinvented — the same shape payment intents, purchases,
/// subscriptions, donations and contributions already use. A retried creation is answered with the
/// campaign that exists.
/// </para>
/// </summary>
public sealed class CreateCampaignCommandHandler
    : ICommandHandler<CreateCampaignCommand, CampaignResponse>
{
    private readonly ICampaignRepository _campaigns;
    private readonly CampaignAccessService _access;
    private readonly CurrencyCatalog _currencies;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCampaignCommandHandler(
        ICampaignRepository campaigns,
        CampaignAccessService access,
        CurrencyCatalog currencies,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _campaigns = campaigns;
        _access = access;
        _currencies = currencies;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CampaignResponse>> Handle(
        CreateCampaignCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CampaignResponse>(caller.Error);
        }

        var request = command.Request;
        var now = _clock.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey)
            && await _campaigns.FindByIdempotencyKeyAsync(
                caller.Value, request.IdempotencyKey, cancellationToken) is { } replayed)
        {
            // A retry of a request that already worked. Answered with what it created, and the
            // ownership check is what stops a second caller replaying somebody else's key.
            if (replayed.OrganizerUserId != caller.Value)
            {
                return Result.Failure<CampaignResponse>(Error.Conflict(
                    FinancialErrorCodes.CampaignNotFound,
                    "That idempotency key belongs to another request."));
            }

            return Result.Success(CampaignMapper.ToResponse(
                replayed,
                null,
                true,
                now,
                await CampaignMapper.SymbolAsync(_currencies, replayed.Currency, cancellationToken),
                _access.CanDecideReview()));
        }

        var organizer = await _access.RequireOrganizerAsync(
            request.OrganizationId, caller.Value, cancellationToken);

        if (organizer.IsFailure)
        {
            return Result.Failure<CampaignResponse>(organizer.Error);
        }

        if (!await _currencies.IsSupportedAsync(request.Currency, cancellationToken))
        {
            return Result.Failure<CampaignResponse>(Error.Validation(
                FinancialErrorCodes.CurrencyNotSupported,
                $"'{request.Currency}' is not a currency this deployment collects in."));
        }

        var facts = await _currencies.FactsAsync(request.Currency, cancellationToken);

        if (!Enum.TryParse<BeneficiaryType>(request.BeneficiaryType, true, out var beneficiaryType)
            || !Enum.IsDefined(beneficiaryType))
        {
            return Result.Failure<CampaignResponse>(Error.Validation(
                FinancialErrorCodes.SubjectInvalid,
                $"'{request.BeneficiaryType}' is not a beneficiary this platform recognises."));
        }

        // The organizer's label. An organization campaign carries the body's name where the seam can
        // resolve one, and the member's own otherwise — a blank organizer is worse than either.
        var organizerName = request.OrganizationId is { } body
            ? await _access.DescribeOrganizationAsync(body, cancellationToken)
            : null;

        var campaign = Campaign.Open(
            CampaignId.From(_guids.NewSequentialGuid()),
            caller.Value,
            organizerName ?? request.BeneficiaryName,
            request.Title,
            request.Summary,
            request.Story,
            CampaignMapper.ParseCategory(request.Category),
            beneficiaryType,
            request.BeneficiaryName,
            facts.Code,
            facts.Exponent,
            request.AcceptedTerms,
            now,
            request.OrganizationId,
            request.BeneficiaryId,
            request.TargetAmount is { } target
                ? Money.FromAmount(target, facts.Code, facts.Exponent)
                : null,
            request.Deadline,
            request.CountryCode,
            request.LocationLabel,
            request.CoverImage,
            request.Gallery,
            ParseKinds(request.AcceptedKinds),
            request.IdempotencyKey);

        if (campaign.IsFailure)
        {
            return Result.Failure<CampaignResponse>(campaign.Error);
        }

        _campaigns.Add(campaign.Value);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(CampaignMapper.ToResponse(
            campaign.Value, null, true, now, facts.Symbol, _access.CanDecideReview()));
    }

    private static IReadOnlyList<ContributionKind>? ParseKinds(IReadOnlyList<string>? kinds) =>
        kinds is null
            ? null
            :
            [
                .. kinds
                    .Select(kind => Enum.TryParse<ContributionKind>(kind, true, out var parsed)
                        && Enum.IsDefined(parsed)
                        ? parsed
                        : (ContributionKind?)null)
                    .Where(kind => kind is not null)
                    .Select(kind => kind!.Value)
            ];
}

/// <summary>Edits a campaign's own text.</summary>
public sealed class UpdateCampaignCommandHandler
    : ICommandHandler<UpdateCampaignCommand, CampaignResponse>
{
    private readonly ICampaignRepository _campaigns;
    private readonly IDonationCauseRepository _causes;
    private readonly CampaignAccessService _access;
    private readonly CurrencyCatalog _currencies;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCampaignCommandHandler(
        ICampaignRepository campaigns,
        IDonationCauseRepository causes,
        CampaignAccessService access,
        CurrencyCatalog currencies,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _campaigns = campaigns;
        _causes = causes;
        _access = access;
        _currencies = currencies;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CampaignResponse>> Handle(
        UpdateCampaignCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CampaignResponse>(caller.Error);
        }

        var campaign = await _campaigns.FindAsync(
            CampaignId.From(command.CampaignId), cancellationToken);

        if (campaign is null)
        {
            return Result.Failure<CampaignResponse>(Error.NotFound(
                FinancialErrorCodes.CampaignNotFound, "No campaign with that identifier."));
        }

        if (!await _access.CanManageAsync(campaign, caller.Value, cancellationToken))
        {
            return Result.Failure<CampaignResponse>(Error.Forbidden(
                FinancialErrorCodes.CampaignOrganizerForbidden,
                "That campaign is not yours to edit."));
        }

        var now = _clock.UtcNow;
        var facts = await _currencies.FactsAsync(campaign.Currency, cancellationToken);

        var revised = campaign.Revise(
            command.Request.Title,
            command.Request.Summary,
            command.Request.Story,
            string.IsNullOrWhiteSpace(command.Request.Category)
                ? null
                : CampaignMapper.ParseCategory(command.Request.Category),
            command.Request.TargetAmount is { } target
                ? Money.FromAmount(target, campaign.Currency, campaign.Exponent)
                : null,
            command.Request.Deadline,
            command.Request.CoverImage,
            command.Request.Gallery,
            ParseKinds(command.Request.AcceptedKinds),
            Enum.TryParse<ContributionUrgency>(command.Request.Urgency, true, out var urgency)
                && Enum.IsDefined(urgency)
                ? urgency
                : null,
            command.Request.LocationLabel,
            now);

        if (revised.IsFailure)
        {
            return Result.Failure<CampaignResponse>(revised.Error);
        }

        _campaigns.Update(campaign);

        // The cause carries the presentation a donation screen renders, so an edit to a published
        // campaign has to reach it. Leaving them to drift would mean the giving screen showed the
        // story the campaign page no longer does.
        var cause = campaign.CauseId is { } causeId
            ? await _causes.FindAsync(causeId, cancellationToken)
            : null;

        if (cause is not null)
        {
            cause.SetPresentation(
                campaign.Title, campaign.Summary, campaign.Urgency,
                campaign.LocationLabel, campaign.CoverImageReference, now);

            cause.SetTarget(campaign.Target, now);
            cause.SetAcceptedKinds(campaign.AcceptedKindList);
            _causes.Update(cause);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(CampaignMapper.ToResponse(
            campaign, cause, true, now, facts.Symbol, _access.CanDecideReview()));
    }

    private static IReadOnlyList<ContributionKind>? ParseKinds(IReadOnlyList<string>? kinds) =>
        kinds is null
            ? null
            :
            [
                .. kinds
                    .Select(kind => Enum.TryParse<ContributionKind>(kind, true, out var parsed)
                        && Enum.IsDefined(parsed)
                        ? parsed
                        : (ContributionKind?)null)
                    .Where(kind => kind is not null)
                    .Select(kind => kind!.Value)
            ];
}

/// <summary>
/// Moves a campaign through its lifecycle, and creates its cause on publication.
/// <para>
/// <b>The publish branch is the whole invariant.</b> The cause is created and the campaign moved in
/// one unit of work — so there is no ordering, no window and no failure mode in which a campaign
/// says published and has nowhere to collect. If the save fails, neither happened.
/// </para>
/// <para>
/// <b>Who may publish depends on whether the deployment reviews campaigns.</b> With review on, only
/// somebody holding the platform's giving-management permission may move a campaign out of
/// <c>PendingReview</c> — the organizer submits and waits. With it off, the organizer's submit
/// publishes straight through. Either way the transition table decides what is legal and the
/// aggregate answers 409 when it is not.
/// </para>
/// </summary>
public sealed class TransitionCampaignCommandHandler
    : ICommandHandler<TransitionCampaignCommand, CampaignResponse>
{
    private readonly ICampaignRepository _campaigns;
    private readonly IDonationCauseRepository _causes;
    private readonly CampaignAccessService _access;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly FinancialOptions _options;

    public TransitionCampaignCommandHandler(
        ICampaignRepository campaigns,
        IDonationCauseRepository causes,
        CampaignAccessService access,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork,
        IOptions<FinancialOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _campaigns = campaigns;
        _causes = causes;
        _access = access;
        _authorization = authorization;
        _currencies = currencies;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _options = options.Value;
    }

    public async Task<Result<CampaignResponse>> Handle(
        TransitionCampaignCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CampaignResponse>(caller.Error);
        }

        if (!Enum.TryParse<CampaignStatus>(command.Target, true, out var target)
            || !Enum.IsDefined(target))
        {
            return Result.Failure<CampaignResponse>(Error.Validation(
                FinancialErrorCodes.CampaignTransitionInvalid,
                $"'{command.Target}' is not a campaign state."));
        }

        var campaign = await _campaigns.FindAsync(
            CampaignId.From(command.CampaignId), cancellationToken);

        if (campaign is null)
        {
            return Result.Failure<CampaignResponse>(Error.NotFound(
                FinancialErrorCodes.CampaignNotFound, "No campaign with that identifier."));
        }

        var isStaff = _authorization.Has(FinancialPermissions.GivingManage);

        if (!isStaff && !await _access.CanManageAsync(campaign, caller.Value, cancellationToken))
        {
            return Result.Failure<CampaignResponse>(Error.Forbidden(
                FinancialErrorCodes.CampaignOrganizerForbidden,
                "That campaign is not yours to manage."));
        }

        // Publication and rejection are the review decision. Where a deployment reviews campaigns,
        // that decision is staff's — an organizer who could publish their own submission would make
        // the review a formality it costs a round trip to skip.
        if (_options.RequireCampaignReview
            && target is CampaignStatus.Published or CampaignStatus.Rejected
            && !isStaff)
        {
            return Result.Failure<CampaignResponse>(Error.Forbidden(
                FinancialErrorCodes.CampaignOrganizerForbidden,
                "A campaign is published by Diaspora after review, not by its organiser."));
        }

        var now = _clock.UtcNow;
        var facts = await _currencies.FactsAsync(campaign.Currency, cancellationToken);

        DonationCause? cause = campaign.CauseId is { } existing
            ? await _causes.FindAsync(existing, cancellationToken)
            : null;

        if (target == CampaignStatus.Published)
        {
            var published = await PublishAsync(campaign, cause, now, cancellationToken);

            if (published.IsFailure)
            {
                return Result.Failure<CampaignResponse>(published.Error);
            }

            cause = published.Value;
        }
        else
        {
            var moved = campaign.MoveTo(target, now, command.Request?.Reason);

            if (moved.IsFailure)
            {
                return Result.Failure<CampaignResponse>(moved.Error);
            }

            // A cause stops and starts collecting with its campaign. B15's donation path already
            // refuses an inactive cause, so this is what makes "paused" mean the money stops.
            if (cause is not null)
            {
                var shouldCollect = campaign.AcceptsDonations;

                if (shouldCollect && !cause.IsActive)
                {
                    cause.Reopen(now);
                    _causes.Update(cause);
                }
                else if (!shouldCollect && cause.IsActive)
                {
                    cause.Close(now);
                    _causes.Update(cause);
                }
            }
        }

        _campaigns.Update(campaign);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(CampaignMapper.ToResponse(
            campaign, cause, true, now, facts.Symbol, _access.CanDecideReview()));
    }

    /// <summary>
    /// Creates the cause and publishes, together.
    /// <para>
    /// Re-uses an existing cause when the campaign has one — a campaign that was paused and resumed
    /// through <c>Published</c> must not open a second collection target, because the money already
    /// given is on the first.
    /// </para>
    /// </summary>
    private async Task<Result<DonationCause>> PublishAsync(
        Campaign campaign, DonationCause? existing, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var cause = existing;

        if (cause is null)
        {
            var code = CampaignMapper.CauseCodeFor(campaign);

            if (await _causes.ExistsAsync(code, cancellationToken))
            {
                return Result.Failure<DonationCause>(Error.Conflict(
                    FinancialErrorCodes.CauseDuplicate,
                    "A giving cause already exists for that campaign."));
            }

            cause = DonationCause.Open(
                DonationCauseId.From(_guids.NewSequentialGuid()),
                code,
                campaign.Title,
                campaign.Summary,
                campaign.BeneficiaryType,
                campaign.OrganizerName,
                campaign.Currency,
                now,
                campaign.BeneficiaryId);

            cause.SetAcceptedKinds(campaign.AcceptedKindList);
            cause.SetTarget(campaign.Target, now);
            cause.SetPresentation(
                campaign.Title, campaign.Summary, campaign.Urgency,
                campaign.LocationLabel, campaign.CoverImageReference, now);
            cause.SetContact(null, null, campaign.CountryCode, now);

            _causes.Add(cause);
        }
        else if (!cause.IsActive)
        {
            cause.Reopen(now);
            _causes.Update(cause);
        }

        var published = campaign.Publish(cause, now);

        return published.IsFailure
            ? Result.Failure<DonationCause>(published.Error)
            : Result.Success(cause);
    }
}
