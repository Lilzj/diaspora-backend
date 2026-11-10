using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Application.Mapping;
using Diaspora.Modules.CommunityProgramme.Application.Services;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Requests;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;
using Diaspora.Modules.CommunityProgramme.Domain.Capabilities;
using Diaspora.Modules.CommunityProgramme.Domain.Enums;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Memberships;
using Diaspora.Modules.CommunityProgramme.Domain.Policies;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Modules.CommunityProgramme.Domain.ValueObjects;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.CommunityProgramme.Application.Memberships;

/// <summary>
/// Opens or rewrites an organization's membership programme.
/// <para>
/// <b>Not in the register's proposed contract, and added because a read-only surface with no way to
/// create the data behind it is not a production capability.</b> The register proposes three reads —
/// the programme, its tiers, and a discovery page over both — and no write, because the frontend's
/// development adapter seeded them. A backend that shipped only the reads would answer 404 to every
/// one of them for ever.
/// </para>
/// <para>
/// <b>The tier list is replaced wholesale</b>, because the editor is a form that posts the whole set
/// and three separate add/update/remove routes would let a client apply two of the three. What that
/// costs is that a tier omitted from the payload is removed — so this handler refuses to remove one
/// that has members, which is a rule about the world rather than about the aggregate and belongs
/// here.
/// </para>
/// <para>
/// <b>Every price is validated against B15's currency catalogue.</b> The register is explicit that
/// there is to be no second currency source, and §18 that a client's currency is never trusted. An
/// unsupported code is refused rather than stored and rendered as an unknown symbol later.
/// </para>
/// </summary>
[RequirePermission(CommunityProgrammePermissions.ManageProgramme)]
[Transactional(Reason =
    "Opens the programme and writes its tiers, benefits and requirements as one unit. A programme "
    + "that committed without its tiers would be a join page with nothing to join, and the client "
    + "would render it as an organization that takes members and offers nothing.")]
public sealed record SaveMembershipProgrammeCommand : ICommand<MembershipProgrammeResponse>
{
    public required Guid OrganizationId { get; init; }

    public required SaveMembershipProgrammeRequest Request { get; init; }
}

public sealed class SaveMembershipProgrammeCommandValidator
    : AbstractValidator<SaveMembershipProgrammeCommand>
{
    public SaveMembershipProgrammeCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.Summary)
            .MaximumLength(CommunityProgrammeLimits.MaxSummaryLength);

        RuleFor(command => command.Request.Visibility).NotEmpty();

        RuleFor(command => command.Request.Tiers)
            .NotNull()
            .Must(tiers => tiers is { Count: > 0 })
            .WithMessage("A membership programme needs at least one tier.")
            .Must(tiers => tiers is null || tiers.Count <= CommunityProgrammeLimits.MaxTiers)
            .WithMessage($"A programme may have at most {CommunityProgrammeLimits.MaxTiers} tiers.");

        RuleForEach(command => command.Request.Tiers).ChildRules(tier =>
        {
            tier.RuleFor(value => value.Name)
                .NotEmpty()
                .MaximumLength(CommunityProgrammeLimits.MaxNameLength);

            tier.RuleFor(value => value.BillingPeriod).NotEmpty();

            tier.RuleFor(value => value.PriceAmount)
                .GreaterThan(0)
                .When(value => value.PriceAmount is not null)
                .WithMessage("A priced tier costs something. A free tier has no price at all.");

            tier.RuleFor(value => value.Currency)
                .NotEmpty()
                .When(value => value.PriceAmount is not null)
                .WithMessage("A priced tier must name its currency.");

            tier.RuleFor(value => value.Capacity)
                .GreaterThan(0)
                .When(value => value.Capacity is not null);
        });
    }
}

public sealed class SaveMembershipProgrammeCommandHandler
    : ICommandHandler<SaveMembershipProgrammeCommand, MembershipProgrammeResponse>
{
    private readonly IMembershipProgrammeRepository _programmes;
    private readonly IProgrammeMembershipRepository _memberships;
    private readonly CommunityProgrammeAccessService _access;
    private readonly MembershipEligibilityEvaluator _eligibility;
    private readonly CurrencyCatalog _currencies;
    private readonly ProgrammeIdempotency _commit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public SaveMembershipProgrammeCommandHandler(
        IMembershipProgrammeRepository programmes,
        IProgrammeMembershipRepository memberships,
        CommunityProgrammeAccessService access,
        MembershipEligibilityEvaluator eligibility,
        CurrencyCatalog currencies,
        ProgrammeIdempotency commit,
        IUnitOfWork unitOfWork,
        IGuidGenerator guids,
        IClock clock)
    {
        _programmes = programmes;
        _memberships = memberships;
        _access = access;
        _eligibility = eligibility;
        _currencies = currencies;
        _commit = commit;
        _unitOfWork = unitOfWork;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<MembershipProgrammeResponse>> Handle(
        SaveMembershipProgrammeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<MembershipProgrammeResponse>(caller.Error);
        }

        var permitted = await _access.RequireCapabilityAsync(
            command.OrganizationId, OrganizationCapability.ManageTiers, cancellationToken);

        if (permitted.IsFailure)
        {
            return Result.Failure<MembershipProgrammeResponse>(permitted.Error);
        }

        var organization = await _access.FindOrganizationAsync(
            command.OrganizationId, cancellationToken);

        if (organization is null)
        {
            return Result.Failure<MembershipProgrammeResponse>(
                CommunityProgrammeErrors.OrganizationNotFound);
        }

        if (!Enum.TryParse<ProgrammeVisibility>(
            command.Request.Visibility, ignoreCase: true, out var visibility))
        {
            return Result.Failure<MembershipProgrammeResponse>(
                CommunityProgrammeErrors.Validation(
                    $"'{command.Request.Visibility}' is not a visibility this platform recognises."));
        }

        var now = _clock.UtcNow;
        var programme = await _programmes.FindByOrganizationAsync(
            command.OrganizationId, cancellationToken);

        if (programme is null)
        {
            var opened = MembershipProgramme.Open(
                MembershipProgrammeId.From(_guids.NewSequentialGuid()),
                command.OrganizationId,
                command.Request.Summary,
                visibility,
                command.Request.IsOpen,
                command.Request.RequiresApproval,
                now);

            if (opened.IsFailure)
            {
                return Result.Failure<MembershipProgrammeResponse>(opened.Error);
            }

            programme = opened.Value;
            _programmes.Add(programme);
        }
        else
        {
            programme.Describe(
                command.Request.Summary,
                visibility,
                command.Request.IsOpen,
                command.Request.RequiresApproval,
                now);
        }

        var tiers = await BuildTiersAsync(command, programme, cancellationToken);

        if (tiers.IsFailure)
        {
            return Result.Failure<MembershipProgrammeResponse>(tiers.Error);
        }

        var replaced = programme.ReplaceTiers(tiers.Value, now);

        if (replaced.IsFailure)
        {
            return Result.Failure<MembershipProgrammeResponse>(replaced.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        bool IsMet(TierRequirement requirement) => _eligibility.IsMet(requirement, false);

        return Result.Success(CommunityProgrammeMapper.ToResponse(
            programme,
            organization,
            IsMet,
            ProgrammeMembershipStatus.NotAMember,
            myMembershipId: null));
    }

    private async Task<Result<IReadOnlyList<MembershipTier>>> BuildTiersAsync(
        SaveMembershipProgrammeCommand command,
        MembershipProgramme programme,
        CancellationToken cancellationToken)
    {
        var filled = await _memberships.CountByTierAsync(command.OrganizationId, cancellationToken);
        var keptIds = new HashSet<MembershipTierId>();
        var tiers = new List<MembershipTier>();

        foreach (var request in command.Request.Tiers)
        {
            if (!Enum.TryParse<BillingPeriod>(
                request.BillingPeriod, ignoreCase: true, out var billingPeriod))
            {
                return Result.Failure<IReadOnlyList<MembershipTier>>(
                    CommunityProgrammeErrors.Validation(
                        $"'{request.BillingPeriod}' is not a billing period this platform supports."));
            }

            var price = await ResolvePriceAsync(request, cancellationToken);

            if (price.IsFailure)
            {
                return Result.Failure<IReadOnlyList<MembershipTier>>(price.Error);
            }

            var tierId = request.TierId is { } supplied
                ? MembershipTierId.From(supplied)
                : MembershipTierId.From(_guids.NewSequentialGuid());

            keptIds.Add(tierId);

            tiers.Add(MembershipTier.Create(
                tierId,
                request.Name,
                request.Description,
                price.Value,
                billingPeriod,
                request.Capacity,
                request.IsRecommended,
                request.SortOrder,
                BuildBenefits(request),
                BuildRequirements(request)));
        }

        // A tier that has members cannot be dropped. The alternative — dropping it — would leave
        // every membership sold under it pointing at a tier that no longer exists, and the member's
        // own read would render "Membership" with no price and no benefits.
        foreach (var existing in programme.Tiers)
        {
            if (keptIds.Contains(existing.Id))
            {
                continue;
            }

            if (filled.TryGetValue(existing.Id, out var count) && count > 0)
            {
                return Result.Failure<IReadOnlyList<MembershipTier>>(
                    CommunityProgrammeErrors.Validation(
                        $"'{existing.Name}' has {count} member(s) and cannot be removed. "
                        + "Close it to new members instead."));
            }
        }

        return Result.Success<IReadOnlyList<MembershipTier>>(tiers);
    }

    /// <summary>
    /// Turns a requested price into a stored one, or refuses.
    /// <para>
    /// <b>B15's catalogue decides, and this module holds no currency list of its own.</b> The
    /// exponent comes from the same place, so £12.50 is stored as 1250 with an exponent of 2 and
    /// renders identically wherever it is read.
    /// </para>
    /// </summary>
    private async Task<Result<ProgrammePrice?>> ResolvePriceAsync(
        MembershipTierRequest request, CancellationToken cancellationToken)
    {
        if (request.PriceAmount is not { } amount || string.IsNullOrWhiteSpace(request.Currency))
        {
            // Free. Absent rather than zero — the absence is the meaning.
            return Result.Success<ProgrammePrice?>(null);
        }

        if (!await _currencies.IsSupportedAsync(request.Currency, cancellationToken))
        {
            return Result.Failure<ProgrammePrice?>(
                CommunityProgrammeErrors.CurrencyNotSupported(request.Currency));
        }

        var facts = await _currencies.FactsAsync(request.Currency, cancellationToken);

        return Result.Success<ProgrammePrice?>(
            ProgrammePrice.FromAmount(amount, facts.Code, facts.Exponent));
    }

    private IEnumerable<TierBenefit> BuildBenefits(MembershipTierRequest request) =>
        (request.Benefits ?? [])
            .Take(CommunityProgrammeLimits.MaxBenefitsPerTier)
            .Select(benefit => TierBenefit.Create(
                benefit.Id ?? _guids.NewSequentialGuid(),
                benefit.Title,
                benefit.Description,
                benefit.Icon,
                benefit.IsActive));

    private IEnumerable<TierRequirement> BuildRequirements(MembershipTierRequest request) =>
        (request.Requirements ?? [])
            .Take(CommunityProgrammeLimits.MaxRequirementsPerTier)
            .Select((requirement, index) => TierRequirement.Create(
                requirement.Id ?? _guids.NewSequentialGuid(),
                requirement.Label,
                requirement.Description,
                Enum.TryParse<MembershipRequirementKind>(
                    requirement.Kind, ignoreCase: true, out var kind)
                        ? kind
                        : MembershipRequirementKind.Other,
                requirement.ActionRoute,
                requirement.MinimumAge,
                index));
}
