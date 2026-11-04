using System.Globalization;
using System.Text;
using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Application.Mapping;
using Diaspora.Modules.Creator.Contracts.V1.Requests;
using Diaspora.Modules.Creator.Contracts.V1.Responses;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Errors;
using Diaspora.Modules.Creator.Domain.Grants;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Policies;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.Creator.Application.Grants;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// The administrators' grants (UAT, 3 Nov 2026)
//
// Members saw two grants the catalogue seeder wrote at every startup, in every environment, in
// pounds wherever they lived, and took them for the platform's own. The seeder now writes them only
// where a host asks (CreatorOptions.SeedSampleGrants), and an administrator — creator:programme:manage,
// held by Admin and SuperAdmin — posts, edits, publishes, unpublishes and closes grants here, in the
// currency and countries they choose.
//
// The application form is not edited here. A new grant gets the standard form below, the one the
// sample community fund asks, and an edit leaves the form alone so an application already in
// progress answers the questions it started with.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Every grant, published or not, for the administrators' list.</summary>
[Authorize(Permission = CreatorPermissions.ProgrammeManage)]
public sealed record ListGrantsForAdminQuery : IQuery<IReadOnlyList<AdminGrantResponse>>
{
    public int? Take { get; init; }
}

public sealed class ListGrantsForAdminQueryHandler
    : IQueryHandler<ListGrantsForAdminQuery, IReadOnlyList<AdminGrantResponse>>
{
    private readonly ICommunityGrantRepository _grants;
    private readonly IClock _clock;

    public ListGrantsForAdminQueryHandler(ICommunityGrantRepository grants, IClock clock)
    {
        _grants = grants;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<AdminGrantResponse>>> Handle(
        ListGrantsForAdminQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var grants = await _grants.ListAllAsync(CreatorLimits.PageSize(query.Take), cancellationToken);
        var now = _clock.UtcNow;

        return Result.Success<IReadOnlyList<AdminGrantResponse>>(
            grants.Select(grant => grant.ToAdminResponse(now)).ToList());
    }
}

/// <summary>Posts a grant, or prepares one unpublished.</summary>
[Authorize(Permission = CreatorPermissions.ProgrammeManage)]
[Auditable(Action = "creator.grant.create")]
public sealed record CreateGrantCommand : ICommand<AdminGrantResponse>
{
    public required SaveGrantRequest Request { get; init; }
}

/// <summary>Edits what a grant offers and to whom. Its form is left alone.</summary>
[Authorize(Permission = CreatorPermissions.ProgrammeManage)]
[Auditable(Action = "creator.grant.update")]
public sealed record UpdateGrantCommand : ICommand<AdminGrantResponse>
{
    public required Guid GrantId { get; init; }

    public required SaveGrantRequest Request { get; init; }
}

/// <summary>What an administrator does to a grant's window.</summary>
public enum GrantWindowAction
{
    Publish,
    Unpublish,
    Close
}

/// <summary>Publishes, unpublishes or closes a grant.</summary>
[Authorize(Permission = CreatorPermissions.ProgrammeManage)]
[Auditable(Action = "creator.grant.window")]
public sealed record SetGrantWindowCommand : ICommand<AdminGrantResponse>
{
    public required Guid GrantId { get; init; }

    public required GrantWindowAction Action { get; init; }
}

/// <summary>The rules both saves share.</summary>
public sealed class SaveGrantRequestValidator : AbstractValidator<SaveGrantRequest>
{
    public SaveGrantRequestValidator()
    {
        RuleFor(request => request.Title).NotEmpty().MaximumLength(160);
        RuleFor(request => request.Summary).NotEmpty().MaximumLength(400);
        RuleFor(request => request.Description).NotEmpty().MaximumLength(4000);
        RuleFor(request => request.Category).NotEmpty().MaximumLength(60);
        RuleFor(request => request.FundingMinorUnits).GreaterThan(0);

        RuleFor(request => request.FundingMinimumMinorUnits)
            .GreaterThan(0)
            .LessThan(request => request.FundingMinorUnits)
            .When(request => request.FundingMinimumMinorUnits is not null)
            .WithMessage("The minimum must be above zero and below the most the grant awards.");

        RuleFor(request => request.Currency)
            .NotEmpty()
            .Matches("^[A-Za-z]{3}$")
            .WithMessage("The currency is a three-letter code, such as NGN or GBP.");

        RuleForEach(request => request.CountryCodes)
            .Matches("^[A-Za-z]{2}$")
            .WithMessage("Each country is a two-letter code, such as NG or GH.");

        RuleFor(request => request.DeadlineAt)
            .GreaterThan(request => request.OpensAt)
            .When(request => request.OpensAt is not null && request.DeadlineAt is not null)
            .WithMessage("The deadline is after the opening.");

        RuleFor(request => request.MinimumStatus)
            .Must(status => status is null || GrantAdmin.TryParseStatus(status, out _))
            .WithMessage("The minimum status is one of NotACreator, Contributor, EmergingCreator, "
                + "VerifiedCreator, EstablishedCreator or TopCreator.");
    }
}

public sealed class CreateGrantCommandValidator : AbstractValidator<CreateGrantCommand>
{
    public CreateGrantCommandValidator()
    {
        RuleFor(command => command.Request).NotNull().SetValidator(new SaveGrantRequestValidator());
    }
}

public sealed class UpdateGrantCommandValidator : AbstractValidator<UpdateGrantCommand>
{
    public UpdateGrantCommandValidator()
    {
        RuleFor(command => command.GrantId).NotEmpty();
        RuleFor(command => command.Request).NotNull().SetValidator(new SaveGrantRequestValidator());
    }
}

public sealed class CreateGrantCommandHandler : ICommandHandler<CreateGrantCommand, AdminGrantResponse>
{
    private readonly ICommunityGrantRepository _grants;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CreateGrantCommandHandler(
        ICommunityGrantRepository grants, IUnitOfWork unitOfWork, IGuidGenerator guids, IClock clock)
    {
        _grants = grants;
        _unitOfWork = unitOfWork;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<AdminGrantResponse>> Handle(
        CreateGrantCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;
        var id = _guids.NewSequentialGuid();

        GrantAdmin.TryParseStatus(request.MinimumStatus, out var minimumStatus);

        var grant = CommunityGrant
            .Define(
                CommunityGrantId.From(id),
                GrantAdmin.KeyFor(request.Title!, id),
                request.Title!,
                request.Summary!,
                request.Description!,
                request.Category!.Trim().ToLowerInvariant(),
                request.FundingMinorUnits,
                request.Currency!,
                request.OpensAt,
                request.DeadlineAt,
                allowsAmountRequest: request.FundingMinimumMinorUnits is not null,
                minimumStatus: minimumStatus,
                requiresVerifiedCreator: request.RequiresVerifiedCreator)
            .WithField("project-name", "What is it called?", GrantFieldKind.Text, true, maxLength: 120)
            .WithField("summary", "What will you do with it?", GrantFieldKind.LongText, true,
                "A few sentences. What happens, for whom, and where.", 2_000)
            .WithField("budget", "How will you spend it?", GrantFieldKind.LongText, true,
                "A rough breakdown is fine.", 2_000)
            .WithDeclaration("Everything I have written here is true as far as I know.")
            .WithDeliverable("A short report on what happened within three months of the award.")
            .InCountries([.. request.CountryCodes ?? []]);

        if (request.FundingMinimumMinorUnits is { } minimum)
        {
            grant.WithRange(minimum, request.FundingMinorUnits);
        }

        if (request.Publish)
        {
            grant.Publish();
        }

        _grants.Add(grant);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(grant.ToAdminResponse(_clock.UtcNow));
    }
}

public sealed class UpdateGrantCommandHandler : ICommandHandler<UpdateGrantCommand, AdminGrantResponse>
{
    private readonly ICommunityGrantRepository _grants;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public UpdateGrantCommandHandler(ICommunityGrantRepository grants, IUnitOfWork unitOfWork, IClock clock)
    {
        _grants = grants;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<AdminGrantResponse>> Handle(
        UpdateGrantCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var grant = await _grants.GetAsync(CommunityGrantId.From(command.GrantId), cancellationToken);

        if (grant is null)
        {
            return Result.Failure<AdminGrantResponse>(CreatorErrors.GrantNotFound);
        }

        var request = command.Request;
        GrantAdmin.TryParseStatus(request.MinimumStatus, out var minimumStatus);

        grant.Revise(
            request.Title!,
            request.Summary!,
            request.Description!,
            request.Category!.Trim().ToLowerInvariant(),
            request.FundingMinorUnits,
            request.FundingMinimumMinorUnits,
            request.Currency!,
            request.OpensAt,
            request.DeadlineAt,
            minimumStatus,
            request.RequiresVerifiedCreator,
            request.CountryCodes ?? []);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(grant.ToAdminResponse(_clock.UtcNow));
    }
}

public sealed class SetGrantWindowCommandHandler : ICommandHandler<SetGrantWindowCommand, AdminGrantResponse>
{
    private readonly ICommunityGrantRepository _grants;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public SetGrantWindowCommandHandler(ICommunityGrantRepository grants, IUnitOfWork unitOfWork, IClock clock)
    {
        _grants = grants;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<AdminGrantResponse>> Handle(
        SetGrantWindowCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var grant = await _grants.GetAsync(CommunityGrantId.From(command.GrantId), cancellationToken);

        if (grant is null)
        {
            return Result.Failure<AdminGrantResponse>(CreatorErrors.GrantNotFound);
        }

        var now = _clock.UtcNow;

        switch (command.Action)
        {
            case GrantWindowAction.Publish:
                grant.Publish();
                break;
            case GrantWindowAction.Unpublish:
                grant.Unpublish();
                break;
            case GrantWindowAction.Close:
                grant.CloseAt(now);
                break;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(grant.ToAdminResponse(now));
    }
}

/// <summary>What both saves need.</summary>
public static class GrantAdmin
{
    /// <summary>The statuses a grant may ask for. A sanction is never a threshold.</summary>
    private static readonly CreatorStatus[] Thresholds =
    [
        CreatorStatus.NotACreator,
        CreatorStatus.Contributor,
        CreatorStatus.EmergingCreator,
        CreatorStatus.VerifiedCreator,
        CreatorStatus.EstablishedCreator,
        CreatorStatus.TopCreator
    ];

    /// <summary>Contributor when absent, the sample fund's default.</summary>
    public static bool TryParseStatus(string? value, out CreatorStatus status)
    {
        status = CreatorStatus.Contributor;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (Enum.TryParse<CreatorStatus>(value.Trim(), ignoreCase: true, out var parsed)
            && Thresholds.Contains(parsed))
        {
            status = parsed;
            return true;
        }

        return false;
    }

    /// <summary>"Lagos newcomers fund" → <c>lagos-newcomers-fund-1a2b3c</c>: readable, and unique.</summary>
    public static string KeyFor(string title, Guid id)
    {
        var slug = new StringBuilder();

        foreach (var character in title.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                slug.Append(character);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }

            if (slug.Length >= 48)
            {
                break;
            }
        }

        var stem = slug.ToString().Trim('-');
        var suffix = id.ToString("N", CultureInfo.InvariantCulture)[^6..];

        return stem.Length == 0 ? $"grant-{suffix}" : $"{stem}-{suffix}";
    }

    public static AdminGrantResponse ToAdminResponse(this CommunityGrant grant, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(grant);

        var form = grant.ToFormResponse();

        return new AdminGrantResponse
        {
            GrantId = grant.Id.Value,
            Key = grant.Key,
            Title = grant.Title,
            Summary = grant.Summary,
            Description = grant.Description,
            Category = grant.Category,
            State = grant.StateAt(now).ToString(),
            IsPublished = grant.IsPublished,
            FundingAmount = CreatorMapper.ToMoney(grant.FundingMinorUnits, grant.Currency, grant.Exponent),
            FundingMinimum = grant.FundingMinimumMinorUnits is { } minimum
                ? CreatorMapper.ToMoney(minimum, grant.Currency, grant.Exponent)
                : null,
            OpensAt = grant.OpensAt,
            DeadlineAt = grant.DeadlineAt,
            MinimumStatus = grant.MinimumStatus.ToString(),
            RequiresVerifiedCreator = grant.RequiresVerifiedCreator,
            ApplicantCount = grant.ApplicantCount,
            CountryCodes = grant.Countries.Select(country => country.CountryCode).ToList(),
            Fields = form.Fields
        };
    }
}
