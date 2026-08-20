using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Templates;

namespace Diaspora.Modules.Notifications.Extensibility.Templates;

/// <summary>
/// Everything a render needs.
/// <para>
/// The template arrives as an aggregate rather than a key so the renderer does no I/O — which is
/// what makes it synchronous, trivially testable, and safe to call once per channel without three
/// round trips.
/// </para>
/// </summary>
public sealed record TemplateRenderRequest
{
    public required NotificationTemplate Template { get; init; }

    /// <summary>The recipient's language. Walked through the fallback chain by the renderer.</summary>
    public string? Language { get; init; }

    /// <summary>
    /// The recipient's time zone, so a date renders in their wall clock.
    /// <para>
    /// Without it a member in Lagos reads "your interview is at 09:00" about an interview at 10:00,
    /// which is the single most consequential formatting bug this module could ship.
    /// </para>
    /// </summary>
    public string? TimeZoneId { get; init; }

    /// <summary>
    /// The values, keyed on the declared variable names.
    /// <para>
    /// Values are <c>object?</c> rather than strings so the renderer can apply the declared format:
    /// a <c>DateTimeOffset</c> becomes a local wall clock, a decimal with a currency becomes money in
    /// the reader's culture. Passing a pre-formatted string works and is how a caller opts out.
    /// </para>
    /// </summary>
    public required IReadOnlyDictionary<string, object?> Values { get; init; }

    /// <summary>Which channel to apply variants for. Null renders the localization as written.</summary>
    public NotificationChannelKind? Channel { get; init; }

    /// <summary>Chooses the plural form when the template has one.</summary>
    public int? Count { get; init; }
}
