using Diaspora.Contracts.Automation.V1.Events;
using Diaspora.Modules.Automation.Domain.Automations;

namespace Diaspora.Modules.Automation.Application.Registry;

/// <summary>
/// Builds the lifecycle fact, in one place.
/// <para>
/// <b>B24.</b> Three handlers publish it and a fourth could later. Three hand-built payloads would be
/// three chances to attach an automation name, and the whole privacy position of this contract is
/// that it carries keys and counts and nothing a member wrote. One builder makes that a property of
/// the code rather than of everybody who touches it — which is the same argument the client's own
/// instrumentation makes for its helpers, and it has held there for six phases.
/// </para>
/// </summary>
internal static class AutomationLifecycle
{
    /// <summary>
    /// The fact, from the aggregate.
    /// </summary>
    /// <param name="automation">The rule as it stands after the change.</param>
    /// <param name="change">One of <c>AutomationTelemetry.Tags.Change</c>'s values.</param>
    /// <param name="now">When it happened.</param>
    public static AutomationLifecycleChangedIntegrationEvent Changed(
        AutomationDefinition automation, string change, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(automation);

        return new AutomationLifecycleChangedIntegrationEvent
        {
            AutomationId = automation.Id.Value,
            OwnerId = automation.OwnerId,
            OwnerKind = automation.OwnerKind.ToString(),
            Change = change,
            TriggerKey = automation.TriggerKey,

            // Counts, never contents. A rule with three conditions is a shape; what those three
            // conditions compare is a member's budget, immigration situation or financial position,
            // and none of it belongs on a bus with a different retention policy from the rule.
            ConditionCount = automation.Conditions.Count,
            ActionCount = automation.Actions.Count,
            TemplateId = automation.TemplateId,
            OccurredAt = now
        };
    }
}
