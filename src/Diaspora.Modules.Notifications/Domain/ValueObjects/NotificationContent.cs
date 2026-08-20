using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Policies;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.ValueObjects;

/// <summary>
/// The rendered words a member actually sees, captured at creation.
/// <para>
/// <b>Rendered once and stored, never re-rendered on read.</b> The template it came from will be
/// edited, republished and eventually deactivated; the variables it interpolated — a job title, an
/// employer's name, a price — describe a moment that has passed. Re-rendering on read would quietly
/// rewrite history, so that a member reading last month's message about a listing that has since
/// been renamed would see words nobody ever sent them.
/// </para>
/// <para>
/// It carries the language it was rendered in, because that is what the message <em>is</em>, not
/// what the member currently prefers — somebody who switches to French next week should still see
/// the English notification they were sent.
/// </para>
/// </summary>
public sealed class NotificationContent : ValueObject
{
    private NotificationContent(
        string title,
        string body,
        string language,
        ContentFormat format,
        string? shortBody,
        Guid? imageMediaId)
    {
        Title = title;
        Body = body;
        Language = language;
        Format = format;
        ShortBody = shortBody;
        ImageMediaId = imageMediaId;
    }

    private NotificationContent() { }

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    /// <summary>The BCP-47 tag this was rendered in. Never the member's current preference.</summary>
    public string Language { get; private set; } = LocaleFallbackChain.DefaultLanguage;

    public ContentFormat Format { get; private set; }

    /// <summary>
    /// The push-sized variant. Null when the body already fits, so the push channel does not have
    /// to guess whether truncating is safe.
    /// </summary>
    public string? ShortBody { get; private set; }

    /// <summary>A B5 asset identifier, never a URL. ADR-005.</summary>
    public Guid? ImageMediaId { get; private set; }

    public static NotificationContent Create(
        string title,
        string body,
        string? language = null,
        ContentFormat format = ContentFormat.Plain,
        string? shortBody = null,
        Guid? imageMediaId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        var normalizedLanguage = string.IsNullOrWhiteSpace(language)
            ? LocaleFallbackChain.DefaultLanguage
            : LocaleFallbackChain.Normalize(language);

        var trimmedBody = Clip(body, NotificationLimits.MaxBodyLength)!;

        return new NotificationContent(
            Clip(title, NotificationLimits.MaxTitleLength)!,
            trimmedBody,
            normalizedLanguage,
            format,
            ResolveShortBody(shortBody, trimmedBody),
            imageMediaId);
    }

    /// <summary>
    /// The text a push payload should carry.
    /// <para>
    /// Falls back to the full body when it already fits, so a channel never has to decide whether a
    /// missing short form means "use the long one" or "send nothing".
    /// </para>
    /// </summary>
    public string PushBody => ShortBody ?? Body;

    private static string? ResolveShortBody(string? supplied, string body)
    {
        if (!string.IsNullOrWhiteSpace(supplied))
        {
            return Clip(supplied, NotificationLimits.MaxPushBodyLength);
        }

        return body.Length <= NotificationLimits.MaxPushBodyLength
            ? null
            : Clip(body, NotificationLimits.MaxPushBodyLength);
    }

    private static string? Clip(string? value, int max) =>
        value is null ? null : value.Length <= max ? value.Trim() : value[..max].Trim();

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Title;
        yield return Body;
        yield return Language;
        yield return Format;
        yield return ShortBody;
        yield return ImageMediaId;
    }
}
