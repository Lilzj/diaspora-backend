using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.Modules.Notifications.Domain.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Notifications.Persistence.Configurations;

/// <summary>
/// Maps the template aggregate and its four owned collections.
/// <para>
/// <b>The unique index is on (key, version), not on key alone.</b> Versioning is by supersession: a
/// published template and the draft that will replace it exist at once under one key, which is what
/// makes a bad wording change reversible without a deployment. A unique index on the key would make
/// that impossible and would only be discovered by the first person trying to fix a typo.
/// </para>
/// </summary>
public sealed class NotificationTemplateConfiguration
    : IEntityTypeConfiguration<NotificationTemplate>
{
    public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            NotificationPersistenceConstants.TemplatesTable,
            NotificationsModuleMetadata.Schema);

        builder.HasKey(template => template.Id);

        builder.Property(template => template.Revision).IsConcurrencyToken();

        builder.Property(template => template.Id)
            .HasConversion(NotificationValueConverters.Id<NotificationTemplateId>());

        builder.Property(template => template.Key)
            .HasMaxLength(NotificationPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(template => template.CategoryKey)
            .HasMaxLength(NotificationPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(template => template.DefaultLanguage)
            .HasMaxLength(NotificationPersistenceConstants.LanguageLength)
            .IsRequired();

        builder.Property(template => template.Priority)
            .HasConversion<string>()
            .HasMaxLength(NotificationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(template => template.Status)
            .HasConversion<string>()
            .HasMaxLength(NotificationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(template => template.RouteTemplate)
            .HasMaxLength(NotificationPersistenceConstants.RouteLength);

        builder.Property(template => template.Description)
            .HasMaxLength(NotificationPersistenceConstants.BodyLength);

        ConfigureLocalizations(builder);
        ConfigureVariants(builder);
        ConfigureVariables(builder);
        ConfigureActions(builder);

        builder.HasIndex(template => new { template.Key, template.Version })
            .IsUnique()
            .HasDatabaseName("ix_notification_templates_key_version");

        // The dispatcher's lookup: the published row under a key.
        builder.HasIndex(template => new { template.Key, template.Status })
            .HasDatabaseName("ix_notification_templates_published");

        builder.HasIndex(template => template.CategoryKey)
            .HasDatabaseName("ix_notification_templates_category");

        builder.Ignore(template => template.DomainEvents);
        builder.Ignore(template => template.IsUsable);
    }

    private static void ConfigureLocalizations(EntityTypeBuilder<NotificationTemplate> builder) =>
        builder.OwnsMany(template => template.Localizations, localization =>
        {
            localization.ToTable(
                NotificationPersistenceConstants.LocalizationsTable,
                NotificationsModuleMetadata.Schema);

            localization.HasKey(value => value.LocalizationId);
            localization.Property(value => value.LocalizationId).ValueGeneratedNever();

            localization.Property(value => value.Language)
                .HasMaxLength(NotificationPersistenceConstants.LanguageLength)
                .IsRequired();

            localization.Property(value => value.Title)
                .HasMaxLength(NotificationPersistenceConstants.TitleLength)
                .IsRequired();

            localization.Property(value => value.Body)
                .HasMaxLength(NotificationPersistenceConstants.BodyLength)
                .IsRequired();

            localization.Property(value => value.ShortBody)
                .HasMaxLength(NotificationPersistenceConstants.ShortBodyLength);

            localization.Property(value => value.PluralBody)
                .HasMaxLength(NotificationPersistenceConstants.BodyLength);

            localization.Property(value => value.Format)
                .HasConversion<string>()
                .HasMaxLength(NotificationPersistenceConstants.EnumLength)
                .IsRequired();

            localization.HasIndex(
                    "NotificationTemplateId",
                    nameof(Domain.ValueObjects.TemplateLocalization.Language))
                .IsUnique()
                .HasDatabaseName("ix_notification_template_localizations_unique");
        });

    private static void ConfigureVariants(EntityTypeBuilder<NotificationTemplate> builder) =>
        builder.OwnsMany(template => template.Variants, variant =>
        {
            variant.ToTable(
                NotificationPersistenceConstants.VariantsTable,
                NotificationsModuleMetadata.Schema);

            variant.HasKey(value => value.VariantId);
            variant.Property(value => value.VariantId).ValueGeneratedNever();

            variant.Property(value => value.Channel)
                .HasConversion<string>()
                .HasMaxLength(NotificationPersistenceConstants.EnumLength)
                .IsRequired();

            variant.Property(value => value.Language)
                .HasMaxLength(NotificationPersistenceConstants.LanguageLength)
                .IsRequired();

            variant.Property(value => value.Title)
                .HasMaxLength(NotificationPersistenceConstants.TitleLength);

            variant.Property(value => value.Body)
                .HasMaxLength(NotificationPersistenceConstants.BodyLength);

            variant.Property(value => value.Format)
                .HasConversion<string>()
                .HasMaxLength(NotificationPersistenceConstants.EnumLength);
        });

    private static void ConfigureVariables(EntityTypeBuilder<NotificationTemplate> builder) =>
        builder.OwnsMany(template => template.Variables, variable =>
        {
            variable.ToTable(
                NotificationPersistenceConstants.VariablesTable,
                NotificationsModuleMetadata.Schema);

            variable.HasKey(value => value.VariableId);
            variable.Property(value => value.VariableId).ValueGeneratedNever();

            variable.Property(value => value.Name)
                .HasMaxLength(NotificationPersistenceConstants.KeyLength)
                .IsRequired();

            variable.Property(value => value.Format)
                .HasMaxLength(NotificationPersistenceConstants.EnumLength);

            variable.Property(value => value.Sample)
                .HasMaxLength(NotificationPersistenceConstants.ShortBodyLength);
        });

    private static void ConfigureActions(EntityTypeBuilder<NotificationTemplate> builder) =>
        builder.OwnsMany(template => template.Actions, action =>
        {
            action.ToTable(
                NotificationPersistenceConstants.TemplateActionsTable,
                NotificationsModuleMetadata.Schema);

            action.HasKey(value => value.ActionId);
            action.Property(value => value.ActionId).ValueGeneratedNever();

            action.Property(value => value.Key)
                .HasMaxLength(NotificationPersistenceConstants.KeyLength)
                .IsRequired();

            action.Property(value => value.Label)
                .HasMaxLength(NotificationPersistenceConstants.KeyLength)
                .IsRequired();

            action.Property(value => value.RouteTemplate)
                .HasMaxLength(NotificationPersistenceConstants.RouteLength);

            action.Property(value => value.Style)
                .HasConversion<string>()
                .HasMaxLength(NotificationPersistenceConstants.EnumLength)
                .IsRequired();
        });
}
