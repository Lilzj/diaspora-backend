using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Intelligence.Mapping;
using Diaspora.Modules.Guides.Application.Intelligence.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Emergency;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Intelligence.Directory;

/// <summary>
/// Saves one country's emergency information, creating the page on first save.
/// <para>
/// <b>Create-or-update in one handler, because the editorial act is the same either way.</b> An
/// editor opening a country that has no emergency page yet and one correcting an existing page are
/// doing the same job with the same screen, and a separate create command would mean the client has
/// to know which — and would get it wrong exactly once, on the country nobody had done yet.
/// </para>
/// <para>
/// <b>Contacts are applied before translations, and the order is load-bearing.</b> A translation must
/// name a kind the country lists, and the aggregate refuses one that does not — so sending both in
/// one save only works if the new contacts land first. Reversed, an editor adding a contact and its
/// translation together would be told the kind does not exist.
/// </para>
/// </summary>
public sealed class SaveEmergencyContactsCommandHandler
    : ICommandHandler<SaveEmergencyContactsCommand, EmergencyContactsResponse>
{
    private readonly ICountryEmergencyDirectoryRepository _directories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IntelligenceAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public SaveEmergencyContactsCommandHandler(
        ICountryEmergencyDirectoryRepository directories,
        IUnitOfWork unitOfWork,
        IntelligenceAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock)
    {
        _directories = directories;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<EmergencyContactsResponse>> Handle(
        SaveEmergencyContactsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.CallerId;
        var request = command.Request;

        var directory = await _directories.GetForCountryAsync(
            command.CountryCode, cancellationToken);

        if (directory is null)
        {
            var created = CountryEmergencyDirectory.Create(
                CountryEmergencyDirectoryId.From(_guids.NewSequentialGuid()),
                command.CountryCode,
                _clock.UtcNow,
                caller);

            if (created.IsFailure)
            {
                return Result.Failure<EmergencyContactsResponse>(created.Error);
            }

            directory = created.Value;

            await _directories.AddAsync(directory, cancellationToken);
        }
        else
        {
            var revision = directory.EnsureRevision(request.ExpectedRevision);

            if (revision.IsFailure)
            {
                return Result.Failure<EmergencyContactsResponse>(revision.Error);
            }
        }

        var guided = directory.SetGuidance(request.Guidance, caller);

        if (guided.IsFailure)
        {
            return Result.Failure<EmergencyContactsResponse>(guided.Error);
        }

        // Contacts first. A translation naming a kind the country does not list is refused, so the
        // new contact set has to be in place before the translations that describe it.
        var contacts = directory.SetContacts(
            [.. request.Contacts.Select(contact =>
                ((string?)contact.Kind,
                    (string?)contact.Label,
                    (string?)contact.Number,
                    contact.AlternateNumber,
                    contact.Note,
                    contact.IsAvailableAllHours))],
            caller);

        if (contacts.IsFailure)
        {
            return Result.Failure<EmergencyContactsResponse>(contacts.Error);
        }

        var localizations = directory.SetLocalizations(
            [.. request.Localizations.Select(localization =>
                ((string?)localization.Locale,
                    (string?)localization.Kind,
                    (string?)localization.Label,
                    localization.Note))],
            caller);

        if (localizations.IsFailure)
        {
            return Result.Failure<EmergencyContactsResponse>(localizations.Error);
        }

        if (request.MarkReviewed)
        {
            var reviewed = directory.MarkReviewed(_clock.UtcNow, caller);

            if (reviewed.IsFailure)
            {
                return Result.Failure<EmergencyContactsResponse>(reviewed.Error);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            IntelligenceDirectoryMapper.ToResponse(directory, locale: null, forEditor: true));
    }
}
