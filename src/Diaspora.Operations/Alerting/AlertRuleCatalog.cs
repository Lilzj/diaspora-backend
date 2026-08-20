using Diaspora.Platform.Operations.Alerting;

namespace Diaspora.Operations.Alerting;

/// <summary>
/// The composed alert rules.
/// <para>
/// Validated at composition: a duplicate name, a blank runbook or a zero <c>For</c> window is refused.
/// The runbook check in particular is not pedantry — a rule with no stated response is a rule that
/// gets acknowledged, ignored and eventually muted, and the platform is better off without it than
/// with it.
/// </para>
/// </summary>
public sealed class AlertRuleCatalog : IAlertRuleCatalog
{
    public AlertRuleCatalog(IEnumerable<IAlertRuleContributor> contributors)
    {
        ArgumentNullException.ThrowIfNull(contributors);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var problems = new List<string>();
        var rules = new List<AlertRule>();

        foreach (var contributor in contributors)
        {
            foreach (var rule in contributor.Rules ?? [])
            {
                if (!seen.Add(rule.Name))
                {
                    problems.Add($"'{rule.Name}' is declared more than once.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(rule.Runbook))
                {
                    problems.Add($"'{rule.Name}' has no runbook. An alert nobody knows how to answer is noise.");
                }

                if (rule.For <= TimeSpan.Zero)
                {
                    problems.Add(
                        $"'{rule.Name}' fires immediately on threshold. Nearly every metric crosses " +
                        "nearly every threshold for a second; give it a window.");
                }

                if (string.IsNullOrWhiteSpace(rule.Metric))
                {
                    problems.Add($"'{rule.Name}' names no metric.");
                }

                rules.Add(rule);
            }
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                "The alert rule catalogue is inconsistent:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, problems.Select(problem => "  - " + problem)));
        }

        All = [.. rules.OrderBy(rule => rule.Module, StringComparer.Ordinal)
            .ThenBy(rule => rule.Name, StringComparer.Ordinal)];
    }

    public IReadOnlyList<AlertRule> All { get; }

    public IReadOnlyList<AlertRule> ForModule(string module)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(module);

        return [.. All.Where(rule => string.Equals(rule.Module, module, StringComparison.Ordinal))];
    }
}
