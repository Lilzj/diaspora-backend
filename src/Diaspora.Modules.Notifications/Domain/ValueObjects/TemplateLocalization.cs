using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Policies;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.ValueObjects;

/// <summary>
/// One template's words in one language.
/// <para>
/// <b>Written by a person, always.</b> There is no machine translation anywhere in this module, and
/// a locale with no localization falls back through <see cref="LocaleFallbackChain"/> rather than
/// being generated. A wrong translation of "your account has been suspended" is worse than an
/// English one.
/// </para>
/// <para>
/// <see cref="PluralBody"/> is the module's whole answer to pluralisation, and it is deliberately
/// the simple one: a singular form and a plural form, chosen by a count the caller supplies. Full
/// CLDR plural categories — zero, one, two, few, many, other — would be correct and would require
/// every template author to understand Arabic's six forms before writing an English notification.
/// The renderer picks plural when the count is not one and the form exists, and the singular
/// otherwise, which is right for every language this platform currently ships in and honestly wrong
/// for some it will.
/// </para>
/// </summary>
public sealed class TemplateLocalization : ValueObject
{
    private TemplateLocalization(
        Guid localizationId,
        string language,
        string title,
        string body,
        string? shortBody,
        string? pluralBody,
        ContentFormat format)
    {
        LocalizationId = localizationId;
        Language = language;
        Title = title;
        Body = body;
        ShortBody = shortBody;
        PluralBody = pluralBody;
        Format = format;
    }

    private TemplateLocalization() { }

    /// <summary>Caller-supplied key. The configuration declares <c>ValueGeneratedNever</c>.</summary>
    public Guid LocalizationId { get; private set; }

    /// <summary>A normalised BCP-47 tag — <c>en</c>, <c>fr</c>, <c>pt-BR</c>.</summary>
    public string Language { get; private set; } = LocaleFallbackChain.DefaultLanguage;

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    /// <summary>The push-length variant, when the author wrote one.</summary>
    public string? ShortBody { get; private set; }

    /// <summary>The form used when the count is not one. See the class summary.</summary>
    public string? PluralBody { get; private set; }

    public ContentFormat Format { get; private set; }

    public static TemplateLocalization Create(
        Guid localizationId,
        string language,
        string title,
        string body,
        string? shortBody = null,
        string? pluralBody = null,
        ContentFormat format = ContentFormat.Plain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        return new TemplateLocalization(
            localizationId,
            LocaleFallbackChain.Normalize(language),
            Clip(title, NotificationLimits.MaxTitleLength)!,
            Clip(body, NotificationLimits.MaxBodyLength)!,
            Clip(shortBody, NotificationLimits.MaxPushBodyLength),
            Clip(pluralBody, NotificationLimits.MaxBodyLength),
            format);
    }

    /// <summary>The body to render for a given count. Falls back to the singular when no plural exists.</summary>
    public string BodyFor(int? count) =>
        count is { } value && value != 1 && !string.IsNullOrWhiteSpace(PluralBody)
            ? PluralBody
            : Body;

    private static string? Clip(string? value, int max) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().Length <= max ? value.Trim() : value.Trim()[..max];

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return LocalizationId;
        yield return Language;
        yield return Title;
        yield return Body;
        yield return ShortBody;
        yield return PluralBody;
        yield return Format;
    }
}
