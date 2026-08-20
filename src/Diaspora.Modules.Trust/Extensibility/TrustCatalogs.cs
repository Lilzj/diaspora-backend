using Diaspora.Modules.Trust.Domain;

namespace Diaspora.Modules.Trust.Extensibility;

/// <summary>
/// The composed set of verification type handlers.
/// <para>
/// Built once at startup from every discovered <see cref="IVerificationTypeHandler"/>. Two
/// handlers claiming the same type fail the boot — the same discipline the platform kernel
/// applies to duplicate permission keys, and for the same reason: a silent last-one-wins
/// means the behaviour of a verification depends on assembly load order.
/// </para>
/// </summary>
public sealed class VerificationTypeHandlerRegistry
{
    private readonly IReadOnlyDictionary<VerificationType, IVerificationTypeHandler> _handlers;

    public VerificationTypeHandlerRegistry(IEnumerable<IVerificationTypeHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        var map = new Dictionary<VerificationType, IVerificationTypeHandler>();

        foreach (var handler in handlers)
        {
            if (map.TryGetValue(handler.Type, out var existing))
            {
                throw new InvalidOperationException(
                    $"Two verification handlers claim '{handler.Type}': " +
                    $"'{existing.GetType().FullName}' and '{handler.GetType().FullName}'. " +
                    "Each verification type must have exactly one handler.");
            }

            map[handler.Type] = handler;
        }

        _handlers = map;
    }

    /// <summary>The handler for a type, or null when the platform cannot verify it.</summary>
    public IVerificationTypeHandler? For(VerificationType type) =>
        _handlers.TryGetValue(type, out var handler) ? handler : null;

    /// <summary>Whether the platform can verify this type at all.</summary>
    public bool Supports(VerificationType type) => _handlers.ContainsKey(type);

    /// <summary>Every type the platform can verify.</summary>
    public IReadOnlyCollection<VerificationType> SupportedTypes => _handlers.Keys.ToList();
}

/// <summary>
/// The composed reputation rule set.
/// <para>
/// Trust's own defaults plus every contributed rule. Two rules pricing the same action fail
/// the boot: letting one module silently re-price another module's action by load order is
/// the kind of bug that is noticed a quarter later, in an argument about someone's score.
/// </para>
/// </summary>
public sealed class ReputationRuleCatalog
{
    private readonly IReadOnlyDictionary<string, ReputationRule> _rules;

    public ReputationRuleCatalog(IEnumerable<IReputationRuleContributor> contributors)
    {
        ArgumentNullException.ThrowIfNull(contributors);

        var map = new Dictionary<string, ReputationRule>(StringComparer.OrdinalIgnoreCase);
        var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in DefaultReputationRules.All)
        {
            map[rule.ActionType] = rule;
            owners[rule.ActionType] = TrustModuleMetadata.Name;
        }

        foreach (var contributor in contributors)
        {
            foreach (var rule in contributor.GetRules())
            {
                if (owners.TryGetValue(rule.ActionType, out var owner))
                {
                    throw new InvalidOperationException(
                        $"Module '{contributor.Module}' declares reputation action " +
                        $"'{rule.ActionType}', which module '{owner}' already owns. " +
                        "Reputation actions must be uniquely owned.");
                }

                map[rule.ActionType] = rule;
                owners[rule.ActionType] = contributor.Module;
            }
        }

        _rules = map;
    }

    public ReputationRule? For(string actionType) =>
        actionType is not null && _rules.TryGetValue(actionType, out var rule) && rule.IsActive
            ? rule
            : null;

    public IReadOnlyCollection<ReputationRule> All => _rules.Values.ToList();
}

/// <summary>
/// The composed capability requirements.
/// <para>
/// Trust's defaults — the four the frontend already ships plus the guide programme — merged
/// with whatever the modules that own a capability declare. Duplicates fail the boot, so a
/// capability's cost has exactly one definition.
/// </para>
/// </summary>
public sealed class TrustRequirementCatalog
{
    private readonly IReadOnlyDictionary<string, TrustRequirement> _requirements;

    public TrustRequirementCatalog(IEnumerable<ITrustRequirementContributor> contributors)
    {
        ArgumentNullException.ThrowIfNull(contributors);

        var map = new Dictionary<string, TrustRequirement>(StringComparer.OrdinalIgnoreCase);
        var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var requirement in DefaultTrustRequirements.All)
        {
            map[requirement.CapabilityKey] = requirement;
            owners[requirement.CapabilityKey] = TrustModuleMetadata.Name;
        }

        foreach (var contributor in contributors)
        {
            foreach (var requirement in contributor.GetRequirements())
            {
                if (owners.TryGetValue(requirement.CapabilityKey, out var owner))
                {
                    throw new InvalidOperationException(
                        $"Module '{contributor.Module}' declares capability " +
                        $"'{requirement.CapabilityKey}', which module '{owner}' already owns. " +
                        "Capabilities must be uniquely owned.");
                }

                map[requirement.CapabilityKey] = requirement;
                owners[requirement.CapabilityKey] = contributor.Module;
            }
        }

        _requirements = map;
    }

    public TrustRequirement? For(string capabilityKey) =>
        capabilityKey is not null && _requirements.TryGetValue(capabilityKey, out var requirement)
            ? requirement
            : null;

    public IReadOnlyCollection<TrustRequirement> All => _requirements.Values.ToList();
}

/// <summary>
/// The composed badge rule set.
/// <para>
/// Unlike the catalogues above, a duplicate here is a genuine conflict rather than a naming
/// clash — two rules deciding the same badge would make the award depend on evaluation
/// order — so it fails the boot too.
/// </para>
/// </summary>
public sealed class BadgeRuleCatalog
{
    private readonly IReadOnlyList<IBadgeRule> _rules;

    public BadgeRuleCatalog(IEnumerable<IBadgeRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        var ordered = rules.ToList();
        var seen = new Dictionary<BadgeType, string>();

        foreach (var rule in ordered)
        {
            if (seen.TryGetValue(rule.Badge, out var existing))
            {
                throw new InvalidOperationException(
                    $"Two badge rules decide '{rule.Badge}': '{existing}' and " +
                    $"'{rule.GetType().FullName}'. Each badge must have exactly one rule.");
            }

            seen[rule.Badge] = rule.GetType().FullName!;
        }

        _rules = ordered;
    }

    /// <summary>Rules that apply to the member as a whole.</summary>
    public IReadOnlyList<IBadgeRule> Global => _rules.Where(rule => !rule.IsCountryScoped).ToList();

    /// <summary>Rules evaluated once per country the member has standing in.</summary>
    public IReadOnlyList<IBadgeRule> CountryScoped => _rules.Where(rule => rule.IsCountryScoped).ToList();

    public IReadOnlyList<IBadgeRule> All => _rules;
}

/// <summary>
/// The ordered chain of extra trust rules.
/// <para>
/// Evaluated after the declarative thresholds pass. First refusal wins and short-circuits the
/// rest, so a member sees the most important reason rather than whichever rule happened to
/// run last.
/// </para>
/// </summary>
public sealed class TrustRuleChain
{
    private readonly IReadOnlyList<ITrustRule> _rules;

    public TrustRuleChain(IEnumerable<ITrustRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        _rules = rules.OrderBy(rule => rule.Order).ThenBy(rule => rule.Module).ToList();
    }

    /// <summary>
    /// Runs every rule that applies to the capability. Returns the first refusal, or
    /// <see cref="TrustRuleVerdict.Allow"/> when they all pass.
    /// </summary>
    public TrustRuleVerdict Evaluate(TrustRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var rule in _rules)
        {
            if (rule.AppliesTo.Count > 0
                && !rule.AppliesTo.Contains(context.CapabilityKey, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var verdict = rule.Evaluate(context);
            if (!verdict.Allowed)
            {
                return verdict;
            }
        }

        return TrustRuleVerdict.Allow;
    }
}
