using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Automation.Authorization;

/// <summary>
/// What a caller must hold, and at which tier.
/// <para>
/// <b>Reading is <see cref="AccessTier.Limited"/>; everything that configures a rule is
/// <see cref="AccessTier.Verified"/>.</b> B2.0.1's rule is that a tier gates privilege over other
/// members rather than progression, and configuring an automation is not privilege over anybody —
/// it is a member arranging their own notifications. But an automation is a standing instruction the
/// platform executes unattended, and a half-registered account is not somebody this deployment
/// should be running standing instructions for.
/// </para>
/// <para>
/// <b>Reaching other people is not a tier, it is a capability.</b> No permission here grants
/// broadcast. <c>automations.community</c> and its siblings come from
/// <c>GET /automations/capabilities</c>, are re-derived from the owning scope's own authority, and
/// are re-checked at the moment before dispatch — because a moderator who created a community
/// automation and then lost the role must stop being able to broadcast through it, and the only
/// place that can be enforced is the dispatch itself.
/// </para>
/// </summary>
public sealed class AutomationPermissionContributor : IPermissionContributor
{
    public string Module => AutomationModuleMetadata.Name;

    public IEnumerable<PermissionDefinition> GetPermissions()
    {
        yield return new PermissionDefinition(
            AutomationPermissions.Read,
            Module,
            "Read one's own automations, their execution history, their audit trail and the "
            + "automation capability set.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new PermissionDefinition(
            AutomationPermissions.Write,
            Module,
            "Create, change, activate, pause, disable or delete an automation one owns or manages.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            AutomationPermissions.Execute,
            Module,
            "Run a manual automation one owns or manages.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            AutomationPermissions.Simulate,
            Module,
            "Dry-run an automation against real state, performing nothing.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            AutomationPermissions.RemindersWrite,
            Module,
            "Schedule or cancel one's own reminder on a subject one can see.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new PermissionDefinition(
            AutomationPermissions.SavedSearchesWrite,
            Module,
            "Save, change or delete one's own saved search and its alert settings.")
        {
            MinimumTier = AccessTier.Limited
        };
    }
}

/// <summary>
/// The module's feature gates.
/// <para>
/// <b>Five rather than one, and the split is the frontend's.</b> <c>automation-api.ts</c> holds six
/// separate booleans — registry, engine, scheduler, history, dispatch, saved-search evaluation —
/// specifically so the client can say "your automations are saved and will start running when the
/// engine ships" rather than choosing between "everything works" and "nothing does". A single gate
/// would force that choice back on it.
/// </para>
/// </summary>
public sealed class AutomationFeatureContributor : IFeatureContributor
{
    public string Module => AutomationModuleMetadata.Name;

    public IEnumerable<FeatureDefinition> GetFeatures()
    {
        yield return new FeatureDefinition(
            AutomationFeatures.Registry,
            Module,
            "Storing an automation: the Automation Center, the builder, templates and the audit trail.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            AutomationFeatures.Engine,
            Module,
            "Running an automation: event consumption, condition evaluation, action dispatch and "
            + "execution history.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            AutomationFeatures.Scheduler,
            Module,
            "Time-based execution: the durable occurrence store and its sweep.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            AutomationFeatures.SavedSearches,
            Module,
            "Saved searches and their evaluation against newly published content.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            AutomationFeatures.Reminders,
            Module,
            "Reminders on an event, an occurrence or a series.")
        {
            MinimumTier = AccessTier.Limited
        };
    }
}

public sealed class AutomationPolicyContributor : IPolicyContributor
{
    public string Module => AutomationModuleMetadata.Name;

    public IEnumerable<PolicyDefinition> GetPolicies()
    {
        yield return PolicyDefinition.ForPermission(
            AutomationPolicies.Read, Module, AutomationPermissions.Read);

        yield return PolicyDefinition.ForPermission(
            AutomationPolicies.Write, Module, AutomationPermissions.Write);

        yield return PolicyDefinition.ForPermission(
            AutomationPolicies.Execute, Module, AutomationPermissions.Execute);

        yield return PolicyDefinition.ForPermission(
            AutomationPolicies.Simulate, Module, AutomationPermissions.Simulate);

        yield return PolicyDefinition.ForPermission(
            AutomationPolicies.RemindersWrite, Module, AutomationPermissions.RemindersWrite);

        yield return PolicyDefinition.ForPermission(
            AutomationPolicies.SavedSearchesWrite, Module, AutomationPermissions.SavedSearchesWrite);
    }
}
