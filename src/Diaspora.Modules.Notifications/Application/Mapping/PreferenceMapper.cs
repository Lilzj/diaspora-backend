using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.Modules.Notifications.Domain.Catalog;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Preferences;

namespace Diaspora.Modules.Notifications.Application.Mapping;

/// <summary>
/// Turns the preference aggregate into the settings screen's shape.
/// <para>
/// <b>The category list is driven by the catalogue, not by the member's saved rows.</b> A member who
/// has never touched a switch has no rows at all, and a response built from what they saved would
/// show an empty settings screen. Every active category appears, with the member's setting where
/// they have one and the platform default where they do not.
/// </para>
/// </summary>
public static class PreferenceMapper
{
    public static NotificationPreferencesResponse ToResponse(
        NotificationPreference? preference,
        IReadOnlyList<NotificationCategory> categories,
        string defaultLanguage,
        string defaultTimeZoneId)
    {
        ArgumentNullException.ThrowIfNull(categories);

        return new NotificationPreferencesResponse
        {
            // Always true and always sent: the client renders a disabled switch rather than
            // pretending the inbox can be turned off.
            InApp = true,
            Push = preference?.IsChannelEnabled(NotificationChannelKind.Push) ?? true,
            Email = preference?.IsChannelEnabled(NotificationChannelKind.Email) ?? false,
            Categories = categories.Select(category => ToResponse(category, preference)).ToList(),
            Communication = ToCommunication(preference, defaultLanguage, defaultTimeZoneId)
        };
    }

    private static CategoryPreferenceResponse ToResponse(
        NotificationCategory category, NotificationPreference? preference)
    {
        var setting = preference?.CategoryFor(category.Key);

        // A mandatory category is reported as on whatever the member saved, because that is what the
        // dispatcher will actually do. Reporting their stored preference would show a switch that
        // says off beside messages that keep arriving.
        var muted = !category.IsMandatory && setting?.IsMuted == true;

        return new CategoryPreferenceResponse
        {
            Key = category.Key,
            DisplayName = category.DisplayName,
            Description = category.Description,
            Enabled = !muted,
            InApp = category.IsMandatory || setting?.InApp != false,
            Push = !muted && (category.IsMandatory || setting?.Push != false),
            Email = !muted && setting?.Email == true,
            UseDigest = setting?.UseDigest ?? category.DigestByDefault,
            IsMandatory = category.IsMandatory,
            Position = category.Position
        };
    }

    private static CommunicationPreferencesResponse ToCommunication(
        NotificationPreference? preference, string defaultLanguage, string defaultTimeZoneId) =>
        new()
        {
            DoNotDisturb = preference?.DoNotDisturb ?? false,
            QuietHoursEnabled = preference?.QuietHours.IsEnabled ?? false,
            QuietHoursStart = preference?.QuietHours.StartHour ?? 22,
            QuietHoursEnd = preference?.QuietHours.EndHour ?? 7,
            DigestFrequency = (preference?.Digest.Frequency ?? DigestFrequency.Daily).ToString(),
            DigestHour = preference?.Digest.HourOfDay ?? 8,
            DigestDayOfWeek = (preference?.Digest.DayOfWeek ?? DayOfWeek.Monday).ToString(),
            Language = preference?.Language ?? defaultLanguage,
            TimeZoneId = preference?.TimeZoneId ?? defaultTimeZoneId,
            AllowsMarketing = preference?.AllowsMarketing ?? false,

            // Stated rather than implied. A settings screen that did not say this would be
            // misrepresenting every switch above it.
            CriticalOverrideEnabled = true
        };

    public static NotificationCategoryResponse ToResponse(NotificationCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);

        return new NotificationCategoryResponse
        {
            Key = category.Key,
            DisplayName = category.DisplayName,
            Description = category.Description,
            DefaultPriority = category.DefaultPriority.ToString(),
            IsMandatory = category.IsMandatory,
            DigestByDefault = category.DigestByDefault,
            OwnerModule = category.OwnerModule,
            Position = category.Position
        };
    }
}
