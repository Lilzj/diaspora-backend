using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.ValueObjects;

/// <summary>
/// One tuning knob on a strategy or a policy, as a string.
/// <para>
/// <b>A string, deliberately, and the alternative was worse.</b> A radius is a number, a demotion
/// factor is a decimal, a country allow-list is a list and a "treat these categories as equivalent"
/// rule is a map. Typing the column means a column per shape and a migration per new parameter;
/// leaving it a string means a strategy parses what it declared it needs, and a parse failure is a
/// startup validation error naming the parameter rather than a silent zero.
/// </para>
/// <para>
/// The parsing lives on this type rather than in each strategy so that "0.5", " 0.5 " and "50%" do
/// not mean three different things depending on which strategy read them.
/// </para>
/// </summary>
public sealed class ConfigurationParameter : ValueObject
{
    private ConfigurationParameter(Guid parameterId, string key, string value)
    {
        ParameterId = parameterId;
        Key = key;
        Value = value;
    }

    private ConfigurationParameter() { }

    /// <summary>Caller-supplied key. The configuration declares <c>ValueGeneratedNever</c>.</summary>
    public Guid ParameterId { get; private set; }

    public string Key { get; private set; } = string.Empty;

    public string Value { get; private set; } = string.Empty;

    public static ConfigurationParameter Create(Guid parameterId, string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return new ConfigurationParameter(
            parameterId, key.Trim().ToLowerInvariant(), (value ?? string.Empty).Trim());
    }

    public void SetValue(string value) => Value = (value ?? string.Empty).Trim();

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ParameterId;
        yield return Key;
        yield return Value;
    }
}
