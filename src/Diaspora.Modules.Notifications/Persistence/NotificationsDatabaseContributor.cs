using Diaspora.Modules.Notifications.Persistence.Configurations;
using Diaspora.Persistence.Modules;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Notifications.Persistence;

/// <summary>
/// Adds the Communication Platform's tables to the shared context.
/// <para>
/// Twenty tables in the module's own <c>notifications</c> schema, which the context verifies at
/// startup. Nine configurations, not twenty, because eleven of those tables are owned by an
/// aggregate and are configured from inside it — the aggregate boundary made visible in the
/// persistence layer.
/// </para>
/// <para>
/// <b>Four aggregates are <c>ISoftDeletable</c></b> — the notification, the template, the digest and
/// the device registration — so the shared context applies the global query filter and
/// <c>SoftDeleteConverter</c> repairs the owned graph on delete. Nothing here declares either:
/// B1.0.1 exists so a module never has to.
/// </para>
/// <para>
/// The five that are not are deliberate. The <b>inbox counters</b> and the <b>analytics rollup</b>
/// are measurements that outlive what they measure — an inbox that vanished with its owner would
/// take the badge with it, and a category's open rate is computed from messages sent years ago. The
/// <b>preference record</b> is a member's stated wishes and is meaningless once they are gone, so it
/// is hard-deleted with the account rather than lingering as a soft-deleted opinion. The two
/// <b>catalogues</b> are reference data retired with a flag, because notifications and preferences
/// store their keys.
/// </para>
/// </summary>
public sealed class NotificationsDatabaseContributor : IDatabaseContributor
{
    public string Module => NotificationsModuleMetadata.Name;

    public string Schema => NotificationsModuleMetadata.Schema;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // The message itself — owns its deliveries, its attempt log and its actions.
        modelBuilder.ApplyConfiguration(new NotificationConfiguration());

        // The counters that make the badge a single indexed read.
        modelBuilder.ApplyConfiguration(new UserInboxConfiguration());

        // What the member asked for — owns its channel and category rows.
        modelBuilder.ApplyConfiguration(new NotificationPreferenceConfiguration());

        // The authored wording — owns its localizations, variants, variables and actions.
        modelBuilder.ApplyConfiguration(new NotificationTemplateConfiguration());

        // The assembled summary — owns its entries.
        modelBuilder.ApplyConfiguration(new NotificationDigestConfiguration());

        // The two catalogues, seeded by contributors at startup.
        modelBuilder.ApplyConfiguration(new NotificationCategoryConfiguration());
        modelBuilder.ApplyConfiguration(new NotificationRuleConfiguration());

        // Where a member can be reached.
        modelBuilder.ApplyConfiguration(new DeviceRegistrationConfiguration());

        // The measured record.
        modelBuilder.ApplyConfiguration(new NotificationAnalyticsConfiguration());
    }
}
