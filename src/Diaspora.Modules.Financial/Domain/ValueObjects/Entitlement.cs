using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.ValueObjects;

/// <summary>
/// One capability a plan grants.
/// <para>
/// <b>The reason a new plan needs no client release.</b> The client renders entitlements, never plan
/// names — a screen that switched on <c>planCode == "professional"</c> would have to ship again the
/// day somebody adds a plan between two existing ones. An entitlement is a key the client can look
/// up and an optional limit; a plan is a bag of them.
/// </para>
/// <para><see cref="EntitlementId"/> is caller-supplied. <c>ValueGeneratedNever()</c>.</para>
/// </summary>
public sealed class Entitlement : ValueObject
{
    private Entitlement(Guid entitlementId, string key, string? value)
    {
        EntitlementId = entitlementId;
        Key = key;
        Value = value;
    }

    private Entitlement() { }

    public Guid EntitlementId { get; private set; }

    /// <summary>Lower case, dotted. <c>listings.concurrent</c>, <c>analytics.advanced</c>.</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>A limit or a setting. Null means the capability is simply granted.</summary>
    public string? Value { get; private set; }

    public static Entitlement Create(Guid entitlementId, string key, string? value = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return new Entitlement(
            entitlementId,
            key.Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(value) ? null : value.Trim());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return EntitlementId;
        yield return Key;
        yield return Value;
    }
}
