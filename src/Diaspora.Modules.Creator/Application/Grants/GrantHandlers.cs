using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Application.Mapping;
using Diaspora.Modules.Creator.Application.Services;
using Diaspora.Modules.Creator.Contracts.V1.Requests;
using Diaspora.Modules.Creator.Contracts.V1.Responses;
using Diaspora.Modules.Creator.Domain.Creators;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Errors;
using Diaspora.Modules.Creator.Domain.Grants;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Policies;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Modules.Creator.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.Creator.Application.Grants;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// CREATOR-GAP-005 — community grants.
//
// Eight member routes plus the reviewer's. The register's two hard requirements are both structural
// here rather than conventional:
//
//   The form is served (GET /grants/{id}/form) and the same rows validate the submission, so adding
//   a question is a seed change rather than an app release.
//
//   No decision is ever made on the device. Approve, reject and complete need
//   creator:grant:review, which an applicant does not hold — and a reviewer is refused their own
//   application even when they do.
//
// Money is B15's throughout. This module records an awarded amount and publishes it; the funding leg
// is documented in Application/Financial/GrantFundingBoundary.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Whether one creator may apply to one grant, and the requirements behind the answer.
/// <para>
/// Static and stateless, for the reason <c>ChallengeEligibility</c> is: the discovery list, the
/// detail read and the submit command need one answer, not three.
/// </para>
/// </summary>
public static class GrantEligibility
{
    public static (bool IsEligible, string? Reason, IReadOnlyList<CreatorRequirement> Requirements)
        Evaluate(CommunityGrant grant, CreatorProfile? creator, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(grant);

        var requirements = new List<CreatorRequirement>
        {
            CreatorRequirement.Flag(
                "grant-open",
                "The grant is accepting applications",
                grant.IsAcceptingApplications(now)),

            CreatorRequirement.Flag(
                "creator-status",
                $"Be at least {CreatorStatusWords.WithArticle(grant.MinimumStatus)}",
                creator is not null && creator.EarnedStatus >= grant.MinimumStatus,
                actionRoute: "/creator/profile",
                actionLabel: "See your status"),

            CreatorRequirement.Flag(
                "good-standing",
                "Keep your account in good standing",
                creator is not null && !creator.IsRestricted && !creator.IsSuspended)
        };

        if (grant.RequiresVerifiedCreator)
        {
            requirements.Add(CreatorRequirement.Flag(
                "verified-creator",
                "Be a verified creator",
                creator?.IsVerifiedCreator ?? false,
                "The creator programme verification, which is separate from identity verification.",
                "/creator/verification",
                "Apply"));
        }

        if (grant.Countries.Count > 0)
        {
            requirements.Add(CreatorRequirement.Flag(
                "country",
                "Be in a country this grant runs in",
                creator?.CountryCode is { } country
                && grant.Countries.Any(entry =>
                    string.Equals(entry.CountryCode, country, StringComparison.Ordinal))));
        }

        var unmet = requirements.Find(requirement => !requirement.IsMet);

        return (unmet is null, unmet?.Label, requirements);
    }
}

/// <summary>
/// A creator status in a sentence: "a verified creator", "an emerging creator". The requirement used
/// to print the enum's name — "Be at least a VerifiedCreator" (UAT, 3 Nov 2026).
/// </summary>
public static class CreatorStatusWords
{
    public static string WithArticle(CreatorStatus status)
    {
        var words = status switch
        {
            CreatorStatus.NotACreator => "member",
            CreatorStatus.Contributor => "contributor",
            CreatorStatus.EmergingCreator => "emerging creator",
            CreatorStatus.VerifiedCreator => "verified creator",
            CreatorStatus.EstablishedCreator => "established creator",
            CreatorStatus.TopCreator => "top creator",
            CreatorStatus.PendingVerification => "creator pending verification",
            _ => status.ToString().ToLowerInvariant()
        };

        return "aeiou".Contains(words[0], StringComparison.Ordinal) ? $"an {words}" : $"a {words}";
    }
}

/// <summary>Grant discovery.</summary>
[Authorize(Permission = CreatorPermissions.ProfileRead)]
public sealed record ListGrantsQuery : IQuery<IReadOnlyList<CommunityGrantResponse>>
{
    public string? Category { get; init; }

    public string? CountryCode { get; init; }

    public string? State { get; init; }

    public int? Take { get; init; }
}

public sealed class ListGrantsQueryHandler
    : IQueryHandler<ListGrantsQuery, IReadOnlyList<CommunityGrantResponse>>
{
    private readonly CreatorContext _context;
    private readonly ICommunityGrantRepository _grants;
    private readonly IGrantApplicationRepository _applications;
    private readonly IClock _clock;

    public ListGrantsQueryHandler(
        CreatorContext context,
        ICommunityGrantRepository grants,
        IGrantApplicationRepository applications,
        IClock clock)
    {
        _context = context;
        _grants = grants;
        _applications = applications;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<CommunityGrantResponse>>> Handle(
        ListGrantsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<CommunityGrantResponse>>(caller.Error);
        }

        var now = _clock.UtcNow;
        var take = CreatorLimits.PageSize(query.Take);

        var grants = await _grants.ListAsync(
            query.Category, query.CountryCode, take, cancellationToken);

        var creator = await _context.FindCreatorAsync(cancellationToken);

        var mine = creator is null
            ? []
            : (await _applications.ListForCreatorAsync(creator.Id, cancellationToken))
                .ToDictionary(application => application.GrantId);

        // One state or several, comma-separated: the app's Open tab asks for "Open,ClosingSoon",
        // the grants taking applications, where it used to list every published grant.
        var wanted = (query.State ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => Enum.TryParse<CommunityGrantState>(value, ignoreCase: true, out var parsed)
                ? parsed
                : (CommunityGrantState?)null)
            .OfType<CommunityGrantState>()
            .ToHashSet();

        var response = grants
            .Where(grant => wanted.Count == 0 || wanted.Contains(grant.StateAt(now)))
            .Select(grant =>
            {
                mine.TryGetValue(grant.Id, out var application);

                var (eligible, reason, requirements) =
                    GrantEligibility.Evaluate(grant, creator, now);

                return grant.ToResponse(application, requirements, eligible, reason, now);
            })
            .ToList();

        return Result.Success<IReadOnlyList<CommunityGrantResponse>>(response);
    }
}

/// <summary>One grant.</summary>
[Authorize(Permission = CreatorPermissions.ProfileRead)]
public sealed record GetGrantQuery : IQuery<CommunityGrantResponse>
{
    public required Guid GrantId { get; init; }
}

public sealed class GetGrantQueryHandler : IQueryHandler<GetGrantQuery, CommunityGrantResponse>
{
    private readonly CreatorContext _context;
    private readonly ICommunityGrantRepository _grants;
    private readonly IGrantApplicationRepository _applications;
    private readonly IClock _clock;

    public GetGrantQueryHandler(
        CreatorContext context,
        ICommunityGrantRepository grants,
        IGrantApplicationRepository applications,
        IClock clock)
    {
        _context = context;
        _grants = grants;
        _applications = applications;
        _clock = clock;
    }

    public async Task<Result<CommunityGrantResponse>> Handle(
        GetGrantQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var grant = await _grants.GetAsync(CommunityGrantId.From(query.GrantId), cancellationToken);

        if (grant is null || !grant.IsPublished)
        {
            return Result.Failure<CommunityGrantResponse>(CreatorErrors.GrantNotFound);
        }

        var creator = await _context.FindCreatorAsync(cancellationToken);

        var application = creator is null
            ? null
            : await _applications.FindForCreatorAsync(grant.Id, creator.Id, cancellationToken);

        var now = _clock.UtcNow;
        var (eligible, reason, requirements) = GrantEligibility.Evaluate(grant, creator, now);

        return Result.Success(grant.ToResponse(application, requirements, eligible, reason, now));
    }
}

/// <summary>
/// The served application form.
/// <para>
/// The register calls this load-bearing, and it is: the client renders whatever it is handed and the
/// server validates against the same rows. Adding a question is a seed change.
/// </para>
/// </summary>
[Authorize(Permission = CreatorPermissions.ProfileRead)]
public sealed record GetGrantFormQuery : IQuery<GrantApplicationFormResponse>
{
    public required Guid GrantId { get; init; }
}

public sealed class GetGrantFormQueryHandler
    : IQueryHandler<GetGrantFormQuery, GrantApplicationFormResponse>
{
    private readonly ICommunityGrantRepository _grants;

    public GetGrantFormQueryHandler(ICommunityGrantRepository grants) => _grants = grants;

    public async Task<Result<GrantApplicationFormResponse>> Handle(
        GetGrantFormQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var grant = await _grants.GetAsync(CommunityGrantId.From(query.GrantId), cancellationToken);

        return grant is null || !grant.IsPublished
            ? Result.Failure<GrantApplicationFormResponse>(CreatorErrors.GrantNotFound)
            : Result.Success(grant.ToFormResponse());
    }
}

/// <summary>The caller's own applications.</summary>
[Authorize(Permission = CreatorPermissions.ProfileRead)]
public sealed record ListMyGrantApplicationsQuery : IQuery<IReadOnlyList<GrantApplicationResponse>>;

public sealed class ListMyGrantApplicationsQueryHandler
    : IQueryHandler<ListMyGrantApplicationsQuery, IReadOnlyList<GrantApplicationResponse>>
{
    private readonly CreatorContext _context;
    private readonly IGrantApplicationRepository _applications;
    private readonly ICommunityGrantRepository _grants;
    private readonly IClock _clock;

    public ListMyGrantApplicationsQueryHandler(
        CreatorContext context,
        IGrantApplicationRepository applications,
        ICommunityGrantRepository grants,
        IClock clock)
    {
        _context = context;
        _applications = applications;
        _grants = grants;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<GrantApplicationResponse>>> Handle(
        ListMyGrantApplicationsQuery query, CancellationToken cancellationToken)
    {
        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<GrantApplicationResponse>>(caller.Error);
        }

        var creator = await _context.FindCreatorAsync(cancellationToken);

        if (creator is null)
        {
            return Result.Success<IReadOnlyList<GrantApplicationResponse>>([]);
        }

        var applications = await _applications.ListForCreatorAsync(creator.Id, cancellationToken);
        var now = _clock.UtcNow;
        var response = new List<GrantApplicationResponse>(applications.Count);

        foreach (var application in applications)
        {
            var grant = await _grants.GetAsync(application.GrantId, cancellationToken);

            if (grant is not null)
            {
                response.Add(application.ToResponse(grant, now));
            }
        }

        return Result.Success<IReadOnlyList<GrantApplicationResponse>>(response);
    }
}

/// <summary>
/// One grant application, by id.
/// <para>
/// <b>The applicant's, and the review panel's, and nobody else's.</b> Added 21 Oct 2026 for the
/// app's application screen. Anybody else is answered exactly as a missing application is — the
/// reason <see cref="WithdrawGrantApplicationCommand"/> already records: a 403 would confirm to
/// somebody probing for other people's applications that the identifier is real.
/// </para>
/// <para>
/// <b>A reviewer sees what has been filed, never a draft.</b> A draft is the applicant's unfinished
/// work; the panel's interest begins at submission, which is where its default queue begins too.
/// A reviewer reading their own application is answered as its applicant.
/// </para>
/// </summary>
[Authorize(Permission = CreatorPermissions.ProfileRead)]
public sealed record GetGrantApplicationQuery : IQuery<GrantApplicationResponse>
{
    public required Guid ApplicationId { get; init; }
}

public sealed class GetGrantApplicationQueryHandler
    : IQueryHandler<GetGrantApplicationQuery, GrantApplicationResponse>
{
    private readonly CreatorContext _context;
    private readonly IGrantApplicationRepository _applications;
    private readonly ICommunityGrantRepository _grants;
    private readonly IClock _clock;

    public GetGrantApplicationQueryHandler(
        CreatorContext context,
        IGrantApplicationRepository applications,
        ICommunityGrantRepository grants,
        IClock clock)
    {
        _context = context;
        _applications = applications;
        _grants = grants;
        _clock = clock;
    }

    public async Task<Result<GrantApplicationResponse>> Handle(
        GetGrantApplicationQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<GrantApplicationResponse>(caller.Error);
        }

        if (query.ApplicationId == Guid.Empty)
        {
            return Result.Failure<GrantApplicationResponse>(CreatorErrors.GrantApplicationNotFound);
        }

        var application = await _applications.GetAsync(
            GrantApplicationId.From(query.ApplicationId), cancellationToken);

        if (application is null)
        {
            return Result.Failure<GrantApplicationResponse>(CreatorErrors.GrantApplicationNotFound);
        }

        var isApplicant = application.ApplicantUserId == caller.Value;

        var isPanel = !isApplicant
            && _context.IsGrantReviewer()
            && application.State != GrantApplicationState.Draft;

        if (!isApplicant && !isPanel)
        {
            return Result.Failure<GrantApplicationResponse>(CreatorErrors.GrantApplicationNotFound);
        }

        var grant = await _grants.GetAsync(application.GrantId, cancellationToken);

        return grant is null
            ? Result.Failure<GrantApplicationResponse>(CreatorErrors.GrantNotFound)
            : Result.Success(application.ToResponse(grant, _clock.UtcNow));
    }
}

/// <summary>
/// Starts or updates a draft application.
/// <para>
/// One live application per creator per grant, which the unique index enforces and this handler
/// reads first — so a second POST edits the first rather than colliding.
/// </para>
/// </summary>
[Authorize(Permission = CreatorPermissions.Participate)]
[Auditable(Action = "creator.grant.draft")]
public sealed record SaveGrantApplicationCommand : ICommand<GrantApplicationResponse>
{
    public required Guid GrantId { get; init; }

    public required SaveGrantApplicationRequest Request { get; init; }
}

public sealed class SaveGrantApplicationCommandValidator
    : AbstractValidator<SaveGrantApplicationCommand>
{
    public SaveGrantApplicationCommandValidator()
    {
        RuleFor(command => command.GrantId).NotEmpty();
        RuleFor(command => command.Request).NotNull();
    }
}

public sealed class SaveGrantApplicationCommandHandler
    : ICommandHandler<SaveGrantApplicationCommand, GrantApplicationResponse>
{
    private readonly CreatorContext _context;
    private readonly ICommunityGrantRepository _grants;
    private readonly IGrantApplicationRepository _applications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public SaveGrantApplicationCommandHandler(
        CreatorContext context,
        ICommunityGrantRepository grants,
        IGrantApplicationRepository applications,
        IUnitOfWork unitOfWork,
        IGuidGenerator guids,
        IClock clock)
    {
        _context = context;
        _grants = grants;
        _applications = applications;
        _unitOfWork = unitOfWork;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<GrantApplicationResponse>> Handle(
        SaveGrantApplicationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var creator = await _context.RequireCreatorAsync(cancellationToken);

        if (creator.IsFailure)
        {
            return Result.Failure<GrantApplicationResponse>(creator.Error);
        }

        var allowed = CreatorContext.EnsureCanParticipate(creator.Value);

        if (allowed.IsFailure)
        {
            return Result.Failure<GrantApplicationResponse>(allowed.Error);
        }

        var grant = await _grants.GetAsync(
            CommunityGrantId.From(command.GrantId), cancellationToken);

        if (grant is null || !grant.IsPublished)
        {
            return Result.Failure<GrantApplicationResponse>(CreatorErrors.GrantNotFound);
        }

        var now = _clock.UtcNow;

        if (!grant.IsAcceptingApplications(now))
        {
            return Result.Failure<GrantApplicationResponse>(CreatorErrors.GrantNotOpen);
        }

        var (eligible, _, _) = GrantEligibility.Evaluate(grant, creator.Value, now);

        if (!eligible)
        {
            return Result.Failure<GrantApplicationResponse>(CreatorErrors.GrantNotEligible);
        }

        var application = await _applications.FindForCreatorAsync(
            grant.Id, creator.Value.Id, cancellationToken);

        var isNew = application is null;

        application ??= GrantApplication.Start(
            GrantApplicationId.From(_guids.NewSequentialGuid()),
            grant.Id,
            creator.Value.Id,
            creator.Value.UserId,
            now);

        var saved = application.SaveAnswers(
            grant,
            command.Request.Answers,
            command.Request.RequestedAmountMinorUnits,
            command.Request.AttachmentIds,
            command.Request.AcceptedDeclarations,
            now);

        if (saved.IsFailure)
        {
            return Result.Failure<GrantApplicationResponse>(saved.Error);
        }

        // Add or Update, never both — see TrackingSafety for the trap this avoids.
        if (isNew)
        {
            _applications.Add(application);
            grant.RecordApplication();
        }
        else
        {
            _applications.Update(application);
        }

        // A draft collides on the natural (grant, creator) index when two requests race, and the
        // caller sent no key — so the lookup resolves the natural identity and the loser reads back
        // the winner's draft rather than a 500.
        var commit = await CreatorIdempotency.GuardAsync(
            key: null,
            token => _unitOfWork.SaveChangesAsync(token),
            token => _applications.FindForCreatorAsync(grant.Id, creator.Value.Id, token),
            cancellationToken);

        if (commit.IsFailure)
        {
            return Result.Failure<GrantApplicationResponse>(commit.Error);
        }

        return Result.Success((commit.Value.Replayed ?? application).ToResponse(grant, now));
    }
}

/// <summary>Submits an application. Idempotent on the key.</summary>
[Authorize(Permission = CreatorPermissions.Participate)]
[Auditable(Action = "creator.grant.submit")]
public sealed record SubmitGrantApplicationCommand : ICommand<GrantApplicationResponse>
{
    public required Guid GrantId { get; init; }

    public SaveGrantApplicationRequest? Request { get; init; }

    public string? IdempotencyKeyHeader { get; init; }
}

public sealed class SubmitGrantApplicationCommandHandler
    : ICommandHandler<SubmitGrantApplicationCommand, GrantApplicationResponse>
{
    private const string Operation = "creator-grant-submit";

    private readonly CreatorContext _context;
    private readonly ICommunityGrantRepository _grants;
    private readonly IGrantApplicationRepository _applications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public SubmitGrantApplicationCommandHandler(
        CreatorContext context,
        ICommunityGrantRepository grants,
        IGrantApplicationRepository applications,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _context = context;
        _grants = grants;
        _applications = applications;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<GrantApplicationResponse>> Handle(
        SubmitGrantApplicationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var creator = await _context.RequireCreatorAsync(cancellationToken);

        if (creator.IsFailure)
        {
            return Result.Failure<GrantApplicationResponse>(creator.Error);
        }

        var allowed = CreatorContext.EnsureCanParticipate(creator.Value);

        if (allowed.IsFailure)
        {
            return Result.Failure<GrantApplicationResponse>(allowed.Error);
        }

        var grant = await _grants.GetAsync(
            CommunityGrantId.From(command.GrantId), cancellationToken);

        if (grant is null || !grant.IsPublished)
        {
            return Result.Failure<GrantApplicationResponse>(CreatorErrors.GrantNotFound);
        }

        var now = _clock.UtcNow;

        var key = CreatorIdempotency.Normalise(
            Operation, creator.Value.UserId, command.IdempotencyKeyHeader, command.Request?.IdempotencyKey);

        if (key is not null)
        {
            var replayed = await _applications.FindBySubmitKeyAsync(creator.Value.Id, key, cancellationToken);

            if (replayed is not null)
            {
                // A key the caller already spent on another grant is not a retry of this one.
                return replayed.GrantId == grant.Id
                    ? Result.Success(replayed.ToResponse(grant, now))
                    : Result.Failure<GrantApplicationResponse>(CreatorErrors.IdempotencyKeyReused);
            }
        }

        var application = await _applications.FindForCreatorAsync(
            grant.Id, creator.Value.Id, cancellationToken);

        if (application is null)
        {
            return Result.Failure<GrantApplicationResponse>(CreatorErrors.GrantApplicationNotFound);
        }

        // Already submitted. Answering with the current state rather than a 409 is what makes a
        // retried submit safe.
        if (application.State is not (GrantApplicationState.Draft
            or GrantApplicationState.MoreInformationRequested))
        {
            return Result.Success(application.ToResponse(grant, now));
        }

        if (!grant.IsAcceptingApplications(now))
        {
            return Result.Failure<GrantApplicationResponse>(CreatorErrors.GrantNotOpen);
        }

        // A submission carrying answers saves them first, so the client can submit in one call.
        if (command.Request is not null)
        {
            var saved = application.SaveAnswers(
                grant,
                command.Request.Answers,
                command.Request.RequestedAmountMinorUnits,
                command.Request.AttachmentIds,
                command.Request.AcceptedDeclarations,
                now);

            if (saved.IsFailure)
            {
                return Result.Failure<GrantApplicationResponse>(saved.Error);
            }
        }

        var submitted = application.Submit(grant, key, now);

        if (submitted.IsFailure)
        {
            return Result.Failure<GrantApplicationResponse>(submitted.Error);
        }

        _applications.Update(application);

        var commit = await CreatorIdempotency.GuardAsync(
            key,
            token => _unitOfWork.SaveChangesAsync(token),
            token => _applications.FindBySubmitKeyAsync(creator.Value.Id, key!, token),
            cancellationToken);

        if (commit.IsFailure)
        {
            return Result.Failure<GrantApplicationResponse>(commit.Error);
        }

        if (commit.Value.Replayed is { } raced && raced.GrantId != grant.Id)
        {
            return Result.Failure<GrantApplicationResponse>(CreatorErrors.IdempotencyKeyReused);
        }

        return Result.Success((commit.Value.Replayed ?? application).ToResponse(grant, now));
    }
}

/// <summary>
/// The applicant withdraws.
/// <para>
/// <b>Authorization is on the record, not on the permission.</b> Holding
/// <c>creator:programme:participate</c> says the caller may withdraw <em>their own</em> application;
/// whether this one is theirs is asked separately, on every request.
/// </para>
/// </summary>
[Authorize(Permission = CreatorPermissions.Participate)]
[Auditable(Action = "creator.grant.withdraw")]
public sealed record WithdrawGrantApplicationCommand : ICommand<GrantApplicationResponse>
{
    public required Guid ApplicationId { get; init; }
}

public sealed class WithdrawGrantApplicationCommandHandler
    : ICommandHandler<WithdrawGrantApplicationCommand, GrantApplicationResponse>
{
    private readonly CreatorContext _context;
    private readonly IGrantApplicationRepository _applications;
    private readonly ICommunityGrantRepository _grants;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public WithdrawGrantApplicationCommandHandler(
        CreatorContext context,
        IGrantApplicationRepository applications,
        ICommunityGrantRepository grants,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _context = context;
        _applications = applications;
        _grants = grants;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<GrantApplicationResponse>> Handle(
        WithdrawGrantApplicationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<GrantApplicationResponse>(caller.Error);
        }

        var application = await _applications.GetAsync(
            GrantApplicationId.From(command.ApplicationId), cancellationToken);

        if (application is null)
        {
            return Result.Failure<GrantApplicationResponse>(CreatorErrors.GrantApplicationNotFound);
        }

        if (application.ApplicantUserId != caller.Value)
        {
            // The same error a missing application produces: somebody probing for other people's
            // applications learns nothing from a 403 that they did not already know.
            return Result.Failure<GrantApplicationResponse>(CreatorErrors.GrantApplicationNotFound);
        }

        var now = _clock.UtcNow;
        var withdrawn = application.Withdraw(now);

        if (withdrawn.IsFailure)
        {
            return Result.Failure<GrantApplicationResponse>(withdrawn.Error);
        }

        _applications.Update(application);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var grant = await _grants.GetAsync(application.GrantId, cancellationToken);

        return grant is null
            ? Result.Failure<GrantApplicationResponse>(CreatorErrors.GrantNotFound)
            : Result.Success(application.ToResponse(grant, now));
    }
}

/// <summary>Files a report against an active grant.</summary>
[Authorize(Permission = CreatorPermissions.Participate)]
[Auditable(Action = "creator.grant.report")]
public sealed record SubmitGrantReportCommand : ICommand<GrantApplicationResponse>
{
    public required Guid ApplicationId { get; init; }

    public required SubmitGrantReportRequest Request { get; init; }
}

public sealed class SubmitGrantReportCommandValidator : AbstractValidator<SubmitGrantReportCommand>
{
    public SubmitGrantReportCommandValidator()
    {
        RuleFor(command => command.ApplicationId).NotEmpty();
        RuleFor(command => command.Request).NotNull();
        RuleFor(command => command.Request.ReportId).NotEmpty();
    }
}

public sealed class SubmitGrantReportCommandHandler
    : ICommandHandler<SubmitGrantReportCommand, GrantApplicationResponse>
{
    private readonly CreatorContext _context;
    private readonly IGrantApplicationRepository _applications;
    private readonly ICommunityGrantRepository _grants;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public SubmitGrantReportCommandHandler(
        CreatorContext context,
        IGrantApplicationRepository applications,
        ICommunityGrantRepository grants,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _context = context;
        _applications = applications;
        _grants = grants;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<GrantApplicationResponse>> Handle(
        SubmitGrantReportCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<GrantApplicationResponse>(caller.Error);
        }

        var application = await _applications.GetAsync(
            GrantApplicationId.From(command.ApplicationId), cancellationToken);

        if (application is null || application.ApplicantUserId != caller.Value)
        {
            return Result.Failure<GrantApplicationResponse>(CreatorErrors.GrantApplicationNotFound);
        }

        var now = _clock.UtcNow;

        var filed = application.SubmitReport(
            command.Request.ReportId, command.Request.Summary, now);

        if (filed.IsFailure)
        {
            return Result.Failure<GrantApplicationResponse>(filed.Error);
        }

        _applications.Update(application);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var grant = await _grants.GetAsync(application.GrantId, cancellationToken);

        return grant is null
            ? Result.Failure<GrantApplicationResponse>(CreatorErrors.GrantNotFound)
            : Result.Success(application.ToResponse(grant, now));
    }
}

/// <summary>The reviewer's queue.</summary>
[Authorize(Permission = CreatorPermissions.GrantReview)]
public sealed record ListGrantApplicationsForReviewQuery
    : IQuery<IReadOnlyList<GrantApplicationResponse>>
{
    public Guid? GrantId { get; init; }

    public string? State { get; init; }

    public int? Take { get; init; }
}

public sealed class ListGrantApplicationsForReviewQueryHandler
    : IQueryHandler<ListGrantApplicationsForReviewQuery, IReadOnlyList<GrantApplicationResponse>>
{
    private readonly IGrantApplicationRepository _applications;
    private readonly ICommunityGrantRepository _grants;
    private readonly IClock _clock;

    public ListGrantApplicationsForReviewQueryHandler(
        IGrantApplicationRepository applications,
        ICommunityGrantRepository grants,
        IClock clock)
    {
        _applications = applications;
        _grants = grants;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<GrantApplicationResponse>>> Handle(
        ListGrantApplicationsForReviewQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var state = Enum.TryParse<GrantApplicationState>(query.State, ignoreCase: true, out var parsed)
            ? parsed
            : (GrantApplicationState?)null;

        var applications = await _applications.ListForReviewAsync(
            query.GrantId is { } grantId ? CommunityGrantId.From(grantId) : null,
            state,
            CreatorLimits.PageSize(query.Take),
            cancellationToken);

        var now = _clock.UtcNow;
        var response = new List<GrantApplicationResponse>(applications.Count);

        foreach (var application in applications)
        {
            var grant = await _grants.GetAsync(application.GrantId, cancellationToken);

            if (grant is not null)
            {
                response.Add(application.ToResponse(grant, now));
            }
        }

        return Result.Success<IReadOnlyList<GrantApplicationResponse>>(response);
    }
}

/// <summary>
/// A reviewer's decision.
/// <para>
/// <b>Needs <c>creator:grant:review</c>, and refuses the reviewer's own application.</b> §29 of the
/// phase brief: "Grant applicants cannot decide Approved, Rejected, UnderReview." The permission is
/// the necessary condition and the applicant check is the sufficient one.
/// </para>
/// </summary>
[Authorize(Permission = CreatorPermissions.GrantReview)]
[Auditable(Action = "creator.grant.decide")]
public sealed record DecideGrantApplicationCommand : ICommand<GrantApplicationResponse>
{
    public required Guid ApplicationId { get; init; }

    public required DecideGrantApplicationRequest Request { get; init; }
}

public sealed class DecideGrantApplicationCommandValidator
    : AbstractValidator<DecideGrantApplicationCommand>
{
    public DecideGrantApplicationCommandValidator()
    {
        RuleFor(command => command.ApplicationId).NotEmpty();
        RuleFor(command => command.Request).NotNull();
        RuleFor(command => command.Request.Action).NotEmpty();
        RuleFor(command => command.Request.Reason).MaximumLength(4000);

        RuleFor(command => command.Request.AwardedAmountMinorUnits)
            .GreaterThan(0)
            .When(command => command.Request?.AwardedAmountMinorUnits is not null);
    }
}

public sealed class DecideGrantApplicationCommandHandler
    : ICommandHandler<DecideGrantApplicationCommand, GrantApplicationResponse>
{
    private readonly CreatorContext _context;
    private readonly IGrantApplicationRepository _applications;
    private readonly ICommunityGrantRepository _grants;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public DecideGrantApplicationCommandHandler(
        CreatorContext context,
        IGrantApplicationRepository applications,
        ICommunityGrantRepository grants,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _context = context;
        _applications = applications;
        _grants = grants;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<GrantApplicationResponse>> Handle(
        DecideGrantApplicationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var reviewer = _context.RequireUser();

        if (reviewer.IsFailure)
        {
            return Result.Failure<GrantApplicationResponse>(reviewer.Error);
        }

        var application = await _applications.GetAsync(
            GrantApplicationId.From(command.ApplicationId), cancellationToken);

        if (application is null)
        {
            return Result.Failure<GrantApplicationResponse>(CreatorErrors.GrantApplicationNotFound);
        }

        if (application.ApplicantUserId == reviewer.Value)
        {
            return Result.Failure<GrantApplicationResponse>(CreatorErrors.ReviewerCannotDecideOwn);
        }

        var grant = await _grants.GetAsync(application.GrantId, cancellationToken);

        if (grant is null)
        {
            return Result.Failure<GrantApplicationResponse>(CreatorErrors.GrantNotFound);
        }

        var now = _clock.UtcNow;

        var moved = command.Request.Action.Trim().ToLowerInvariant() switch
        {
            "review" => application.BeginReview(reviewer.Value, now),
            "more-information" => application.RequestInformation(
                reviewer.Value,
                command.Request.InformationRequested ?? [],
                command.Request.RespondBy,
                now),
            "approve" => application.Approve(
                grant,
                reviewer.Value,
                command.Request.AwardedAmountMinorUnits ?? grant.FundingMinorUnits,
                command.Request.Reason,
                now),
            "reject" => application.Reject(reviewer.Value, command.Request.Reason, now),
            "complete" => application.Complete(reviewer.Value, now),
            _ => Result.Failure(CreatorErrors.GrantApplicationInvalidTransition)
        };

        if (moved.IsFailure)
        {
            return Result.Failure<GrantApplicationResponse>(moved.Error);
        }

        _applications.Update(application);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(application.ToResponse(grant, now));
    }
}
