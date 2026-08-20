using Diaspora.Modules.Providers.Domain.Enums;

namespace Diaspora.Modules.Providers.Extensibility.Types;

/// <summary>
/// The provider types this deployment offers, composed from every contributor.
/// <para>
/// Later contributors override earlier ones for the same type, which is the opposite of the
/// duplicate-key failure the other registries impose — and deliberately so. Overriding is the
/// point here: a deployment-specific contributor exists precisely to say "on this platform, a
/// healthcare provider must supply a registration number", and making that a startup failure
/// would leave no way to express it.
/// </para>
/// </summary>
public sealed class ProviderTypeRegistry
{
    private readonly Dictionary<ProviderType, ProviderTypeDescriptor> _descriptors = [];

    public ProviderTypeRegistry(IEnumerable<IProviderTypeContributor> contributors)
    {
        ArgumentNullException.ThrowIfNull(contributors);

        foreach (var contributor in contributors.OrderBy(candidate => candidate.Order))
        {
            foreach (var descriptor in contributor.GetProviderTypes())
            {
                _descriptors[descriptor.Type] = descriptor;
            }
        }
    }

    /// <summary>Every type this deployment offers, for the registration wizard.</summary>
    public IReadOnlyList<ProviderTypeDescriptor> Available =>
        _descriptors.Values.Where(descriptor => descriptor.IsAvailable).ToList();

    /// <summary>Whether a member may register a provider of this type here.</summary>
    public bool IsAvailable(ProviderType type) =>
        _descriptors.TryGetValue(type, out var descriptor) && descriptor.IsAvailable;

    /// <summary>The descriptor for a type, or null when this deployment has none.</summary>
    public ProviderTypeDescriptor? Find(ProviderType type) =>
        _descriptors.GetValueOrDefault(type);

    /// <summary>
    /// Whether a verification kind applies to a type.
    /// <para>
    /// Permissive when the type has no descriptor: a type nobody described should not have its
    /// verifications refused, because that turns a missing configuration into a member being
    /// told they cannot prove who they are.
    /// </para>
    /// </summary>
    public bool SupportsVerification(ProviderType type, ProviderVerificationKind kind) =>
        Find(type) is not { } descriptor
        || descriptor.VerificationKinds.Count == 0
        || descriptor.VerificationKinds.Contains(kind);
}
