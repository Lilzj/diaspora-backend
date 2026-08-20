using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Notifications.Application.Mapping;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Contracts.V1.Requests;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.Modules.Notifications.Domain.Catalog;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Errors;
using Diaspora.Modules.Notifications.Domain.Preferences;
using Diaspora.Modules.Notifications.Domain.ValueObjects;
using Diaspora.Modules.Notifications.Options;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Notifications.Application.Preferences;

/// <summary>
/// Applies whichever sections the request named.
/// <para>
/// <b>A refused section fails the whole request rather than being skipped.</b> A member who sets
/// quiet hours to 09:00–09:00 and also switches email on must be told the first was invalid — saving
/// the half that worked and silently dropping the rest is how somebody ends up believing they
/// configured something they did not.
/// </para>
/// <para>
/// <b>Muting a mandatory category is refused, not ignored.</b> The catalogue says security cannot be
/// silenced; a screen that accepted the change and kept delivering would be lying to the member.
/// </para>
/// </summary>
public sealed class UpdateNotificationPreferencesCommandHandler
    : ICommandHandler<UpdateNotificationPreferencesCommand, NotificationPreferencesResponse>
{
    private readonly INotificationPreferenceRepository _preferences;
    private readonly INotificationCategoryRepository _categories;
    private readonly NotificationAuthorizationService _authorization;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOptions<NotificationOptions> _options;

    public UpdateNotificationPreferencesCommandHandler(
        INotificationPreferenceRepository preferences,
        INotificationCategoryRepository categories,
        NotificationAuthorizationService authorization,
        IUnitOfWork unitOfWork,
        IOptions<NotificationOptions> options)
    {
        _preferences = preferences;
        _categories = categories;
        _authorization = authorization;
        _unitOfWork = unitOfWork;
        _options = options;
    }

    public async Task<Result<NotificationPreferencesResponse>> Handle(
        UpdateNotificationPreferencesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<NotificationPreferencesResponse>(caller.Error);
        }

        var preference = await _preferences.GetOrCreateAsync(caller.Value, cancellationToken);
        var categories = await _categories.ListActiveAsync(cancellationToken);
        var request = command.Request;

        // Applied in order and stopped on the first refusal. A partially applied settings screen is
        // how somebody comes to believe they configured something they did not.
        Result[] steps =
        [
            ApplyChannels(preference, request),
            ApplyCategories(preference, request, categories),
            ApplyQuietHours(preference, request),
            ApplyDigest(preference, request),
            ApplyLocale(preference, request)
        ];

        foreach (var step in steps)
        {
            if (step.IsFailure)
            {
                return Result.Failure<NotificationPreferencesResponse>(step.Error);
            }
        }

        if (request.DoNotDisturb is { } doNotDisturb)
        {
            preference.SetDoNotDisturb(doNotDisturb);
        }

        if (request.AllowsMarketing is { } marketing)
        {
            preference.SetMarketing(marketing);
        }

        _preferences.Update(preference);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(PreferenceMapper.ToResponse(
            preference, categories, _options.Value.DefaultLanguage, _options.Value.DefaultTimeZoneId));
    }

    private static Result ApplyChannels(
        NotificationPreference preference, UpdateNotificationPreferencesRequest request)
    {
        if (request.Push is { } push)
        {
            var result = preference.SetChannel(NotificationChannelKind.Push, push);

            if (result.IsFailure)
            {
                return result;
            }
        }

        if (request.Email is { } email)
        {
            var result = preference.SetChannel(NotificationChannelKind.Email, email);

            if (result.IsFailure)
            {
                return result;
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Applies both category shapes.
    /// <para>
    /// The coarse map first — the existing client's one boolean per subject area — then the
    /// per-channel form on top, so a client sending both gets the finer one where they overlap.
    /// </para>
    /// </summary>
    private static Result ApplyCategories(
        NotificationPreference preference,
        UpdateNotificationPreferencesRequest request,
        IReadOnlyList<NotificationCategory> categories)
    {
        if (request.Categories is { Count: > 0 })
        {
            foreach (var (key, enabled) in request.Categories)
            {
                var refusal = RefuseIfMandatory(key, !enabled, categories);

                if (refusal.IsFailure)
                {
                    return refusal;
                }

                var result = preference.SetCategoryEnabled(key, enabled);

                if (result.IsFailure)
                {
                    return result;
                }
            }
        }

        if (request.CategoryChannels is not { Count: > 0 })
        {
            return Result.Success();
        }

        foreach (var entry in request.CategoryChannels)
        {
            var refusal = RefuseIfMandatory(entry.CategoryKey, entry.IsMuted == true, categories);

            if (refusal.IsFailure)
            {
                return refusal;
            }

            var current = preference.CategoryFor(entry.CategoryKey);

            var result = preference.SetCategory(CategoryPreference.Create(
                entry.CategoryKey,
                entry.InApp ?? current.InApp,
                entry.Push ?? current.Push,
                entry.Email ?? current.Email,
                entry.UseDigest ?? current.UseDigest,
                entry.IsMuted ?? current.IsMuted));

            if (result.IsFailure)
            {
                return result;
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Refuses an attempt to silence a category the platform will not let a member silence.
    /// <para>
    /// Refused rather than ignored: a screen that accepted the change and kept delivering would be
    /// lying, and the member would reasonably conclude the platform ignores their settings.
    /// </para>
    /// </summary>
    private static Result RefuseIfMandatory(
        string categoryKey, bool muting, IReadOnlyList<NotificationCategory> categories)
    {
        if (!muting)
        {
            return Result.Success();
        }

        var key = categoryKey.Trim().ToLowerInvariant();

        var category = categories.FirstOrDefault(entry =>
            string.Equals(entry.Key, key, StringComparison.Ordinal));

        return category?.IsMandatory == true
            ? Result.Failure(NotificationErrors.CriticalChannelRequired)
            : Result.Success();
    }

    private static Result ApplyQuietHours(
        NotificationPreference preference, UpdateNotificationPreferencesRequest request)
    {
        if (request.QuietHoursEnabled is null
            && request.QuietHoursStart is null
            && request.QuietHoursEnd is null)
        {
            return Result.Success();
        }

        return preference.SetQuietHours(
            request.QuietHoursEnabled ?? preference.QuietHours.IsEnabled,
            request.QuietHoursStart ?? preference.QuietHours.StartHour,
            request.QuietHoursEnd ?? preference.QuietHours.EndHour);
    }

    private static Result ApplyDigest(
        NotificationPreference preference, UpdateNotificationPreferencesRequest request)
    {
        if (request.DigestFrequency is null
            && request.DigestHour is null
            && request.DigestDayOfWeek is null)
        {
            return Result.Success();
        }

        var frequency = NotificationMapper.ParseEnum<DigestFrequency>(request.DigestFrequency)
                        ?? preference.Digest.Frequency;

        var day = NotificationMapper.ParseEnum<DayOfWeek>(request.DigestDayOfWeek)
                  ?? preference.Digest.DayOfWeek;

        return preference.SetDigest(
            frequency, request.DigestHour ?? preference.Digest.HourOfDay, day);
    }

    private static Result ApplyLocale(
        NotificationPreference preference, UpdateNotificationPreferencesRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Language))
        {
            var result = preference.SetLanguage(request.Language);

            if (result.IsFailure)
            {
                return result;
            }
        }

        return string.IsNullOrWhiteSpace(request.TimeZoneId)
            ? Result.Success()
            : preference.SetTimeZone(request.TimeZoneId);
    }
}
