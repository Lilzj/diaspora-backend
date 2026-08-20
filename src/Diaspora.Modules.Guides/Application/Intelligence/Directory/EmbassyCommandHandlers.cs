using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Intelligence.Mapping;
using Diaspora.Modules.Guides.Application.Intelligence.Services;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Embassies;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Extensibility.Intelligence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Intelligence.Directory;

/// <summary>Adds a mission to the embassy directory.</summary>
public sealed class CreateEmbassyCommandHandler
    : ICommandHandler<CreateEmbassyCommand, EmbassyResponse>
{
    private readonly IEmbassyRepository _embassies;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IntelligenceAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CreateEmbassyCommandHandler(
        IEmbassyRepository embassies,
        IUnitOfWork unitOfWork,
        IntelligenceAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock)
    {
        _embassies = embassies;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<EmbassyResponse>> Handle(
        CreateEmbassyCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;
        var caller = _authorization.CallerId;

        var created = Embassy.Create(
            EmbassyId.From(_guids.NewSequentialGuid()),
            request.CountryCode,
            request.ServesCountryCode,
            request.Name,
            _clock.UtcNow,
            caller);

        if (created.IsFailure)
        {
            return Result.Failure<EmbassyResponse>(created.Error);
        }

        var embassy = created.Value;

        var profiled = embassy.UpdateProfile(
            request.Name, request.City, request.Address, null, null, caller);

        if (profiled.IsFailure)
        {
            return Result.Failure<EmbassyResponse>(profiled.Error);
        }

        await _embassies.AddAsync(embassy, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            IntelligenceDirectoryMapper.ToResponse(embassy, locale: null, forEditor: true));
    }
}

/// <summary>
/// Rewrites a mission's entry.
/// <para>
/// Applied in sequence and stopped at the first refusal, which the <c>[Transactional]</c> on the
/// command makes safe: nothing applied before the refusal survives it.
/// </para>
/// </summary>
public sealed class UpdateEmbassyCommandHandler
    : ICommandHandler<UpdateEmbassyCommand, EmbassyResponse>
{
    private readonly IEmbassyRepository _embassies;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IntelligenceAuthorizationService _authorization;

    public UpdateEmbassyCommandHandler(
        IEmbassyRepository embassies,
        IUnitOfWork unitOfWork,
        IntelligenceAuthorizationService authorization)
    {
        _embassies = embassies;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Result<EmbassyResponse>> Handle(
        UpdateEmbassyCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var embassy = await _embassies.GetAsync(
            EmbassyId.From(command.EmbassyId), cancellationToken);

        if (embassy is null)
        {
            return Result.Failure<EmbassyResponse>(IntelligenceErrors.EmbassyNotFound);
        }

        var request = command.Request;

        var revision = embassy.EnsureRevision(request.ExpectedRevision);

        if (revision.IsFailure)
        {
            return Result.Failure<EmbassyResponse>(revision.Error);
        }

        var applied = Apply(embassy, request, _authorization.CallerId);

        if (applied.IsFailure)
        {
            return Result.Failure<EmbassyResponse>(applied.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            IntelligenceDirectoryMapper.ToResponse(embassy, locale: null, forEditor: true));
    }

    private static Result Apply(Embassy embassy, UpdateEmbassyRequest request, UserId? caller)
    {
        var profiled = embassy.UpdateProfile(
            request.Name,
            request.City,
            request.Address,
            request.Latitude,
            request.Longitude,
            caller);

        if (profiled.IsFailure)
        {
            return profiled;
        }

        var contacted = embassy.UpdateContact(
            request.Telephone,
            request.EmergencyTelephone,
            request.Email,
            request.Website,
            request.AppointmentUrl,
            request.RequiresAppointment,
            caller);

        if (contacted.IsFailure)
        {
            return contacted;
        }

        var capable = embassy.UpdateCapabilities(
            request.OffersPassportServices,
            request.OffersVisaServices,
            request.OffersConsularServices,
            request.OffersEmergencyAssistance,
            caller);

        if (capable.IsFailure)
        {
            return capable;
        }

        var media = embassy.SetMedia(request.MediaId, caller);

        if (media.IsFailure)
        {
            return media;
        }

        // Null means "unlink" and absent means "leave it", which one nullable field cannot say. The
        // flag is what separates them — the same shape UpdateOfficialNoticeRequest.SetDates uses.
        if (request.SetAuthority)
        {
            var linked = embassy.LinkAuthority(
                request.AuthorityId is { } id ? IssuingAuthorityId.From(id) : null, caller);

            if (linked.IsFailure)
            {
                return linked;
            }
        }

        return ApplyCollections(embassy, request, caller);
    }

    private static Result ApplyCollections(
        Embassy embassy, UpdateEmbassyRequest request, UserId? caller)
    {
        if (request.OfficeHours is { } hours)
        {
            var resolved = new List<(DayOfWeek, string?, bool)>(hours.Count);

            foreach (var row in hours)
            {
                if (!Enum.TryParse<DayOfWeek>(row.Day, ignoreCase: true, out var day))
                {
                    return Result.Failure(IntelligenceErrors.EmbassyNameInvalid);
                }

                resolved.Add((day, row.Hours, row.IsClosed));
            }

            var applied = embassy.SetOfficeHours(resolved, caller);

            if (applied.IsFailure)
            {
                return applied;
            }
        }

        if (request.Services is { } services)
        {
            var applied = embassy.SetServices(
                [.. services.Select(service =>
                    ((string?)service.Key,
                        (string?)service.Name,
                        service.Description,
                        service.RequiresAppointment))],
                caller);

            if (applied.IsFailure)
            {
                return applied;
            }
        }

        return request.Localizations is not { } localizations
            ? Result.Success()
            : embassy.SetLocalizations(
                [.. localizations.Select(localization =>
                    ((string?)localization.Locale,
                        (string?)localization.Name,
                        localization.Description,
                        localization.Address))],
                caller);
    }
}

/// <summary>
/// Raises a claim that a mission is what it says it is.
/// <para>
/// The same shape as an authority's submission, and it stores the case identifier for the same
/// reason: the inbound projection has nothing else to correlate a Trust decision against.
/// </para>
/// </summary>
public sealed class SubmitEmbassyVerificationCommandHandler
    : ICommandHandler<SubmitEmbassyVerificationCommand, EmbassyResponse>
{
    private readonly IEmbassyRepository _embassies;
    private readonly AuthorityVerificationProviderRegistry _providers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IntelligenceAuthorizationService _authorization;

    public SubmitEmbassyVerificationCommandHandler(
        IEmbassyRepository embassies,
        AuthorityVerificationProviderRegistry providers,
        IUnitOfWork unitOfWork,
        IntelligenceAuthorizationService authorization)
    {
        _embassies = embassies;
        _providers = providers;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Result<EmbassyResponse>> Handle(
        SubmitEmbassyVerificationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<EmbassyResponse>(caller.Error);
        }

        var embassy = await _embassies.GetAsync(
            EmbassyId.From(command.EmbassyId), cancellationToken);

        if (embassy is null)
        {
            return Result.Failure<EmbassyResponse>(IntelligenceErrors.EmbassyNotFound);
        }

        if (embassy.VerificationCaseId is not null && !embassy.IsVerified)
        {
            return Result.Failure<EmbassyResponse>(
                IntelligenceErrors.VerificationAlreadyPending);
        }

        var submitted = await _providers.SubmitAsync(
            new AuthorityVerificationRequest
            {
                SubjectKind = AuthoritySubjectKind.Embassy,
                SubjectId = embassy.Id.Value,
                CountryCode = embassy.CountryCode,
                Name = embassy.Name,
                AuthorityType = AuthorityType.Embassy,
                Website = embassy.Website,
                SubmittedBy = caller.Value,
                Evidence =
                [
                    .. command.Request.Evidence.Select(evidence =>
                        new AuthorityVerificationEvidence
                        {
                            MediaId = evidence.MediaId,
                            DocumentType = evidence.DocumentType
                        })
                ],
                Notes = command.Request.Notes
            },
            cancellationToken);

        if (submitted.IsFailure)
        {
            return Result.Failure<EmbassyResponse>(submitted.Error);
        }

        embassy.RecordVerificationCase(submitted.Value.CaseId);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            IntelligenceDirectoryMapper.ToResponse(embassy, locale: null, forEditor: true));
    }
}

/// <summary>Removes a mission from the directory, through the platform's soft-delete path.</summary>
public sealed class ArchiveEmbassyCommandHandler : ICommandHandler<ArchiveEmbassyCommand>
{
    private readonly IEmbassyRepository _embassies;
    private readonly IUnitOfWork _unitOfWork;

    public ArchiveEmbassyCommandHandler(IEmbassyRepository embassies, IUnitOfWork unitOfWork)
    {
        _embassies = embassies;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        ArchiveEmbassyCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var embassy = await _embassies.GetAsync(
            EmbassyId.From(command.EmbassyId), cancellationToken);

        if (embassy is null)
        {
            return Result.Failure(IntelligenceErrors.EmbassyNotFound);
        }

        _embassies.Remove(embassy);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

/// <summary>Returns an archived mission to the directory, reading past the query filter.</summary>
public sealed class RestoreEmbassyCommandHandler
    : ICommandHandler<RestoreEmbassyCommand, EmbassyResponse>
{
    private readonly IEmbassyRepository _embassies;
    private readonly IUnitOfWork _unitOfWork;

    public RestoreEmbassyCommandHandler(IEmbassyRepository embassies, IUnitOfWork unitOfWork)
    {
        _embassies = embassies;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EmbassyResponse>> Handle(
        RestoreEmbassyCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var embassy = await _embassies.GetIncludingArchivedAsync(
            EmbassyId.From(command.EmbassyId), cancellationToken);

        if (embassy is null)
        {
            return Result.Failure<EmbassyResponse>(IntelligenceErrors.EmbassyNotFound);
        }

        if (!embassy.IsDeleted)
        {
            return Result.Failure<EmbassyResponse>(IntelligenceErrors.EmbassyNotArchived);
        }

        embassy.Restore();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            IntelligenceDirectoryMapper.ToResponse(embassy, locale: null, forEditor: true));
    }
}
