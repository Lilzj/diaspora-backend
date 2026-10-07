using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Intelligence.Mapping;
using Diaspora.Modules.Guides.Application.Intelligence.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Extensibility.Intelligence;
using Diaspora.Modules.Guides.Observability;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Intelligence.Catalogue;

/// <summary>Records a body whose statements this platform will treat as official.</summary>
public sealed class CreateIssuingAuthorityCommandHandler
    : ICommandHandler<CreateIssuingAuthorityCommand, IssuingAuthorityResponse>
{
    private readonly IIssuingAuthorityRepository _authorities;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IntelligenceAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CreateIssuingAuthorityCommandHandler(
        IIssuingAuthorityRepository authorities,
        IUnitOfWork unitOfWork,
        IntelligenceAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock)
    {
        _authorities = authorities;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<IssuingAuthorityResponse>> Handle(
        CreateIssuingAuthorityCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;
        var caller = _authorization.CallerId;

        if (IntelligenceWireNames.ParseAuthorityType(request.AuthorityType) == AuthorityType.Platform)
        {
            return Result.Failure<IssuingAuthorityResponse>(IntelligenceErrors.AuthorityTypeReserved);
        }

        var created = IssuingAuthority.Create(
            IssuingAuthorityId.From(_guids.NewSequentialGuid()),
            request.CountryCode,
            request.Name,
            IntelligenceWireNames.ParseAuthorityType(request.AuthorityType) ?? AuthorityType.Other,
            _clock.UtcNow,
            caller);

        if (created.IsFailure)
        {
            return Result.Failure<IssuingAuthorityResponse>(created.Error);
        }

        var authority = created.Value;

        // Through the aggregate rather than by construction, so the contact details a create sends
        // are checked by exactly the rules an edit would apply.
        var configured = authority.Update(
            request.Name,
            request.ShortName,
            authority.AuthorityType,
            request.Description,
            request.Website,
            request.ContactEmail,
            request.ContactPhone,
            caller);

        if (configured.IsFailure)
        {
            return Result.Failure<IssuingAuthorityResponse>(configured.Error);
        }

        await _authorities.AddAsync(authority, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            IntelligenceCatalogueMapper.ToResponse(authority, forEditor: true));
    }
}

/// <summary>Returns, or creates and confirms, the platform's authority for a country.</summary>
public sealed class EnsurePlatformAuthorityCommandHandler
    : ICommandHandler<EnsurePlatformAuthorityCommand, IssuingAuthorityResponse>
{
    private readonly IIssuingAuthorityRepository _authorities;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IntelligenceAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public EnsurePlatformAuthorityCommandHandler(
        IIssuingAuthorityRepository authorities,
        IUnitOfWork unitOfWork,
        IntelligenceAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock)
    {
        _authorities = authorities;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<IssuingAuthorityResponse>> Handle(
        EnsurePlatformAuthorityCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var country = IntelligenceNormalization.Country(command.CountryCode);
        if (country is null)
        {
            return Result.Failure<IssuingAuthorityResponse>(IntelligenceErrors.CountryInvalid);
        }

        var existing = await _authorities.ListForCountryAsync(
            country, AuthorityType.Platform, cancellationToken: cancellationToken);
        if (existing.Count > 0)
        {
            return Result.Success(
                IntelligenceCatalogueMapper.ToResponse(existing[0], forEditor: true));
        }

        var created = IssuingAuthority.CreatePlatform(
            IssuingAuthorityId.From(_guids.NewSequentialGuid()),
            country,
            _clock.UtcNow,
            _authorization.CallerId);
        if (created.IsFailure)
        {
            return Result.Failure<IssuingAuthorityResponse>(created.Error);
        }

        await _authorities.AddAsync(created.Value, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            IntelligenceCatalogueMapper.ToResponse(created.Value, forEditor: true));
    }
}

/// <summary>Rewrites an authority record.</summary>
public sealed class UpdateIssuingAuthorityCommandHandler
    : ICommandHandler<UpdateIssuingAuthorityCommand, IssuingAuthorityResponse>
{
    private readonly IIssuingAuthorityRepository _authorities;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IntelligenceAuthorizationService _authorization;

    public UpdateIssuingAuthorityCommandHandler(
        IIssuingAuthorityRepository authorities,
        IUnitOfWork unitOfWork,
        IntelligenceAuthorizationService authorization)
    {
        _authorities = authorities;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Result<IssuingAuthorityResponse>> Handle(
        UpdateIssuingAuthorityCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var authority = await _authorities.GetAsync(
            IssuingAuthorityId.From(command.IssuingAuthorityId), cancellationToken);

        if (authority is null)
        {
            return Result.Failure<IssuingAuthorityResponse>(IntelligenceErrors.AuthorityNotFound);
        }

        var request = command.Request;

        // Neither into nor out of the reserved type: the platform's authority is made only by
        // EnsurePlatformAuthorityCommand, and a government body never becomes Diaspora.
        var requestedType = IntelligenceWireNames.ParseAuthorityType(request.AuthorityType);
        if (requestedType is not null
            && (requestedType == AuthorityType.Platform) != authority.IsPlatform)
        {
            return Result.Failure<IssuingAuthorityResponse>(IntelligenceErrors.AuthorityTypeReserved);
        }

        var revision = authority.EnsureRevision(request.ExpectedRevision);

        if (revision.IsFailure)
        {
            return Result.Failure<IssuingAuthorityResponse>(revision.Error);
        }

        var updated = authority.Update(
            request.Name,
            request.ShortName,
            IntelligenceWireNames.ParseAuthorityType(request.AuthorityType)
                ?? authority.AuthorityType,
            request.Description,
            request.Website,
            request.ContactEmail,
            request.ContactPhone,
            _authorization.CallerId);

        if (updated.IsFailure)
        {
            return Result.Failure<IssuingAuthorityResponse>(updated.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            IntelligenceCatalogueMapper.ToResponse(authority, forEditor: true));
    }
}

/// <summary>
/// Raises a claim that a body is what it says it is.
/// <para>
/// <b>The case identifier is stored before the transaction closes, and that is the entire point of
/// the command.</b> B8 shipped its equivalent without doing so: submissions went out, Trust decided,
/// approvals came back, and every one of them had nowhere to land. Each half was correct on its own,
/// which is precisely why only an end-to-end test can catch it.
/// </para>
/// <para>
/// <b>A submission with a claim already pending is refused.</b> Two open cases for one institution
/// means two reviewers deciding the same question and two decisions arriving out of order at one
/// projection — the second overwriting the first for no reason anybody can see.
/// </para>
/// </summary>
public sealed class SubmitAuthorityVerificationCommandHandler
    : ICommandHandler<SubmitAuthorityVerificationCommand, IssuingAuthorityResponse>
{
    private readonly IIssuingAuthorityRepository _authorities;
    private readonly AuthorityVerificationProviderRegistry _providers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IntelligenceAuthorizationService _authorization;

    public SubmitAuthorityVerificationCommandHandler(
        IIssuingAuthorityRepository authorities,
        AuthorityVerificationProviderRegistry providers,
        IUnitOfWork unitOfWork,
        IntelligenceAuthorizationService authorization)
    {
        _authorities = authorities;
        _providers = providers;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Result<IssuingAuthorityResponse>> Handle(
        SubmitAuthorityVerificationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<IssuingAuthorityResponse>(caller.Error);
        }

        var authority = await _authorities.GetAsync(
            IssuingAuthorityId.From(command.IssuingAuthorityId), cancellationToken);

        if (authority is null)
        {
            return Result.Failure<IssuingAuthorityResponse>(IntelligenceErrors.AuthorityNotFound);
        }

        if (authority.VerificationCaseId is not null && !authority.IsVerified)
        {
            return Result.Failure<IssuingAuthorityResponse>(
                IntelligenceErrors.VerificationAlreadyPending);
        }

        var submitted = await _providers.SubmitAsync(
            new AuthorityVerificationRequest
            {
                SubjectKind = AuthoritySubjectKind.IssuingAuthority,
                SubjectId = authority.Id.Value,
                CountryCode = authority.CountryCode,
                Name = authority.Name,
                AuthorityType = authority.AuthorityType,
                Website = authority.Website,
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
            return Result.Failure<IssuingAuthorityResponse>(submitted.Error);
        }

        // The write the whole integration depends on.
        authority.RecordVerificationCase(submitted.Value.CaseId);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        IntelligenceTelemetry.AuthorityVerifications.Add(
            1, new KeyValuePair<string, object?>("country", authority.CountryCode));

        return Result.Success(
            IntelligenceCatalogueMapper.ToResponse(authority, forEditor: true));
    }
}

/// <summary>Retires an authority, unless live notices still name it.</summary>
public sealed class ArchiveIssuingAuthorityCommandHandler
    : ICommandHandler<ArchiveIssuingAuthorityCommand>
{
    private readonly IIssuingAuthorityRepository _authorities;
    private readonly IOfficialNoticeRepository _notices;
    private readonly IUnitOfWork _unitOfWork;

    public ArchiveIssuingAuthorityCommandHandler(
        IIssuingAuthorityRepository authorities,
        IOfficialNoticeRepository notices,
        IUnitOfWork unitOfWork)
    {
        _authorities = authorities;
        _notices = notices;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        ArchiveIssuingAuthorityCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var authorityId = IssuingAuthorityId.From(command.IssuingAuthorityId);
        var authority = await _authorities.GetAsync(authorityId, cancellationToken);

        if (authority is null)
        {
            return Result.Failure(IntelligenceErrors.AuthorityNotFound);
        }

        if (await _notices.AnyPublishedForAuthorityAsync(authorityId, cancellationToken))
        {
            return Result.Failure(IntelligenceErrors.AuthorityInUse);
        }

        _authorities.Remove(authority);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

/// <summary>Returns an archived authority to the register, verification intact.</summary>
public sealed class RestoreIssuingAuthorityCommandHandler
    : ICommandHandler<RestoreIssuingAuthorityCommand, IssuingAuthorityResponse>
{
    private readonly IIssuingAuthorityRepository _authorities;
    private readonly IUnitOfWork _unitOfWork;

    public RestoreIssuingAuthorityCommandHandler(
        IIssuingAuthorityRepository authorities, IUnitOfWork unitOfWork)
    {
        _authorities = authorities;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IssuingAuthorityResponse>> Handle(
        RestoreIssuingAuthorityCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var authority = await _authorities.GetIncludingArchivedAsync(
            IssuingAuthorityId.From(command.IssuingAuthorityId), cancellationToken);

        if (authority is null)
        {
            return Result.Failure<IssuingAuthorityResponse>(IntelligenceErrors.AuthorityNotFound);
        }

        if (!authority.IsDeleted)
        {
            return Result.Failure<IssuingAuthorityResponse>(
                IntelligenceErrors.AuthorityNotArchived);
        }

        authority.Restore();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            IntelligenceCatalogueMapper.ToResponse(authority, forEditor: true));
    }
}
