namespace Diaspora.Modules.Automation;

/// <summary>
/// The permission keys this module defines.
/// <para>
/// <c>module:resource:action</c>, per DSP5002. Colons for permissions, dots for policies, so a
/// policy name and the permission behind it can never be confused in an attribute or a token claim.
/// </para>
/// </summary>
public static class AutomationPermissions
{
    public const string Read = "automation:automations:read";
    public const string Write = "automation:automations:write";
    public const string Execute = "automation:automations:execute";
    public const string Simulate = "automation:automations:simulate";
    public const string RemindersWrite = "automation:reminders:write";
    public const string SavedSearchesWrite = "automation:saved-searches:write";
}

/// <summary>
/// The policy names endpoints carry.
/// <para>
/// <c>module.resource.action</c>, lower case, per DSP5003 and DSP5006.
/// </para>
/// </summary>
public static class AutomationPolicies
{
    public const string Read = "automation.automations.read";
    public const string Write = "automation.automations.write";
    public const string Execute = "automation.automations.execute";
    public const string Simulate = "automation.automations.simulate";
    public const string RemindersWrite = "automation.reminders.write";
    public const string SavedSearchesWrite = "automation.saved-searches.write";
}

/// <summary>
/// The feature gates this module contributes.
/// <para>
/// <c>module.capability</c>, per DSP5001. Separate gates because they do not ship together: a
/// registry without an engine is a real intermediate state and the client is built to render it.
/// </para>
/// </summary>
public static class AutomationFeatures
{
    public const string Registry = "automation.registry";
    public const string Engine = "automation.engine";
    public const string Scheduler = "automation.scheduler";
    public const string SavedSearches = "automation.saved-searches";
    public const string Reminders = "automation.reminders";
}

/// <summary>
/// The capability keys the server grants, which the client reads and never derives.
/// <para>
/// These are the strings the frontend's action and trigger catalogues name in
/// <c>requiredCapability</c>. They are granted by <c>GET /automations/capabilities</c> and re-checked
/// on every write and again at execution — the capability set is advisory to the client and binding
/// on the server.
/// </para>
/// </summary>
public static class AutomationCapabilities
{
    public const string Community = "automations.community";
    public const string Organization = "automations.organization";
    public const string Creator = "automations.creator";
    public const string Employer = "automations.employer";
    public const string Seller = "automations.seller";
    public const string Landlord = "automations.landlord";
    public const string Provider = "automations.provider";
    public const string Financial = "automations.financial";

    /// <summary>
    /// A member's own area automations — granted to every member over their own scope since
    /// 21 Oct 2026.
    /// <para>
    /// Withheld until then because there was no backend: the app requests only foreground location
    /// and nothing on the server could observe an area. Location now takes an opt-in,
    /// foreground-only presence report and keeps only the last area, so <c>location.enteredArea</c>
    /// can fire; the member's opt-in decides whether it ever does.
    /// </para>
    /// </summary>
    public const string Location = "automations.location";
}
