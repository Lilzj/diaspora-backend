using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Requests;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.Financial.Application.Campaigns;

/// <summary>
/// A moderator suspends a live fundraiser, or lifts a suspension. Staff only.
/// <para>
/// <b>The one door staff have onto a running campaign.</b> Until 7 Nov 2026 staff reached a live
/// fundraiser through its organiser's own pause, close and cancel routes, which recorded nothing
/// about why. Those routes now refuse staff, and this is what replaces them: a separate route, on
/// the giving-management permission, that takes a reason or nothing.
/// </para>
/// <para>
/// <b>Suspend, not close.</b> A fraudulent campaign is taken out of public view and stops collecting,
/// and its organiser is shown why — but nothing terminal happens, nothing is refunded and nothing is
/// decided about the money already given. Those are separate decisions with separate owners.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.GivingManage)]
[Transactional(Reason = "The campaign moves and its cause stops collecting in one unit of work.")]
[Auditable(Action = "financial.campaign.moderate")]
public sealed record ModerateCampaignCommand : ICommand<CampaignResponse>
{
    public required Guid CampaignId { get; init; }

    public required ModerateCampaignRequest Request { get; init; }
}

public sealed class ModerateCampaignCommandValidator : AbstractValidator<ModerateCampaignCommand>
{
    public ModerateCampaignCommandValidator()
    {
        RuleFor(command => command.CampaignId).NotEmpty();
        RuleFor(command => command.Request).NotNull();
        RuleFor(command => command.Request.Action).NotEmpty();

        RuleFor(command => command.Request.Reason)
            .NotEmpty()
            .WithMessage("A moderation decision needs a reason.")
            .MaximumLength(FinancialLimits.MaxDescriptionLength);
    }
}

/// <summary>
/// Applies a moderation decision, and keeps the cause in step with it.
/// <para>
/// <b>The cause closes with the suspension.</b> B15's donation path and the recurring sweep both
/// refuse an inactive cause, so closing it is what makes "suspended" mean the money stops — the same
/// mechanism a pause already uses. A restored campaign comes back paused, so its cause stays closed
/// until the organiser resumes it.
/// </para>
/// <para>
/// <b>Nobody moderates a campaign they run.</b> A moderator who organises a fundraiser is its
/// organiser here, and lifting their own suspension would make the suspension a formality.
/// </para>
/// </summary>
public sealed class ModerateCampaignCommandHandler
    : ICommandHandler<ModerateCampaignCommand, CampaignResponse>
{
    private readonly ICampaignRepository _campaigns;
    private readonly IDonationCauseRepository _causes;
    private readonly CampaignAccessService _access;
    private readonly CurrencyCatalog _currencies;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public ModerateCampaignCommandHandler(
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
        ModerateCampaignCommand command, CancellationToken cancellationToken)
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

        if (await _access.CanManageAsync(campaign, caller.Value, cancellationToken))
        {
            return Result.Failure<CampaignResponse>(Error.Forbidden(
                FinancialErrorCodes.CampaignModerationInvalid,
                "A campaign you run is moderated by somebody else at Diaspora."));
        }

        var now = _clock.UtcNow;
        var reason = command.Request.Reason.Trim();

        var decided = command.Request.Action.Trim().ToUpperInvariant() switch
        {
            "SUSPEND" => campaign.Suspend(reason, now),
            "RESTORE" => campaign.Restore(reason, now),
            _ => Result.Failure(Error.Validation(
                FinancialErrorCodes.CampaignModerationInvalid,
                $"'{command.Request.Action}' is not a moderation action. Suspend or Restore."))
        };

        if (decided.IsFailure)
        {
            return Result.Failure<CampaignResponse>(decided.Error);
        }

        var cause = campaign.CauseId is { } causeId
            ? await _causes.FindAsync(causeId, cancellationToken)
            : null;

        if (cause is not null && cause.IsActive != campaign.AcceptsDonations)
        {
            if (campaign.AcceptsDonations)
            {
                cause.Reopen(now);
            }
            else
            {
                cause.Close(now);
            }

            _causes.Update(cause);
        }

        _campaigns.Update(campaign);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(CampaignMapper.ToResponse(
            campaign,
            cause,
            false,
            now,
            await CampaignMapper.SymbolAsync(_currencies, campaign.Currency, cancellationToken),
            _access.CanDecideReview()));
    }
}
