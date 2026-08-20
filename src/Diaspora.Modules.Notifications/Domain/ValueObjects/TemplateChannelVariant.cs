using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Policies;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.ValueObjects;

/// <summary>
/// How one template reads on one particular transport.
/// <para>
/// <b>Optional by design.</b> Most messages read the same everywhere and forcing an author to write
/// three variants of "your interview is confirmed" would guarantee two of them drift. A variant
/// exists only where the transport genuinely changes the writing: email has room for a paragraph and
/// a subject line that must stand alone in a list; push has forty words and no context.
/// </para>
/// <para>
/// A variant overrides; it does not replace. Anything it leaves null falls through to the
/// localization, so adding a channel later cannot blank a message.
/// </para>
/// </summary>
public sealed class TemplateChannelVariant : ValueObject
{
    private TemplateChannelVariant(
        Guid variantId,
        NotificationChannelKind channel,
        string language,
        string? title,
        string? body,
        ContentFormat? format)
    {
        VariantId = variantId;
        Channel = channel;
        Language = language;
        Title = title;
        Body = body;
        Format = format;
    }

    private TemplateChannelVariant() { }

    /// <summary>Caller-supplied key. The configuration declares <c>ValueGeneratedNever</c>.</summary>
    public Guid VariantId { get; private set; }

    public NotificationChannelKind Channel { get; private set; }

    /// <summary>The language this variant is written in. A variant is per channel and per language.</summary>
    public string Language { get; private set; } = LocaleFallbackChain.DefaultLanguage;

    /// <summary>Overrides the localization's title. Null to inherit.</summary>
    public string? Title { get; private set; }

    /// <summary>Overrides the localization's body. Null to inherit.</summary>
    public string? Body { get; private set; }

    /// <summary>Overrides the localization's format. Null to inherit.</summary>
    public ContentFormat? Format { get; private set; }

    public static TemplateChannelVariant Create(
        Guid variantId,
        NotificationChannelKind channel,
        string language,
        string? title = null,
        string? body = null,
        ContentFormat? format = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);

        return new TemplateChannelVariant(
            variantId,
            channel,
            LocaleFallbackChain.Normalize(language),
            Clip(title, NotificationLimits.MaxTitleLength),
            Clip(body, NotificationLimits.MaxBodyLength),
            format);
    }

    private static string? Clip(string? value, int max) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().Length <= max ? value.Trim() : value.Trim()[..max];

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return VariantId;
        yield return Channel;
        yield return Language;
        yield return Title;
        yield return Body;
        yield return Format;
    }
}
