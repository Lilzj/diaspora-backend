using Diaspora.Modules.Notifications.Domain.Policies;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.ValueObjects;

/// <summary>
/// A hole in a template and what is allowed to fill it.
/// <para>
/// <b>Declared, not inferred.</b> A renderer that simply substituted whatever it was handed would
/// silently produce "Your interview with  is at " the first time a publisher forgot a field, and
/// the member would receive it. Declaring the variables means the template can be validated when it
/// is published rather than when somebody is waiting for it.
/// </para>
/// <para>
/// <see cref="Format"/> is a hint the renderer honours for dates, times and money — the three things
/// that are wrong in a different way in every culture. It is never a format string, because a format
/// string in reference data is code in reference data.
/// </para>
/// </summary>
public sealed class TemplateVariable : ValueObject
{
    /// <summary>Rendered with the recipient's culture as a long date.</summary>
    public const string DateFormat = "date";

    /// <summary>Rendered with the recipient's culture and time zone as a wall clock.</summary>
    public const string TimeFormat = "time";

    /// <summary>Rendered as a date and a time together.</summary>
    public const string DateTimeFormat = "datetime";

    /// <summary>Rendered with the currency symbol the value carries.</summary>
    public const string CurrencyFormat = "currency";

    /// <summary>Rendered with the recipient's culture's group separators.</summary>
    public const string NumberFormat = "number";

    private TemplateVariable(Guid variableId, string name, bool isRequired, string? format, string? sample)
    {
        VariableId = variableId;
        Name = name;
        IsRequired = isRequired;
        Format = format;
        Sample = sample;
    }

    private TemplateVariable() { }

    /// <summary>Caller-supplied key. The configuration declares <c>ValueGeneratedNever</c>.</summary>
    public Guid VariableId { get; private set; }

    /// <summary>The token inside the braces. Case-insensitive at render time, lower-cased here.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Whether publishing without a value for it is a failure rather than an empty string.</summary>
    public bool IsRequired { get; private set; }

    /// <summary>One of the format constants on this type, or null for plain text.</summary>
    public string? Format { get; private set; }

    /// <summary>What it looks like, so a template author can preview without sending anything.</summary>
    public string? Sample { get; private set; }

    public static TemplateVariable Create(
        Guid variableId, string name, bool isRequired = true, string? format = null, string? sample = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var normalized = name.Trim().ToLowerInvariant();

        if (normalized.Length > NotificationLimits.MaxVariableNameLength)
        {
            normalized = normalized[..NotificationLimits.MaxVariableNameLength];
        }

        return new TemplateVariable(variableId, normalized, isRequired, Clip(format), Clip(sample));
    }

    private static string? Clip(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= NotificationLimits.MaxVariableValueLength
            ? trimmed
            : trimmed[..NotificationLimits.MaxVariableValueLength];
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return VariableId;
        yield return Name;
        yield return IsRequired;
        yield return Format;
        yield return Sample;
    }
}
