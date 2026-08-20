using Diaspora.Modules.Notifications.Domain.Preferences;
using Diaspora.Modules.Notifications.Options;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Notifications.Application.Services;

/// <summary>
/// Everything about a member that rendering and routing need, resolved once per dispatch.
/// <para>
/// <b>One lookup, not three.</b> The renderer wants a language, quiet hours want a time zone, the
/// policy resolver wants the whole preference record — and all three come from the same row. Loading
/// it once and passing this around keeps a dispatch at one preference query however many rules match.
/// </para>
/// <para>
/// <see cref="Preference"/> is null for a member who has never opened the settings screen, which is
/// most of them. The language and zone fall back to the deployment's defaults, so the absence of a
/// row never changes what a member receives — only what they can change.
/// </para>
/// </summary>
public sealed record NotificationRecipientContext
{
    public required UserId UserId { get; init; }

    public NotificationPreference? Preference { get; init; }

    public required string Language { get; init; }

    public required string TimeZoneId { get; init; }

    public static NotificationRecipientContext For(
        UserId userId, NotificationPreference? preference, IOptions<NotificationOptions> options)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(options);

        return new NotificationRecipientContext
        {
            UserId = userId,
            Preference = preference,
            Language = preference?.Language ?? options.Value.DefaultLanguage,
            TimeZoneId = preference?.TimeZoneId ?? options.Value.DefaultTimeZoneId
        };
    }
}
