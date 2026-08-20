using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Requests;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using FluentValidation;

namespace Diaspora.Modules.Financial.Application.Campaigns;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// COMMUNITY-LIFE-GAP-006 and -007 — the campaign requests.
//
// Every write is gated on `financial:giving:create`, which the permission catalogue already places
// at AccessTier.Verified. That is the register's rule for a personal campaign — "AccessTier.Verified
// for a personal one" — and it is inherited rather than restated, so a deployment that moved the
// giving bar would move this one with it. An organization campaign clears that bar and then a second
// one: canManageCampaigns inside the body, asked of the module that owns the role edge.
//
// Every read is gated on `financial:giving:read`, which survives account restriction. Somebody under
// review may still look at a campaign.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Creates a campaign, in draft. COMMUNITY-LIFE-GAP-006.</summary>
[Authorize(Permission = FinancialPermissions.GivingCreate)]
[Transactional(Reason = "A campaign is created and its idempotency key recorded together.")]
[Auditable(Action = "financial.campaign.create")]
public sealed record CreateCampaignCommand : ICommand<CampaignResponse>
{
    public required CreateCampaignRequest Request { get; init; }
}

public sealed class CreateCampaignCommandValidator : AbstractValidator<CreateCampaignCommand>
{
    public CreateCampaignCommandValidator()
    {
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.Title)
            .NotEmpty()
            .MaximumLength(160);

        RuleFor(command => command.Request.Summary)
            .NotEmpty()
            .MaximumLength(1024);

        RuleFor(command => command.Request.Story)
            .NotEmpty()
            .MaximumLength(8192);

        RuleFor(command => command.Request.Category).NotEmpty();

        RuleFor(command => command.Request.Currency)
            .NotEmpty()
            .Length(3);

        RuleFor(command => command.Request.BeneficiaryType).NotEmpty();

        RuleFor(command => command.Request.BeneficiaryName)
            .NotEmpty()
            .MaximumLength(160);

        RuleFor(command => command.Request.TargetAmount)
            .GreaterThan(0m)
            .When(command => command.Request.TargetAmount is not null)
            .WithMessage("A target of nothing is not a target.");

        RuleFor(command => command.Request.IdempotencyKey).MaximumLength(256);
    }
}

/// <summary>Edits a campaign's own text. Nothing financial moves through it.</summary>
[Authorize(Permission = FinancialPermissions.GivingCreate)]
[Transactional(Reason = "A campaign edit is one write with its concurrency token.")]
[Auditable(Action = "financial.campaign.update")]
public sealed record UpdateCampaignCommand : ICommand<CampaignResponse>
{
    public required Guid CampaignId { get; init; }

    public required UpdateCampaignRequest Request { get; init; }
}

public sealed class UpdateCampaignCommandValidator : AbstractValidator<UpdateCampaignCommand>
{
    public UpdateCampaignCommandValidator()
    {
        RuleFor(command => command.CampaignId).NotEmpty();
        RuleFor(command => command.Request).NotNull();
        RuleFor(command => command.Request.Title).MaximumLength(160);
        RuleFor(command => command.Request.Summary).MaximumLength(1024);
        RuleFor(command => command.Request.Story).MaximumLength(8192);

        RuleFor(command => command.Request.TargetAmount)
            .GreaterThan(0m)
            .When(command => command.Request.TargetAmount is not null);
    }
}

/// <summary>
/// Moves a campaign through its lifecycle. COMMUNITY-LIFE-GAP-006.
/// <para>
/// <b>One command for six routes.</b> Submit, publish, pause, resume, close and cancel are one
/// operation with a destination, and writing six handlers would be six places the transition table
/// is consulted — five of which would eventually stop being consulted the same way. The route names
/// the destination; the aggregate decides whether it is legal and answers 409 when it is not.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.GivingCreate)]
[Transactional(
    Reason = "Publishing writes the campaign and creates its donation cause in one unit of work.")]
[Auditable(Action = "financial.campaign.transition")]
public sealed record TransitionCampaignCommand : ICommand<CampaignResponse>
{
    public required Guid CampaignId { get; init; }

    /// <summary>The destination, named by the route rather than by the client's body.</summary>
    public required string Target { get; init; }

    public CampaignTransitionRequest? Request { get; init; }
}

public sealed class TransitionCampaignCommandValidator
    : AbstractValidator<TransitionCampaignCommand>
{
    public TransitionCampaignCommandValidator()
    {
        RuleFor(command => command.CampaignId).NotEmpty();
        RuleFor(command => command.Target).NotEmpty();
        RuleFor(command => command.Request!.Reason)
            .MaximumLength(1024)
            .When(command => command.Request is not null);
    }
}

/// <summary>One campaign, with everything the detail screen needs.</summary>
[Authorize(Permission = FinancialPermissions.GivingRead)]
public sealed record GetCampaignQuery : IQuery<CampaignResponse>
{
    public required Guid CampaignId { get; init; }
}

/// <summary>Campaigns a member can browse, cursor-paged.</summary>
[Authorize(Permission = FinancialPermissions.GivingRead)]
public sealed record SearchCampaignsQuery : IQuery<GivingPageResponse<CampaignResponse>>
{
    public string? State { get; init; }

    public string? Category { get; init; }

    public string? CountryCode { get; init; }

    public Guid? OrganizationId { get; init; }

    /// <summary>Restricts to the caller's own campaigns, drafts included.</summary>
    public bool MineOnly { get; init; }

    public string? Cursor { get; init; }

    public int? Take { get; init; }
}

// ── GAP-007 · Transparency ───────────────────────────────────────────────────────────────────────

/// <summary>Publishes an update to a campaign's supporters.</summary>
[Authorize(Permission = FinancialPermissions.GivingCreate)]
[Transactional(Reason = "The update is written and announced together.")]
[Auditable(Action = "financial.campaign.update.publish")]
public sealed record PublishCampaignUpdateCommand : ICommand<CampaignUpdateResponse>
{
    public required Guid CampaignId { get; init; }

    public required CreateCampaignUpdateRequest Request { get; init; }
}

public sealed class PublishCampaignUpdateCommandValidator
    : AbstractValidator<PublishCampaignUpdateCommand>
{
    public PublishCampaignUpdateCommandValidator()
    {
        RuleFor(command => command.CampaignId).NotEmpty();
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.Title)
            .NotEmpty()
            .MaximumLength(160);

        RuleFor(command => command.Request.Body)
            .NotEmpty()
            .MaximumLength(8192);

        RuleFor(command => command.Request.IdempotencyKey).MaximumLength(256);
    }
}

/// <summary>A campaign's timeline, newest first, cursor-paged.</summary>
[Authorize(Permission = FinancialPermissions.GivingRead)]
public sealed record GetCampaignUpdatesQuery : IQuery<GivingPageResponse<CampaignUpdateResponse>>
{
    public required Guid CampaignId { get; init; }

    public string? Cursor { get; init; }

    public int? Take { get; init; }
}

/// <summary>Declares a campaign's milestones. Whether they are reached is never the caller's.</summary>
[Authorize(Permission = FinancialPermissions.GivingCreate)]
[Transactional(Reason = "Milestones are replaced and re-evaluated in one write.")]
[Auditable(Action = "financial.campaign.milestones.set")]
public sealed record SetCampaignMilestonesCommand : ICommand<IReadOnlyList<CampaignMilestoneResponse>>
{
    public required Guid CampaignId { get; init; }

    public required SetCampaignMilestonesRequest Request { get; init; }
}

public sealed class SetCampaignMilestonesCommandValidator
    : AbstractValidator<SetCampaignMilestonesCommand>
{
    public SetCampaignMilestonesCommandValidator()
    {
        RuleFor(command => command.CampaignId).NotEmpty();
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.Milestones)
            .NotNull()
            .Must(milestones => milestones.Count <= 20)
            .WithMessage("Twenty milestones is already more than anybody will read.");

        RuleForEach(command => command.Request.Milestones).ChildRules(milestone =>
        {
            milestone.RuleFor(row => row.Label).NotEmpty().MaximumLength(128);
            milestone.RuleFor(row => row.TargetAmount).GreaterThan(0m);
        });
    }
}

/// <summary>A campaign's milestones, with the server's own reached-ness.</summary>
[Authorize(Permission = FinancialPermissions.GivingRead)]
public sealed record GetCampaignMilestonesQuery : IQuery<IReadOnlyList<CampaignMilestoneResponse>>
{
    public required Guid CampaignId { get; init; }
}

/// <summary>
/// A campaign's supporters, already anonymised. Cursor-paged.
/// <para>
/// The anonymisation happens in the handler, from a record that never held an identity for the
/// anonymous ones. There is nothing here for a client-side filter to have to hide.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.GivingRead)]
public sealed record GetCampaignSupportersQuery
    : IQuery<GivingPageResponse<CampaignSupporterResponse>>
{
    public required Guid CampaignId { get; init; }

    public string? Cursor { get; init; }

    public int? Take { get; init; }
}

/// <summary>Declares how a campaign's money is allocated, and reports what has been spent.</summary>
[Authorize(Permission = FinancialPermissions.GivingCreate)]
[Transactional(Reason = "The allocation lines are replaced in one write.")]
[Auditable(Action = "financial.campaign.fund_usage.set")]
public sealed record SetCampaignFundUsageCommand : ICommand<CampaignFundUsageResponse>
{
    public required Guid CampaignId { get; init; }

    public required SetCampaignFundUsageRequest Request { get; init; }
}

public sealed class SetCampaignFundUsageCommandValidator
    : AbstractValidator<SetCampaignFundUsageCommand>
{
    public SetCampaignFundUsageCommandValidator()
    {
        RuleFor(command => command.CampaignId).NotEmpty();
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.Lines)
            .NotNull()
            .Must(lines => lines.Count <= 50)
            .WithMessage("Fifty allocation lines is a spreadsheet, not a summary.");

        RuleForEach(command => command.Request.Lines).ChildRules(line =>
        {
            line.RuleFor(row => row.Label).NotEmpty().MaximumLength(128);
            line.RuleFor(row => row.Allocated).GreaterThanOrEqualTo(0m);
            line.RuleFor(row => row.Spent).GreaterThanOrEqualTo(0m).When(row => row.Spent is not null);
            line.RuleFor(row => row.Note).MaximumLength(512);
        });
    }
}

/// <summary>Where a campaign's money is. Four figures with four different authorities.</summary>
[Authorize(Permission = FinancialPermissions.GivingRead)]
public sealed record GetCampaignFundUsageQuery : IQuery<CampaignFundUsageResponse>
{
    public required Guid CampaignId { get; init; }
}

/// <summary>
/// A cause's giving over a window, bucketed by UTC day.
/// <para>
/// <b>UTC, and the response says so.</b> A platform spanning eleven time zones has no single local
/// midnight to group on; picking the caller's would make two people disagree about which day a
/// donation happened on, and picking the organiser's would make the chart change when they travel.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.GivingRead)]
public sealed record GetDonationTimeSeriesQuery : IQuery<DonationTimeSeriesResponse>
{
    public required string CauseCode { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }
}
