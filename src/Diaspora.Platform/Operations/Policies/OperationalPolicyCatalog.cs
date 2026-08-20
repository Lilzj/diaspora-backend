using System.Collections.Frozen;
using System.Globalization;

namespace Diaspora.Platform.Operations.Policies;

/// <summary>
/// Composes every contributor's policies into one immutable set.
/// <para>
/// Built once at composition and frozen. Read on every retried call, every cache lookup and every
/// limiter decision, so the read path is a frozen-dictionary probe and nothing else.
/// </para>
/// <para>
/// <b>The conflict rule.</b> Two declarations of one <see cref="OperationalPolicyKey"/> at the same
/// version throw; at different versions the higher wins. That asymmetry is deliberate. The
/// override case is real and needed — a deployment ships <c>v2</c> of <c>outbox.dispatch</c> and
/// the platform default <c>v1</c> steps aside. The same-version case is always a mistake, and the
/// platform's other catalogues have taught the same lesson three times: a duplicate that resolves
/// silently is a duplicate two components disagree about for months.
/// </para>
/// <para>
/// Validation runs here too, once, over the composed set. A policy that fails its own
/// <see cref="OperationalPolicy.Validate"/> stops composition rather than misbehaving at 3am.
/// </para>
/// </summary>
public sealed class OperationalPolicyCatalog : IOperationalPolicyCatalog
{
    private readonly FrozenDictionary<OperationalPolicyKey, OperationalPolicy> _byKey;

    public OperationalPolicyCatalog(IEnumerable<IOperationalPolicyContributor> contributors)
    {
        ArgumentNullException.ThrowIfNull(contributors);

        var accepted = new Dictionary<OperationalPolicyKey, (OperationalPolicy Policy, string Module)>();
        var problems = new List<string>();

        foreach (var contributor in contributors)
        {
            foreach (var policy in contributor.Policies ?? [])
            {
                if (policy is null)
                {
                    problems.Add($"'{contributor.Module}' declared a null policy.");
                    continue;
                }

                foreach (var problem in policy.Validate())
                {
                    problems.Add($"{policy} (declared by '{contributor.Module}') is invalid: {problem}");
                }

                if (!accepted.TryGetValue(policy.Key, out var existing))
                {
                    accepted[policy.Key] = (policy, contributor.Module);
                    continue;
                }

                if (existing.Policy.Version == policy.Version)
                {
                    var version = policy.Version.ToString(CultureInfo.InvariantCulture);

                    problems.Add(
                        $"'{policy.Key}' is declared at version {version} by both " +
                        $"'{existing.Module}' and '{contributor.Module}'. Two declarations of one " +
                        "policy at one version is a conflict, not an override — raise the version " +
                        "of the one that should win.");

                    continue;
                }

                if (policy.Version > existing.Policy.Version)
                {
                    accepted[policy.Key] = (policy, contributor.Module);
                }
            }
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                "The operational policy catalogue is inconsistent:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, problems.Select(problem => "  - " + problem)));
        }

        _byKey = accepted.ToFrozenDictionary(entry => entry.Key, entry => entry.Value.Policy);

        All = [.. _byKey.Values
            .OrderBy(policy => policy.Kind, StringComparer.Ordinal)
            .ThenBy(policy => policy.Name, StringComparer.Ordinal)];
    }

    public IReadOnlyList<OperationalPolicy> All { get; }

    public TPolicy Get<TPolicy>(string name, TPolicy fallback) where TPolicy : OperationalPolicy
    {
        ArgumentNullException.ThrowIfNull(fallback);

        return Find<TPolicy>(name) ?? fallback;
    }

    public TPolicy? Find<TPolicy>(string name) where TPolicy : OperationalPolicy
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        // The kind is a property of the instance, not of the type, so the lookup cannot be keyed
        // on typeof(TPolicy) alone. Probing every kind would be wrong — two families may share a
        // name deliberately. Matching the returned instance's type is exact and costs one cast.
        foreach (var candidate in _byKey)
        {
            if (string.Equals(candidate.Key.Name, name, StringComparison.Ordinal) &&
                candidate.Value is TPolicy typed)
            {
                return typed;
            }
        }

        return null;
    }
}
