using System.Collections.Frozen;
using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Platform.Authorization;

/// <summary>
/// Composes every module's feature-gate declarations into one catalogue.
/// <para>
/// Evaluation returns a reason code for every closed gate rather than a bare boolean.
/// That is the whole point of having gates as well as permissions: a client that knows
/// <em>why</em> something is locked can show the member the way forward, and it can do so
/// without hardcoding the platform's progression rules.
/// </para>
/// </summary>
public sealed class FeatureCatalog : IFeatureCatalog
{
    private readonly FrozenDictionary<string, FeatureDefinition> _byName;

    public FeatureCatalog(IEnumerable<IFeatureContributor> contributors)
    {
        ArgumentNullException.ThrowIfNull(contributors);

        var declarations = contributors
            .SelectMany(contributor => contributor
                .GetFeatures()
                .Select(definition => (Contributor: contributor.Module, Definition: definition)))
            .ToList();

        var duplicates = declarations
            .GroupBy(d => d.Definition.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group =>
                $"'{group.Key}' declared by {string.Join(", ", group.Select(d => d.Contributor).Distinct())}")
            .ToList();

        if (duplicates.Count > 0)
        {
            throw new InvalidOperationException(
                $"Two modules declared the same feature gate: {string.Join("; ", duplicates)}.");
        }

        All = declarations
            .Select(d => d.Definition)
            .OrderBy(d => d.Module, StringComparer.Ordinal)
            .ThenBy(d => d.Name, StringComparer.Ordinal)
            .ToArray();

        _byName = All.ToFrozenDictionary(d => d.Name, StringComparer.Ordinal);
    }

    public IReadOnlyList<FeatureDefinition> All { get; }

    public FeatureDefinition? Find(string featureName) =>
        string.IsNullOrWhiteSpace(featureName)
            ? null
            : _byName.GetValueOrDefault(featureName);

    public FeatureEvaluation Evaluate(AuthorizationSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        var granted = new List<string>(All.Count);
        var blocked = new List<BlockedFeature>();

        foreach (var definition in All)
        {
            var block = Evaluate(definition, subject);

            if (block is null)
            {
                granted.Add(definition.Name);
            }
            else
            {
                blocked.Add(block);
            }
        }

        return new FeatureEvaluation(granted, blocked);
    }

    public bool IsOpen(string featureName, AuthorizationSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        var definition = Find(featureName);
        return definition is not null && Evaluate(definition, subject) is null;
    }

    /// <summary>
    /// Returns <c>null</c> when the gate is open, or the reason it is not. Ordered so the
    /// most fundamental refusal wins: telling a suspended member to "complete verification"
    /// would be both useless and slightly insulting.
    /// </summary>
    private static BlockedFeature? Evaluate(FeatureDefinition definition, AuthorizationSubject subject)
    {
        if (subject.IsRestricted && !definition.OpenWhenRestricted)
        {
            return new BlockedFeature(definition.Name, FeatureBlockCode.AccountRestricted, null);
        }

        // Tier before role: a member who has not finished onboarding should be told to
        // finish onboarding, not that the gate is staff-only. The first is actionable.
        if (subject.Tier < definition.MinimumTier)
        {
            var code = definition.MinimumTier <= AccessTier.Limited
                ? FeatureBlockCode.RequiresOnboarding
                : FeatureBlockCode.RequiresHigherTier;

            return new BlockedFeature(definition.Name, code, definition.MinimumTier);
        }

        if (definition.StaffOnly && !subject.IsStaff)
        {
            return new BlockedFeature(definition.Name, FeatureBlockCode.StaffOnly, null);
        }

        if (definition.MaximumTier is { } ceiling && subject.Tier > ceiling)
        {
            return new BlockedFeature(definition.Name, FeatureBlockCode.NoLongerApplicable, null);
        }

        return null;
    }
}
