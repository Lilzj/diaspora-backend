using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Location.Application.Mapping;
using Diaspora.Modules.Location.Application.Services;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.Errors;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Application.Profile;

/// <summary>
/// Applies a partial preference update.
/// <para>
/// Null means "leave alone" on every field, so a client changing only the radius need not
/// round-trip the rest. The alternative — read, mutate, write back — loses any concurrent
/// change to a field the client was not interested in, which on a profile edited from two
/// devices is not hypothetical.
/// </para>
/// </summary>
public sealed class UpdateGeographicPreferencesCommandHandler
    : ICommandHandler<UpdateGeographicPreferencesCommand, LocationProfileResponse>
{
    private readonly LocationProfileProvisioner _profiles;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateGeographicPreferencesCommandHandler(
        LocationProfileProvisioner profiles, ICurrentUser currentUser, IUnitOfWork unitOfWork)
    {
        _profiles = profiles;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<LocationProfileResponse>> Handle(
        UpdateGeographicPreferencesCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure<LocationProfileResponse>(LocationErrors.NotAuthenticated);
        }

        var profile = await _profiles.GetOrCreateAsync(userId, cancellationToken);
        var payload = request.Preferences;
        var preferences = profile.Preferences;

        if (payload.SearchRadiusKm is not null)
        {
            var radius = SearchRadius.FromClientValue(payload.SearchRadiusKm);

            if (radius.IsFailure)
            {
                return Result.Failure<LocationProfileResponse>(radius.Error);
            }

            preferences = preferences.WithSearchRadius(radius.Value);
        }

        if (TryParse<DistanceUnit>(payload.DistanceUnit) is { } unit)
        {
            preferences = preferences.WithDistanceUnit(unit);
        }

        if (TryParse<CoordinatePrecision>(payload.CoordinatePrecision) is { } precision)
        {
            preferences = preferences.WithCoordinatePrecision(precision);
        }

        if (payload.PreferredCountryCode is not null)
        {
            var country = CountryCode.TryCreate(payload.PreferredCountryCode);

            if (country.IsFailure)
            {
                return Result.Failure<LocationProfileResponse>(country.Error);
            }

            preferences = preferences.WithPreferredCountry(country.Value);
        }

        if (payload.PreferredAreaId is not null)
        {
            preferences = preferences.WithPreferredArea(AreaId.From(payload.PreferredAreaId.Value));
        }

        profile.UpdatePreferences(preferences);

        if (payload.HomeCountryCode is not null)
        {
            var home = CountryCode.TryCreate(payload.HomeCountryCode);

            if (home.IsFailure)
            {
                return Result.Failure<LocationProfileResponse>(home.Error);
            }

            profile.SetHomeCountry(home.Value);
        }

        if (payload.OriginCountryCode is not null)
        {
            var origin = CountryCode.TryCreate(payload.OriginCountryCode);

            if (origin.IsFailure)
            {
                return Result.Failure<LocationProfileResponse>(origin.Error);
            }

            profile.SetOriginCountry(origin.Value);
        }

        if (TryParse<LocationPermissionStatus>(payload.PermissionStatus) is { } status)
        {
            profile.SetPermissionStatus(status);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ProfileMapper.ToResponse(profile));
    }

    /// <summary>
    /// Case-insensitive so the client's lower-case <c>permissionStatus</c> union binds to the
    /// PascalCase enum without the client having to change.
    /// </summary>
    private static TEnum? TryParse<TEnum>(string? value) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : null;
}
