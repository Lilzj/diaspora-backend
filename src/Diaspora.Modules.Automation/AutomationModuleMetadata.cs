namespace Diaspora.Modules.Automation;

/// <summary>Names shared by the module's platform module and its contributors.</summary>
public static class AutomationModuleMetadata
{
    public const string Name = "AutomationDefinition";

    public const string Version = "1.0.0";

    /// <summary>The module's own database schema. It owns these tables and no others.</summary>
    public const string Schema = "automation";

    /// <summary>
    /// The module's public URL space.
    /// <para>
    /// The frontend's <c>automation-gap-api.ts</c> declares every route in this module against
    /// <c>/automation-gap/v0/automations…</c> with the note that "when a backend phase ships
    /// automation, the hook swaps which arm it calls and the request shapes already match". They
    /// match. Only the prefix changes.
    /// </para>
    /// </summary>
    public const string RoutePrefix = "/api/v1/automations";

    /// <summary>
    /// The dependency name every guarded execution is run under.
    /// <para>
    /// Suffixed per action family — <c>automation.action.notify</c> — so a breaker opened by one kind
    /// of action does not refuse another. B17's executor resolves the retry policy, the breaker and
    /// the bulkhead from this string and labels every metric with it.
    /// </para>
    /// </summary>
    public const string ResilienceDependencyPrefix = "automation";

    /// <summary>
    /// The lease taken before the occurrence sweeper touches anything.
    /// <para>
    /// One name, stable across instances and restarts, because the sweep's whole purpose is that
    /// exactly one instance claims a due occurrence.
    /// </para>
    /// </summary>
    public const string SchedulerLeaseResource = "automation.scheduler.sweep";

    /// <summary>The lease taken around one automation's execution. Suffixed with its identifier.</summary>
    public const string ExecutionLeasePrefix = "automation.execution";
}
