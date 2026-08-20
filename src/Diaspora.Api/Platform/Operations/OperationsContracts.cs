using Diaspora.Platform.Operations.Configuration;
using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.Resilience;

namespace Diaspora.Api.Platform.Operations;

/// <summary>
/// A policy, flattened for the wire.
/// <para>
/// <see cref="OperationalPolicy"/> is an abstract record with a different shape per family, and
/// serializing the hierarchy directly would emit a different set of fields per entry with no
/// discriminator. Flattening to kind, name, version and description keeps the listing readable and
/// stable; a caller wanting a specific policy's numbers reads the guide, where they are documented
/// with their reasons.
/// </para>
/// </summary>
/// <param name="Kind">The policy family.</param>
/// <param name="Name">Its name.</param>
/// <param name="Version">Its version. A deployment override shows here as a number above one.</param>
/// <param name="Description">Why it exists.</param>
public sealed record OperationalPolicyResponse(string Kind, string Name, int Version, string Description)
{
    public static OperationalPolicyResponse From(OperationalPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return new OperationalPolicyResponse(policy.Kind, policy.Name, policy.Version, policy.Description);
    }
}

/// <summary>Circuit breakers and bulkheads together, because an operator reads them together.</summary>
public sealed record ResilienceStateResponse(
    IReadOnlyList<CircuitBreakerState> Circuits,
    IReadOnlyList<BulkheadState> Bulkheads);

/// <summary>
/// A runtime setting, with its value redacted when the declaration says it is sensitive.
/// <para>
/// A projection rather than the entry itself, because <see cref="ConfigurationEntry"/> carries the real
/// value beside the display value — correct for an in-process reader, and exactly the wrong thing to
/// hand to a serializer.
/// </para>
/// </summary>
public sealed record ConfigurationEntryResponse(
    string Key,
    string Module,
    string Description,
    string Kind,
    string Value,
    string DefaultValue,
    bool IsOverridden,
    bool RequiresRestart,
    bool Sensitive,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy)
{
    public static ConfigurationEntryResponse From(ConfigurationEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new ConfigurationEntryResponse(
            entry.Definition.Key,
            entry.Definition.Module,
            entry.Definition.Description,
            entry.Definition.Kind.ToString(),
            entry.DisplayValue,
            entry.Definition.Sensitive ? "********" : entry.Definition.DefaultValue,
            entry.IsOverridden,
            entry.Definition.RequiresRestart,
            entry.Definition.Sensitive,
            entry.UpdatedAt,
            entry.UpdatedBy);
    }
}

/// <summary>What to invalidate. Exactly one of the three.</summary>
/// <param name="Tag">A cache tag.</param>
/// <param name="CacheNamespace">A cache namespace.</param>
/// <param name="Everything">
/// The whole cache. Behind a flag on the same request rather than its own endpoint, so it cannot be
/// reached by a mistyped URL.
/// </param>
public sealed record CacheInvalidationRequest(
    string? Tag = null,
    string? CacheNamespace = null,
    bool Everything = false);

/// <summary>Turning a flag on or off.</summary>
public sealed record FeatureFlagStateRequest(bool Enabled, string? Reason = null);

/// <summary>Engaging or releasing the kill switch. A reason is required to engage.</summary>
public sealed record KillSwitchRequest(bool Engage, string? Reason = null);

/// <summary>Setting a runtime value.</summary>
public sealed record ConfigurationValueRequest(string Value, string? Reason = null);
