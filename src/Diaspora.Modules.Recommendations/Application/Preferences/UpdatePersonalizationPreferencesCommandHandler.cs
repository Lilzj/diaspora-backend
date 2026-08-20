using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Recommendations.Application.Mapping;
using Diaspora.Modules.Recommendations.Application.Services;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Preferences;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Recommendations.Application.Preferences;

/// <summary>
/// Applies a member's stated changes.
/// <para>
/// <b>Creates the preference row on first write, and only on a write.</b> A member who has never
/// opened the settings screen has no row, which is what makes every row in the table mean "somebody
/// made a decision" — see <c>GetPersonalizationPreferencesQueryHandler</c>.
/// </para>
/// <para>
/// <b>Absent collections are left untouched.</b> A client changing only the language must not
/// silently unmute every category, which a handler that treated omission as "clear" would do.
/// </para>
/// </summary>
public sealed class UpdatePersonalizationPreferencesCommandHandler
    : ICommandHandler<UpdatePersonalizationPreferencesCommand, PersonalizationPreferencesResponse>
{
    private readonly IPersonalizationPreferenceRepository _preferences;
    private readonly RecommendationAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IUnitOfWork _unitOfWork;

    public UpdatePersonalizationPreferencesCommandHandler(
        IPersonalizationPreferenceRepository preferences,
        RecommendationAuthorizationService authorization,
        IGuidGenerator guids,
        IUnitOfWork unitOfWork)
    {
        _preferences = preferences;
        _authorization = authorization;
        _guids = guids;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PersonalizationPreferencesResponse>> Handle(
        UpdatePersonalizationPreferencesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<PersonalizationPreferencesResponse>(caller.Error);
        }

        var preference = await _preferences.GetOrCreateAsync(caller.Value, cancellationToken);

        if (command.IsPersonalizationEnabled is { } personalization)
        {
            preference.SetPersonalizationEnabled(personalization);
        }

        if (command.ShareActivitySignals is { } share)
        {
            preference.SetShareActivitySignals(share);
        }

        if (command.PreferredLanguage is not null)
        {
            preference.SetPreferredLanguage(command.PreferredLanguage);
        }

        if (command.PreferredCountryCode is not null)
        {
            preference.SetPreferredCountry(command.PreferredCountryCode);
        }

        ApplyCategories(command, preference);
        ApplySurfaces(command, preference);
        ApplyLocations(command, preference);

        _preferences.Update(preference);

        // A privacy decision that reported success and was not persisted is the worst failure this
        // handler could have.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(RecommendationMapper.ToResponse(preference));
    }

    private void ApplyCategories(
        UpdatePersonalizationPreferencesCommand command, PersonalizationPreference preference)
    {
        foreach (var request in command.Categories ?? [])
        {
            var targetType = string.IsNullOrWhiteSpace(request.TargetType)
                ? (RecommendationTargetType?)null
                : Enum.Parse<RecommendationTargetType>(request.TargetType, ignoreCase: true);

            // Clearing is distinct from setting IsMuted to false, which is a positive preference.
            // Without the distinction a member could unmute but never return to neutral.
            if (request.Clear)
            {
                preference.ClearCategory(request.CategoryKey, targetType);
                continue;
            }

            preference.SetCategory(
                request.CategoryKey, request.IsMuted, targetType, _guids.NewSequentialGuid());
        }
    }

    private void ApplySurfaces(
        UpdatePersonalizationPreferencesCommand command, PersonalizationPreference preference)
    {
        foreach (var request in command.Surfaces ?? [])
        {
            preference.SetSurfaceEnabled(
                Enum.Parse<RecommendationSurface>(request.Surface, ignoreCase: true),
                request.IsEnabled,
                _guids.NewSequentialGuid());
        }
    }

    private void ApplyLocations(
        UpdatePersonalizationPreferencesCommand command, PersonalizationPreference preference)
    {
        foreach (var request in command.FavoriteLocations ?? [])
        {
            var scope = Enum.Parse<AffinityScope>(request.Scope, ignoreCase: true);

            if (request.Remove)
            {
                preference.RemoveFavoriteLocation(scope, request.Key);
                continue;
            }

            preference.AddFavoriteLocation(
                scope, request.Key, request.DisplayName, _guids.NewSequentialGuid());
        }

    }
}
