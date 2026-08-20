using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Intelligence.Mapping;
using Diaspora.Modules.Guides.Application.Intelligence.Services;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Holidays;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Intelligence.Directory;

/// <summary>Adds a day to the public holiday calendar.</summary>
public sealed class CreatePublicHolidayCommandHandler
    : ICommandHandler<CreatePublicHolidayCommand, PublicHolidayResponse>
{
    private readonly IPublicHolidayRepository _holidays;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IntelligenceAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CreatePublicHolidayCommandHandler(
        IPublicHolidayRepository holidays,
        IUnitOfWork unitOfWork,
        IntelligenceAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock)
    {
        _holidays = holidays;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<PublicHolidayResponse>> Handle(
        CreatePublicHolidayCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;
        var caller = _authorization.CallerId;

        var created = PublicHoliday.Create(
            PublicHolidayId.From(_guids.NewSequentialGuid()),
            request.CountryCode,
            request.Name,
            IntelligenceWireNames.ParseHolidayKind(request.Kind) ?? HolidayKind.National,
            request.ObservedOn,
            request.ObservedUntil,
            _clock.UtcNow,
            caller);

        if (created.IsFailure)
        {
            return Result.Failure<PublicHolidayResponse>(created.Error);
        }

        var holiday = created.Value;

        // Through the aggregate, so the region and the closure flags a create sends are checked by
        // exactly the rules an edit would apply — and so a calendar imported by a script and one
        // typed by an editor land identically.
        var configured = Configure(holiday, request, caller);

        if (configured.IsFailure)
        {
            return Result.Failure<PublicHolidayResponse>(configured.Error);
        }

        await _holidays.AddAsync(holiday, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            IntelligenceDirectoryMapper.ToResponse(holiday, locale: null, forEditor: true));
    }

    private static Result Configure(
        PublicHoliday holiday, CreatePublicHolidayRequest request, UserId? caller)
    {
        var updated = holiday.Update(
            request.Name,
            request.Description,
            IntelligenceWireNames.ParseHolidayKind(request.Kind) ?? HolidayKind.National,
            request.ObservedOn,
            request.ObservedUntil,
            request.Region,
            request.ClosesGovernmentOffices,
            request.ClosesEmbassies,
            caller);

        if (updated.IsFailure || request.Localizations.Count == 0)
        {
            return updated;
        }

        return holiday.SetLocalizations(
            [.. request.Localizations.Select(localization =>
                ((string?)localization.Locale,
                    (string?)localization.Name,
                    localization.Description))],
            caller);
    }
}

/// <summary>Rewrites a calendar entry.</summary>
public sealed class UpdatePublicHolidayCommandHandler
    : ICommandHandler<UpdatePublicHolidayCommand, PublicHolidayResponse>
{
    private readonly IPublicHolidayRepository _holidays;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IntelligenceAuthorizationService _authorization;

    public UpdatePublicHolidayCommandHandler(
        IPublicHolidayRepository holidays,
        IUnitOfWork unitOfWork,
        IntelligenceAuthorizationService authorization)
    {
        _holidays = holidays;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Result<PublicHolidayResponse>> Handle(
        UpdatePublicHolidayCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var holiday = await _holidays.GetAsync(
            PublicHolidayId.From(command.PublicHolidayId), cancellationToken);

        if (holiday is null)
        {
            return Result.Failure<PublicHolidayResponse>(IntelligenceErrors.HolidayNotFound);
        }

        var request = command.Request;

        var revision = holiday.EnsureRevision(request.ExpectedRevision);

        if (revision.IsFailure)
        {
            return Result.Failure<PublicHolidayResponse>(revision.Error);
        }

        var caller = _authorization.CallerId;

        var updated = holiday.Update(
            request.Name,
            request.Description,
            IntelligenceWireNames.ParseHolidayKind(request.Kind) ?? holiday.Kind,
            request.ObservedOn,
            request.ObservedUntil,
            request.Region,
            request.ClosesGovernmentOffices,
            request.ClosesEmbassies,
            caller);

        if (updated.IsFailure)
        {
            return Result.Failure<PublicHolidayResponse>(updated.Error);
        }

        if (request.Localizations is { } localizations)
        {
            var localized = holiday.SetLocalizations(
                [.. localizations.Select(localization =>
                    ((string?)localization.Locale,
                        (string?)localization.Name,
                        localization.Description))],
                caller);

            if (localized.IsFailure)
            {
                return Result.Failure<PublicHolidayResponse>(localized.Error);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            IntelligenceDirectoryMapper.ToResponse(holiday, locale: null, forEditor: true));
    }
}

/// <summary>Removes a calendar entry, through the platform's soft-delete path.</summary>
public sealed class ArchivePublicHolidayCommandHandler
    : ICommandHandler<ArchivePublicHolidayCommand>
{
    private readonly IPublicHolidayRepository _holidays;
    private readonly IUnitOfWork _unitOfWork;

    public ArchivePublicHolidayCommandHandler(
        IPublicHolidayRepository holidays, IUnitOfWork unitOfWork)
    {
        _holidays = holidays;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        ArchivePublicHolidayCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var holiday = await _holidays.GetAsync(
            PublicHolidayId.From(command.PublicHolidayId), cancellationToken);

        if (holiday is null)
        {
            return Result.Failure(IntelligenceErrors.HolidayNotFound);
        }

        _holidays.Remove(holiday);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

/// <summary>Returns an archived calendar entry, reading past the query filter.</summary>
public sealed class RestorePublicHolidayCommandHandler
    : ICommandHandler<RestorePublicHolidayCommand, PublicHolidayResponse>
{
    private readonly IPublicHolidayRepository _holidays;
    private readonly IUnitOfWork _unitOfWork;

    public RestorePublicHolidayCommandHandler(
        IPublicHolidayRepository holidays, IUnitOfWork unitOfWork)
    {
        _holidays = holidays;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PublicHolidayResponse>> Handle(
        RestorePublicHolidayCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var holiday = await _holidays.GetIncludingArchivedAsync(
            PublicHolidayId.From(command.PublicHolidayId), cancellationToken);

        if (holiday is null)
        {
            return Result.Failure<PublicHolidayResponse>(IntelligenceErrors.HolidayNotFound);
        }

        if (!holiday.IsDeleted)
        {
            return Result.Failure<PublicHolidayResponse>(IntelligenceErrors.HolidayNotArchived);
        }

        holiday.Restore();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            IntelligenceDirectoryMapper.ToResponse(holiday, locale: null, forEditor: true));
    }
}
