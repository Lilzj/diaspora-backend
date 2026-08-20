using Diaspora.Modules.Media.Domain.Enums;

namespace Diaspora.Modules.Media.Extensibility.Access;

/// <summary>
/// Resolves the policy that speaks for an owner type.
/// <para>
/// Two modules claiming the same owner type fails at construction rather than resolving by
/// assembly load order. An access decision that depended on link order would be an access
/// decision nobody could reproduce — and the failure mode is silent, which is the worst kind
/// for an authorisation path.
/// </para>
/// <para>
/// Scoped rather than singleton, because a vertical's policy will need its own repository. A
/// singleton registry holding scoped policies is a captive dependency the host's
/// service-provider validation refuses to start on — the same lifetime reasoning Location
/// documents for its nearby providers.
/// </para>
/// </summary>
public sealed class MediaAccessPolicyRegistry
{
    private readonly Dictionary<MediaOwnerType, IMediaAccessPolicy> _policies;

    public MediaAccessPolicyRegistry(IEnumerable<IMediaAccessPolicy> policies)
    {
        ArgumentNullException.ThrowIfNull(policies);

        _policies = [];

        foreach (var policy in policies)
        {
            if (!_policies.TryAdd(policy.OwnerType, policy))
            {
                throw new InvalidOperationException(
                    $"Two media access policies claim owner type '{policy.OwnerType}'. " +
                    "An access decision that depended on assembly load order would not be " +
                    "reproducible.");
            }
        }
    }

    public IReadOnlyCollection<MediaOwnerType> ClaimedOwnerTypes =>
        _policies.Keys.ToList().AsReadOnly();

    /// <summary>
    /// Asks the owning vertical. Abstains when no module claims the type — which is the normal
    /// case for a vertical that has not shipped yet, and must not be a denial: an asset owned
    /// by a type nobody claims still belongs to its uploader, and they must still see it.
    /// </summary>
    public async Task<MediaAccessDecision> EvaluateAsync(
        MediaAccessRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _policies.TryGetValue(request.OwnerType, out var policy)
            ? await policy.EvaluateAsync(request, cancellationToken)
            : MediaAccessDecision.Abstain();
    }
}
