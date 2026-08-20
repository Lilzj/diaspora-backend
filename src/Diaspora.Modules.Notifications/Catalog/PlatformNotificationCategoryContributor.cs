using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Extensibility.Catalog;

namespace Diaspora.Modules.Notifications.Catalog;

/// <summary>
/// The categories the platform ships with.
/// <para>
/// <b>Two are mandatory and the choice of which is the module's clearest value judgement.</b>
/// Security cannot be muted because a member who has silenced sign-in alerts has silenced the only
/// warning they will get that somebody else is in their account. Moderation cannot be muted because
/// it is the only notice a member receives that something they wrote was removed — and a platform
/// where that can be switched off is a platform where people find their posts gone and no
/// explanation anywhere.
/// </para>
/// <para>
/// Community and reviews default to a digest, because a single reaction is not worth interrupting
/// anybody and forty of them in an evening is worth one line tomorrow.
/// </para>
/// <para>
/// Positions are spaced so a deployment can insert a category without renumbering the catalogue.
/// </para>
/// </summary>
public sealed class PlatformNotificationCategoryContributor : INotificationCategoryContributor
{
    public string Module => NotificationsModuleMetadata.Name;

    public IEnumerable<NotificationCategorySeed> GetCategories()
    {
        yield return new NotificationCategorySeed
        {
            Key = NotificationCategoryKeys.Security,
            DisplayName = "Security",
            Description = "Sign-ins, password changes and account restrictions. Always delivered.",
            DefaultPriority = NotificationPriority.Critical,
            IsMandatory = true,
            Position = 10
        };

        yield return new NotificationCategorySeed
        {
            Key = NotificationCategoryKeys.Moderation,
            DisplayName = "Moderation",
            Description = "Decisions about content you posted. Always delivered.",
            DefaultPriority = NotificationPriority.High,
            IsMandatory = true,
            Position = 20
        };

        yield return new NotificationCategorySeed
        {
            Key = NotificationCategoryKeys.Trust,
            DisplayName = "Verification",
            Description = "Verification outcomes, badges and trust changes.",
            DefaultPriority = NotificationPriority.High,
            Position = 30
        };

        yield return new NotificationCategorySeed
        {
            Key = NotificationCategoryKeys.Jobs,
            DisplayName = "Jobs",
            Description = "Applications, interviews and offers.",
            DefaultPriority = NotificationPriority.High,
            Position = 40
        };

        yield return new NotificationCategorySeed
        {
            Key = NotificationCategoryKeys.Housing,
            DisplayName = "Housing",
            Description = "Viewings, reservations and landlord updates.",
            DefaultPriority = NotificationPriority.High,
            Position = 50
        };

        yield return new NotificationCategorySeed
        {
            Key = NotificationCategoryKeys.Marketplace,
            DisplayName = "Marketplace",
            Description = "Offers, reservations and sales.",
            DefaultPriority = NotificationPriority.Normal,
            Position = 60
        };

        yield return new NotificationCategorySeed
        {
            Key = NotificationCategoryKeys.Community,
            DisplayName = "Community",
            Description = "Mentions, replies and reactions.",
            DefaultPriority = NotificationPriority.Low,
            DigestByDefault = true,
            Position = 70
        };

        yield return new NotificationCategorySeed
        {
            Key = NotificationCategoryKeys.Organizations,
            DisplayName = "Organizations",
            Description = "Invitations and membership decisions.",
            DefaultPriority = NotificationPriority.Normal,
            Position = 80
        };

        yield return new NotificationCategorySeed
        {
            Key = NotificationCategoryKeys.Events,
            DisplayName = "Events",
            Description = "Responses, reminders and changes to events.",
            DefaultPriority = NotificationPriority.Normal,
            Position = 90
        };

        yield return new NotificationCategorySeed
        {
            Key = NotificationCategoryKeys.Guides,
            DisplayName = "Community Guides",
            Description = "Newly published and featured guides.",
            DefaultPriority = NotificationPriority.Low,
            DigestByDefault = true,
            Position = 100
        };

        yield return new NotificationCategorySeed
        {
            Key = NotificationCategoryKeys.Learning,
            DisplayName = "Learning",
            Description = "Learning paths and country knowledge updates.",
            DefaultPriority = NotificationPriority.Low,
            DigestByDefault = true,
            Position = 110
        };

        yield return new NotificationCategorySeed
        {
            Key = NotificationCategoryKeys.Provider,
            DisplayName = "Providers",
            Description = "Provider verification and availability.",
            DefaultPriority = NotificationPriority.Normal,
            Position = 120
        };

        yield return new NotificationCategorySeed
        {
            Key = NotificationCategoryKeys.Reviews,
            DisplayName = "New Reviews",
            Description = "When someone reviews or recommends you.",
            DefaultPriority = NotificationPriority.Low,
            DigestByDefault = true,
            Position = 130
        };

        yield return new NotificationCategorySeed
        {
            Key = NotificationCategoryKeys.Alerts,
            DisplayName = "New Alerts",
            Description = "Safety alerts and area updates.",
            DefaultPriority = NotificationPriority.High,
            Position = 140
        };

        yield return new NotificationCategorySeed
        {
            Key = NotificationCategoryKeys.Updates,
            DisplayName = "App Updates",
            Description = "New features and improvements.",
            DefaultPriority = NotificationPriority.Low,
            DigestByDefault = true,
            Position = 150
        };

        yield return new NotificationCategorySeed
        {
            Key = NotificationCategoryKeys.Platform,
            DisplayName = "Platform",
            Description = "Announcements and account lifecycle.",
            DefaultPriority = NotificationPriority.Normal,
            Position = 160
        };

        // Declared with no rules behind it, deliberately. A billing module landing later brings its
        // own rules and templates and needs no client release to appear on the settings screen.
        yield return new NotificationCategorySeed
        {
            Key = NotificationCategoryKeys.Billing,
            DisplayName = "Billing",
            Description = "Payments and receipts.",
            DefaultPriority = NotificationPriority.High,
            Position = 170
        };
    }
}
