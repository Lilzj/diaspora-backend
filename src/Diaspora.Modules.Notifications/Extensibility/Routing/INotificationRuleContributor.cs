namespace Diaspora.Modules.Notifications.Extensibility.Routing;

/// <summary>
/// Ships routing rules with a module.
/// <para>
/// <b>This seam is what the architecture test "notification routing is policy-driven rather than
/// hard-coded" actually checks.</b> An event handler in this module extracts identifiers and values
/// from a published contract; it never names a template, a category, a priority or a channel. Those
/// come from rules, and rules come from here or from an operator editing a row.
/// </para>
/// <para>
/// Constructed before the container exists: public parameterless constructor, no dependencies.
/// </para>
/// </summary>
public interface INotificationRuleContributor
{
    string Module { get; }

    IEnumerable<NotificationRuleSeed> GetRules();
}
