using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Intelligence.Mapping;
using Diaspora.Modules.Guides.Application.Intelligence.Services;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Domain.Visas;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Intelligence.Directory;

/// <summary>Opens a draft visa advisory.</summary>
public sealed class CreateVisaAdvisoryCommandHandler
    : ICommandHandler<CreateVisaAdvisoryCommand, VisaAdvisoryResponse>
{
    private readonly IVisaAdvisoryRepository _advisories;
    private readonly IIssuingAuthorityRepository _authorities;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IntelligenceAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CreateVisaAdvisoryCommandHandler(
        IVisaAdvisoryRepository advisories,
        IIssuingAuthorityRepository authorities,
        IUnitOfWork unitOfWork,
        IntelligenceAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock)
    {
        _advisories = advisories;
        _authorities = authorities;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<VisaAdvisoryResponse>> Handle(
        CreateVisaAdvisoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;
        var authorityId = IssuingAuthorityId.From(request.AuthorityId);

        var authority = await _authorities.GetAsync(authorityId, cancellationToken);

        if (authority is null)
        {
            return Result.Failure<VisaAdvisoryResponse>(IntelligenceErrors.AuthorityNotFound);
        }

        // The destination's authority, not the applicant's. An advisory about entering Mauritius is
        // issued by a Mauritian body whoever it applies to.
        if (!authority.IssuesFor(request.CountryCode))
        {
            return Result.Failure<VisaAdvisoryResponse>(
                IntelligenceErrors.AuthorityCountryMismatch);
        }

        var created = VisaAdvisory.Create(
            VisaAdvisoryId.From(_guids.NewSequentialGuid()),
            request.CountryCode,
            request.AppliesToCountryCode,
            authorityId,
            request.Title,
            request.Summary,
            _clock.UtcNow,
            _authorization.CallerId);

        if (created.IsFailure)
        {
            return Result.Failure<VisaAdvisoryResponse>(created.Error);
        }

        await _advisories.AddAsync(created.Value, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            IntelligenceDirectoryMapper.ToResponse(
                created.Value, authority, _clock.UtcNow, locale: null, forEditor: true));
    }
}

/// <summary>Rewrites a visa advisory.</summary>
public sealed class UpdateVisaAdvisoryCommandHandler
    : ICommandHandler<UpdateVisaAdvisoryCommand, VisaAdvisoryResponse>
{
    private readonly IVisaAdvisoryRepository _advisories;
    private readonly IIssuingAuthorityRepository _authorities;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IntelligenceAuthorizationService _authorization;
    private readonly IClock _clock;

    public UpdateVisaAdvisoryCommandHandler(
        IVisaAdvisoryRepository advisories,
        IIssuingAuthorityRepository authorities,
        IUnitOfWork unitOfWork,
        IntelligenceAuthorizationService authorization,
        IClock clock)
    {
        _advisories = advisories;
        _authorities = authorities;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<VisaAdvisoryResponse>> Handle(
        UpdateVisaAdvisoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var advisory = await _advisories.GetAsync(
            VisaAdvisoryId.From(command.VisaAdvisoryId), cancellationToken);

        if (advisory is null)
        {
            return Result.Failure<VisaAdvisoryResponse>(
                IntelligenceErrors.VisaAdvisoryNotFound);
        }

        var request = command.Request;

        var revision = advisory.EnsureRevision(request.ExpectedRevision);

        if (revision.IsFailure)
        {
            return Result.Failure<VisaAdvisoryResponse>(revision.Error);
        }

        var applied = await ApplyAsync(advisory, request, cancellationToken);

        if (applied.IsFailure)
        {
            return Result.Failure<VisaAdvisoryResponse>(applied.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var authority = await _authorities.GetIncludingArchivedAsync(
            advisory.AuthorityId, cancellationToken);

        return Result.Success(
            IntelligenceDirectoryMapper.ToResponse(
                advisory, authority, _clock.UtcNow, locale: null, forEditor: true));
    }

    private async Task<Result> ApplyAsync(
        VisaAdvisory advisory,
        UpdateVisaAdvisoryRequest request,
        CancellationToken cancellationToken)
    {
        var caller = _authorization.CallerId;

        var updated = advisory.Update(
            request.Title,
            request.Summary,
            request.Content,
            request.VisaCategory,
            request.ProcessingTime,
            caller);

        if (updated.IsFailure)
        {
            return updated;
        }

        var sourced = advisory.AttributeSource(
            request.OfficialReference, request.SourceUrl, request.ApplicationUrl, caller);

        if (sourced.IsFailure)
        {
            return sourced;
        }

        if (request.SetDates)
        {
            var scheduled = advisory.Schedule(request.EffectiveFrom, request.ExpiresAt, caller);

            if (scheduled.IsFailure)
            {
                return scheduled;
            }
        }

        if (request.AuthorityId is { } id)
        {
            var authorityId = IssuingAuthorityId.From(id);
            var authority = await _authorities.GetAsync(authorityId, cancellationToken);

            if (authority is null)
            {
                return Result.Failure(IntelligenceErrors.AuthorityNotFound);
            }

            if (!authority.IssuesFor(advisory.CountryCode))
            {
                return Result.Failure(IntelligenceErrors.AuthorityCountryMismatch);
            }

            var reassigned = advisory.Reassign(authorityId, caller);

            if (reassigned.IsFailure)
            {
                return reassigned;
            }
        }

        return ApplyCollections(advisory, request, caller);
    }

    private static Result ApplyCollections(
        VisaAdvisory advisory, UpdateVisaAdvisoryRequest request, UserId? caller)
    {
        // Both lists together, because the aggregate replaces them as a pair — a caller moving a
        // sentence between requirements and documents must not be momentarily holding an advisory
        // that lists it twice.
        if (request.Requirements is not null || request.Documents is not null)
        {
            var lines = advisory.SetLines(
                request.Requirements ?? [.. advisory.Requirements.Select(line => line.Text)],
                request.Documents ?? [.. advisory.Documents.Select(line => line.Text)],
                caller);

            if (lines.IsFailure)
            {
                return lines;
            }
        }

        if (request.RelatedGuideIds is { } guides)
        {
            var related = advisory.SetRelatedGuides(guides, caller);

            if (related.IsFailure)
            {
                return related;
            }
        }

        return request.Localizations is not { } localizations
            ? Result.Success()
            : advisory.SetLocalizations(
                [.. localizations.Select(localization =>
                    ((string?)localization.Locale,
                        (string?)localization.Title,
                        (string?)localization.Summary,
                        (string?)localization.Content))],
                caller);
    }
}

/// <summary>
/// Makes a visa advisory visible.
/// <para>
/// The authority must be verified, exactly as it must be for a notice. An advisory carries the same
/// implicit claim — this is what the rules are — and a member turned away at a border is not helped
/// by the distinction between the two aggregates.
/// </para>
/// </summary>
public sealed class PublishVisaAdvisoryCommandHandler
    : ICommandHandler<PublishVisaAdvisoryCommand, VisaAdvisoryResponse>
{
    private readonly IVisaAdvisoryRepository _advisories;
    private readonly IIssuingAuthorityRepository _authorities;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IntelligenceAuthorizationService _authorization;
    private readonly IClock _clock;

    public PublishVisaAdvisoryCommandHandler(
        IVisaAdvisoryRepository advisories,
        IIssuingAuthorityRepository authorities,
        IUnitOfWork unitOfWork,
        IntelligenceAuthorizationService authorization,
        IClock clock)
    {
        _advisories = advisories;
        _authorities = authorities;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<VisaAdvisoryResponse>> Handle(
        PublishVisaAdvisoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var advisory = await _advisories.GetAsync(
            VisaAdvisoryId.From(command.VisaAdvisoryId), cancellationToken);

        if (advisory is null)
        {
            return Result.Failure<VisaAdvisoryResponse>(
                IntelligenceErrors.VisaAdvisoryNotFound);
        }

        var revision = advisory.EnsureRevision(command.ExpectedRevision);

        if (revision.IsFailure)
        {
            return Result.Failure<VisaAdvisoryResponse>(revision.Error);
        }

        var authority = await _authorities.GetAsync(advisory.AuthorityId, cancellationToken);

        if (authority is null)
        {
            return Result.Failure<VisaAdvisoryResponse>(IntelligenceErrors.AuthorityNotFound);
        }

        if (!authority.IsVerified)
        {
            return Result.Failure<VisaAdvisoryResponse>(IntelligenceErrors.AuthorityUnverified);
        }

        var published = advisory.Publish(_clock.UtcNow, _authorization.CallerId);

        if (published.IsFailure)
        {
            return Result.Failure<VisaAdvisoryResponse>(published.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            IntelligenceDirectoryMapper.ToResponse(
                advisory, authority, _clock.UtcNow, locale: null, forEditor: true));
    }
}

/// <summary>Withdraws a visa advisory, through the platform's soft-delete path.</summary>
public sealed class ArchiveVisaAdvisoryCommandHandler
    : ICommandHandler<ArchiveVisaAdvisoryCommand>
{
    private readonly IVisaAdvisoryRepository _advisories;
    private readonly IUnitOfWork _unitOfWork;

    public ArchiveVisaAdvisoryCommandHandler(
        IVisaAdvisoryRepository advisories, IUnitOfWork unitOfWork)
    {
        _advisories = advisories;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        ArchiveVisaAdvisoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var advisory = await _advisories.GetAsync(
            VisaAdvisoryId.From(command.VisaAdvisoryId), cancellationToken);

        if (advisory is null)
        {
            return Result.Failure(IntelligenceErrors.VisaAdvisoryNotFound);
        }

        _advisories.Remove(advisory);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

/// <summary>Returns an archived advisory to the desk as a draft, reading past the query filter.</summary>
public sealed class RestoreVisaAdvisoryCommandHandler
    : ICommandHandler<RestoreVisaAdvisoryCommand, VisaAdvisoryResponse>
{
    private readonly IVisaAdvisoryRepository _advisories;
    private readonly IIssuingAuthorityRepository _authorities;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public RestoreVisaAdvisoryCommandHandler(
        IVisaAdvisoryRepository advisories,
        IIssuingAuthorityRepository authorities,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _advisories = advisories;
        _authorities = authorities;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<VisaAdvisoryResponse>> Handle(
        RestoreVisaAdvisoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var advisory = await _advisories.GetIncludingArchivedAsync(
            VisaAdvisoryId.From(command.VisaAdvisoryId), cancellationToken);

        if (advisory is null)
        {
            return Result.Failure<VisaAdvisoryResponse>(
                IntelligenceErrors.VisaAdvisoryNotFound);
        }

        if (!advisory.IsDeleted)
        {
            return Result.Failure<VisaAdvisoryResponse>(IntelligenceErrors.VisaNotArchived);
        }

        advisory.Restore();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var authority = await _authorities.GetIncludingArchivedAsync(
            advisory.AuthorityId, cancellationToken);

        return Result.Success(
            IntelligenceDirectoryMapper.ToResponse(
                advisory, authority, _clock.UtcNow, locale: null, forEditor: true));
    }
}
