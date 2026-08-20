using Diaspora.Modules.Automation.Persistence.Configurations;
using Diaspora.Persistence.Modules;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Automation.Persistence;

/// <summary>
/// Adds the Automation Platform's tables to the shared context.
/// <para>
/// Twelve tables in the module's own <c>automation</c> schema, which the context verifies at startup.
/// <b>No owned type and no owned collection anywhere in the module</b>: every child is an entity with a
/// declared key, every list is a delimited column and every parameter bag is a serialized string.
/// That is a deliberate narrowing — this platform has four recorded defects in owned-type mapping and
/// none of them is reachable from a schema with no owned types in it.
/// </para>
/// <para>
/// <b>Nothing here is <c>ISoftDeletable</c>.</b> A deleted automation is gone, because the contract
/// requires deleting twice to answer the same thing and a soft-deleted row disappears behind the
/// global query filter — which would make the second delete a 404 where the contract requires a 200,
/// and this platform has paid for that exact interaction once already. Runs are kept when their
/// definition goes and carry a denormalised name for that reason: an execution history that vanished
/// with the rule would remove the evidence of what a member was told.
/// </para>
/// </summary>
public sealed class AutomationDatabaseContributor : IDatabaseContributor
{
    public string Module => AutomationModuleMetadata.Name;

    public string Schema => AutomationModuleMetadata.Schema;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // The registry, and the three child collections that make one rule.
        modelBuilder.ApplyConfiguration(new AutomationConfiguration());
        modelBuilder.ApplyConfiguration(new AutomationConditionRuleConfiguration());
        modelBuilder.ApplyConfiguration(new AutomationActionStepConfiguration());
        modelBuilder.ApplyConfiguration(new AutomationAuditRecordConfiguration());

        // Execution history, with per-condition and per-action outcomes.
        modelBuilder.ApplyConfiguration(new AutomationRunConfiguration());
        modelBuilder.ApplyConfiguration(new AutomationRunConditionOutcomeConfiguration());
        modelBuilder.ApplyConfiguration(new AutomationRunActionOutcomeConfiguration());

        // The durable due-store, and the reminders that share its sweep.
        modelBuilder.ApplyConfiguration(new AutomationOccurrenceConfiguration());
        modelBuilder.ApplyConfiguration(new AutomationReminderConfiguration());

        // Wave 12: one row per dated subject a scheduled projection fires from, and the same sweep.
        modelBuilder.ApplyConfiguration(new AutomationSubjectHorizonConfiguration());

        // Saved searches, and the once-ever match barrier.
        modelBuilder.ApplyConfiguration(new SavedSearchConfiguration());
        modelBuilder.ApplyConfiguration(new SavedSearchMatchConfiguration());
    }
}
