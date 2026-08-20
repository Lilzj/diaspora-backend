namespace Diaspora.Platform.Operations.Alerting;

/// <summary>How urgently an alert should be answered.</summary>
public enum AlertSeverity
{
    /// <summary>Look at it during working hours.</summary>
    Info = 0,

    /// <summary>Look at it today.</summary>
    Warning = 1,

    /// <summary>Look at it now.</summary>
    Critical = 2,

    /// <summary>Wake somebody.</summary>
    Page = 3
}

/// <summary>How a rule's condition is expressed.</summary>
public enum AlertComparison
{
    GreaterThan = 0,
    LessThan = 1,
    Equals = 2
}

/// <summary>
/// A condition worth telling somebody about.
/// <para>
/// <b>Declared here and evaluated somewhere else.</b> These are not evaluated in-process: an alerting
/// system that runs inside the thing it monitors goes quiet at exactly the moment it is needed. What
/// the platform owns is the rule — which metric, which threshold, how long for, how serious, and what
/// to do about it — expressed once, in the repository, next to the code that emits the metric.
/// </para>
/// <para>
/// <b>The runbook line is not documentation garnish.</b> An alert with no stated response is an alert
/// that gets acknowledged and ignored, and then gets muted. A rule without one should not ship.
/// </para>
/// </summary>
/// <param name="Name">The rule's name. Dotted, namespaced to what it watches.</param>
/// <param name="Metric">Which metric, from <c>PlatformTelemetryNames</c>.</param>
/// <param name="Comparison">Which way.</param>
/// <param name="Threshold">The number.</param>
/// <param name="For">
/// How long the condition must hold before firing. The field that separates an alert from a hair
/// trigger: nearly every metric crosses nearly every threshold for a few seconds.
/// </param>
/// <param name="Severity">How urgently.</param>
/// <param name="Module">Who owns the response.</param>
/// <param name="Description">What the condition means.</param>
/// <param name="Runbook">What to do about it. Required.</param>
/// <param name="Labels">Attributes to filter the metric by — a module, a queue, a dependency.</param>
public sealed record AlertRule(
    string Name,
    string Metric,
    AlertComparison Comparison,
    double Threshold,
    TimeSpan For,
    AlertSeverity Severity,
    string Module,
    string Description,
    string Runbook,
    IReadOnlyDictionary<string, string>? Labels = null);

/// <summary>Declares a module's alert rules.</summary>
public interface IAlertRuleContributor
{
    string Module { get; }

    IEnumerable<AlertRule> Rules { get; }
}

/// <summary>The composed rule set, for export to whatever evaluates it.</summary>
public interface IAlertRuleCatalog
{
    IReadOnlyList<AlertRule> All { get; }

    IReadOnlyList<AlertRule> ForModule(string module);
}
