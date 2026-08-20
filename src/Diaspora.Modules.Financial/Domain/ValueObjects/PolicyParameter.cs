using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.ValueObjects;

/// <summary>
/// One named value on a policy version.
/// <para>
/// A string key and a string value, because a policy the deployment invented has parameters this
/// module has never heard of. The rule implementation that reads a parameter is the thing that knows
/// its type; storing a typed union here would mean this module knowing every rule that will ever
/// exist.
/// </para>
/// <para>
/// <see cref="ParameterId"/> is caller-supplied and the EF configuration declares
/// <c>ValueGeneratedNever()</c>. Without it EF emits UPDATE where INSERT was meant and the failure
/// surfaces as a bogus concurrency conflict on the second row — the platform's most repeated
/// persistence defect, now also a build failure via DSP3002.
/// </para>
/// </summary>
public sealed class PolicyParameter : ValueObject
{
    private PolicyParameter(Guid parameterId, string key, string value)
    {
        ParameterId = parameterId;
        Key = key;
        Value = value;
    }

    private PolicyParameter() { }

    public Guid ParameterId { get; private set; }

    public string Key { get; private set; } = string.Empty;

    public string Value { get; private set; } = string.Empty;

    public static PolicyParameter Create(Guid parameterId, string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return new PolicyParameter(
            parameterId, key.Trim().ToLowerInvariant(), value?.Trim() ?? string.Empty);
    }

    /// <summary>Reads the parameter as a decimal, or null when it is not one.</summary>
    public decimal? AsDecimal() =>
        decimal.TryParse(
            Value, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    /// <summary>Reads the parameter as a whole number, or null when it is not one.</summary>
    public long? AsInt64() =>
        long.TryParse(
            Value, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ParameterId;
        yield return Key;
        yield return Value;
    }
}
