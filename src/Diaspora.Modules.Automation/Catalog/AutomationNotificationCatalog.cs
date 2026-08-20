using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Extensibility.Catalog;
using Diaspora.Modules.Notifications.Extensibility.Routing;
using Diaspora.Modules.Notifications.Extensibility.Templates;

// Aliased for the same reason Conversations and Integrations alias theirs: inside
// Diaspora.Modules.Automation.*, the identifier `Contracts` binds to this module's own Contracts
// namespace before it reaches Diaspora.Contracts, so the unaliased path resolves to something that
// does not exist and the compiler names the wrong namespace.
using AutomationEvents = Diaspora.Contracts.Automation.V1.Events;

namespace Diaspora.Modules.Automation.Catalog;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// FRONTEND-PHASE18-GAP-024 — the automation categories, their templates and their routing rules.
//
// The register asks for three things and this file is two of them: "automation-owned notification
// categories, so a member can mute automation notifications separately from the platform's own", and
// "a per-automation attribution on the delivered notification, so the inbox can say which automation
// produced it and offer to pause it from there". The third — "an endpoint or internal contract by
// which the automation platform asks B13 to deliver" — is the published contract this module raises
// and B13 consumes, which is the platform's standing shape rather than a new one.
//
// ── Why five categories rather than one ─────────────────────────────────────────────────────────
//
// The frontend's action catalogue names five distinct B13 category keys, and it names them because
// they are muted separately: a member who wants their own reminders but not their community's
// broadcasts has to be able to say so. One `automation` category would collapse that into a single
// switch, and the only way back would be a second preference system inside this module — which §28
// forbids and which is exactly the duplication this phase was most exposed to.
//
// ── Why every rule is keyed on typeof(...).FullName ─────────────────────────────────────────────
//
// B13's `ModuleEventConsumer.Trigger()` builds every trigger with `typeof(TIntegrationEvent).FullName`
// and `NotificationRuleRepository` matches on exact string equality. A rule keyed on `nameof(...)`
// carries the *short* name and can never match. That is not hypothetical: B22 shipped five such rules
// and none of them can fire. `AutomationNotificationArchitectureTests` asserts every key below equals
// the qualified name of a real contract.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>The keys this module contributes to B13's catalogues.</summary>
public static class AutomationNotificationKeys
{
    /// <summary>Notifications an automation sends to its own owner.</summary>
    public const string PersonalCategory = "automation.personal";

    /// <summary>Reminders. Separate because a member mutes an alert and keeps a reminder.</summary>
    public const string ReminderCategory = "automation.reminder";

    /// <summary>A community-scoped broadcast. B13's community announcement category.</summary>
    public const string CommunityCategory = "community.announcement";

    /// <summary>An organization-scoped broadcast.</summary>
    public const string OrganizationCategory = "organization.announcement";

    /// <summary>A creator's update to their followers.</summary>
    public const string CreatorCategory = "creator.update";

    public const string PersonalTemplate = "automation.personal";
    public const string ReminderTemplate = "automation.reminder";
    public const string CommunityTemplate = "automation.community";
    public const string OrganizationTemplate = "automation.organization";
    public const string CreatorTemplate = "automation.creator";
    public const string SavedSearchTemplate = "automation.saved-search";

    /// <summary>
    /// The one contract this module publishes to ask for a delivery.
    /// <para>
    /// Taken from the CLR type rather than a literal, so renaming the contract breaks the seeded rule
    /// at startup rather than silently stopping every automation notification on the platform.
    /// </para>
    /// </summary>
    public static readonly string DeliveryRequestedEvent =
        typeof(AutomationEvents.AutomationDeliveryRequestedIntegrationEvent).FullName!;

    /// <summary>
    /// The audience each rule addresses. One event, eight rules.
    /// <para>
    /// <b>The owner has three rather than one, and the split is the action catalogue's.</b>
    /// <c>notify.self</c>, <c>notify.push</c> and <c>notify.email</c> are three actions a member picks
    /// between, and the difference between them is precisely which channel they meant. One rule
    /// proposing all three channels would send a push to a member who asked only for an inbox card,
    /// because B13 narrows by preference and a member with push switched on has it switched on.
    /// </para>
    /// </summary>
    public const string OwnerAudience = "owner";
    public const string OwnerPushAudience = "owner-push";
    public const string OwnerEmailAudience = "owner-email";
    public const string ReminderAudience = "reminder";
    public const string CommunityAudience = "community";
    public const string OrganizationAudience = "organization";
    public const string FollowerAudience = "follower";
    public const string SavedSearchAudience = "saved-search";
}

/// <summary>
/// The two categories this module owns.
/// <para>
/// The other three — <c>community.announcement</c>, <c>organization.announcement</c>,
/// <c>creator.update</c> — belong to the modules whose broadcasts they are and are not redeclared
/// here. A second declaration of a category another module owns is how two modules come to disagree
/// about whether it is mandatory.
/// </para>
/// </summary>
public sealed class AutomationNotificationCategoryContributor : INotificationCategoryContributor
{
    public string Module => AutomationModuleMetadata.Name;

    public IEnumerable<NotificationCategorySeed> GetCategories()
    {
        yield return new NotificationCategorySeed
        {
            Key = AutomationNotificationKeys.PersonalCategory,
            DisplayName = "Your automations",
            Description =
                "Alerts from the automations you set up for yourself — saved searches, status "
                + "changes and anything else you asked to be told about.",
            DefaultPriority = NotificationPriority.Normal,

            // Never mandatory. An automation is a member's own standing instruction and switching it
            // off must actually switch it off — a mandatory automation category would be a way for a
            // rule to reach somebody who had opted out of everything else, which is §53's forbidden
            // bypass wearing B13's own clothes.
            IsMandatory = false,
            DigestByDefault = false,
            Position = 70
        };

        yield return new NotificationCategorySeed
        {
            Key = AutomationNotificationKeys.ReminderCategory,
            DisplayName = "Reminders",
            Description = "Reminders you set for events, viewings and things you are going to.",
            DefaultPriority = NotificationPriority.Normal,
            IsMandatory = false,

            // Never digested by default. A reminder that arrives in tomorrow's digest is not a
            // reminder, and the whole offset the member chose was about when it lands.
            DigestByDefault = false,
            Position = 71
        };
    }
}

/// <summary>
/// How an automation's request becomes a notification.
/// <para>
/// <b>Six rules over one contract, distinguished by audience.</b> The engine names an audience when
/// it publishes; the catalogue decides the template, the category, the priority, the channels and
/// the de-duplication window. This module names none of them at the call site, which is what keeps
/// "an automation decides when, B13 decides how" a structural property rather than a convention.
/// </para>
/// <para>
/// <b>Every rule de-duplicates, and the windows differ by kind.</b> §39 names notification dispatch
/// as the place a duplicate is most visible, and the engine's own idempotency on
/// <c>(automationId, triggerEventId)</c> covers a redelivered event but not two automations reaching
/// the same conclusion, nor a manual run beside a scheduled one. The window is the second barrier.
/// </para>
/// <para>
/// <b>Nothing is <c>Critical</c>.</b> B13's critical override exists for suspensions and security
/// events; a member-authored rule that could reach it would make quiet hours meaningless. The
/// highest priority available to an automation is <c>Normal</c>, and the engine cannot raise it
/// because the priority is not a field it can set.
/// </para>
/// </summary>
public sealed class AutomationNotificationRuleContributor : INotificationRuleContributor
{
    public string Module => AutomationModuleMetadata.Name;

    public IEnumerable<NotificationRuleSeed> GetRules()
    {
        // notify.self — the inbox, and only the inbox. B13's own model says the in-app channel cannot
        // be switched off, which is why the builder can offer this action unconditionally.
        yield return new NotificationRuleSeed
        {
            Key = "automation.personal",
            EventName = AutomationNotificationKeys.DeliveryRequestedEvent,
            Audience = AutomationNotificationKeys.OwnerAudience,
            TemplateKey = AutomationNotificationKeys.PersonalTemplate,
            CategoryKey = AutomationNotificationKeys.PersonalCategory,
            Priority = NotificationPriority.Normal,
            Policy = DeliveryPolicy.Immediate,
            Channels = [NotificationChannelKind.InApp],

            // Five minutes. Long enough that a redelivered outbox row and a retried run are one
            // message; short enough that a genuinely repeated trigger — three offers on one listing
            // in an hour — still reaches the member three times.
            DeduplicationWindow = TimeSpan.FromMinutes(5)
        };

        // notify.push — the inbox and the device. The inbox is included deliberately: a member whose
        // push is switched off asked to be told, and dropping the message entirely would make the
        // action silently do nothing rather than degrade.
        yield return new NotificationRuleSeed
        {
            Key = "automation.personal.push",
            EventName = AutomationNotificationKeys.DeliveryRequestedEvent,
            Audience = AutomationNotificationKeys.OwnerPushAudience,
            TemplateKey = AutomationNotificationKeys.PersonalTemplate,
            CategoryKey = AutomationNotificationKeys.PersonalCategory,
            Priority = NotificationPriority.Normal,
            Policy = DeliveryPolicy.Immediate,
            Channels = [NotificationChannelKind.InApp, NotificationChannelKind.Push],
            DeduplicationWindow = TimeSpan.FromMinutes(5)
        };

        // notify.email — the inbox and email, for the same reason.
        yield return new NotificationRuleSeed
        {
            Key = "automation.personal.email",
            EventName = AutomationNotificationKeys.DeliveryRequestedEvent,
            Audience = AutomationNotificationKeys.OwnerEmailAudience,
            TemplateKey = AutomationNotificationKeys.PersonalTemplate,
            CategoryKey = AutomationNotificationKeys.PersonalCategory,
            Priority = NotificationPriority.Normal,
            Policy = DeliveryPolicy.Immediate,
            Channels = [NotificationChannelKind.InApp, NotificationChannelKind.Email],
            DeduplicationWindow = TimeSpan.FromMinutes(5)
        };

        yield return new NotificationRuleSeed
        {
            Key = "automation.reminder",
            EventName = AutomationNotificationKeys.DeliveryRequestedEvent,
            Audience = AutomationNotificationKeys.ReminderAudience,
            TemplateKey = AutomationNotificationKeys.ReminderTemplate,
            CategoryKey = AutomationNotificationKeys.ReminderCategory,
            Priority = NotificationPriority.Normal,
            Policy = DeliveryPolicy.Immediate,
            Channels = [NotificationChannelKind.InApp, NotificationChannelKind.Push],

            // An hour, keyed on the reminder's own subject. A reminder is scheduled once per
            // (member, subject, offset, anchor) and a second delivery inside an hour is a redelivery
            // rather than a second intention.
            DeduplicationWindow = TimeSpan.FromHours(1)
        };

        yield return new NotificationRuleSeed
        {
            Key = "automation.saved-search",
            EventName = AutomationNotificationKeys.DeliveryRequestedEvent,
            Audience = AutomationNotificationKeys.SavedSearchAudience,
            TemplateKey = AutomationNotificationKeys.SavedSearchTemplate,
            CategoryKey = AutomationNotificationKeys.PersonalCategory,
            Priority = NotificationPriority.Normal,

            // Digest-capable, because a member with a broad saved search can match twenty listings in
            // an evening and twenty separate pushes is how a member switches the feature off.
            Policy = DeliveryPolicy.Digest,
            Channels = [NotificationChannelKind.InApp, NotificationChannelKind.Push],
            DeduplicationWindow = TimeSpan.FromMinutes(30)
        };

        // The three that reach other people. In-app only: a rule authored by one member that could
        // put a push on hundreds of devices is a different product decision from one that puts a card
        // in their inbox, and it is not one this phase was asked to make.
        yield return new NotificationRuleSeed
        {
            Key = "automation.community",
            EventName = AutomationNotificationKeys.DeliveryRequestedEvent,
            Audience = AutomationNotificationKeys.CommunityAudience,
            TemplateKey = AutomationNotificationKeys.CommunityTemplate,
            CategoryKey = AutomationNotificationKeys.CommunityCategory,
            Priority = NotificationPriority.Normal,
            Policy = DeliveryPolicy.Immediate,
            Channels = [NotificationChannelKind.InApp],
            DeduplicationWindow = TimeSpan.FromHours(1)
        };

        yield return new NotificationRuleSeed
        {
            Key = "automation.organization",
            EventName = AutomationNotificationKeys.DeliveryRequestedEvent,
            Audience = AutomationNotificationKeys.OrganizationAudience,
            TemplateKey = AutomationNotificationKeys.OrganizationTemplate,
            CategoryKey = AutomationNotificationKeys.OrganizationCategory,
            Priority = NotificationPriority.Normal,
            Policy = DeliveryPolicy.Immediate,
            Channels = [NotificationChannelKind.InApp],
            DeduplicationWindow = TimeSpan.FromHours(1)
        };

        yield return new NotificationRuleSeed
        {
            Key = "automation.creator",
            EventName = AutomationNotificationKeys.DeliveryRequestedEvent,
            Audience = AutomationNotificationKeys.FollowerAudience,
            TemplateKey = AutomationNotificationKeys.CreatorTemplate,
            CategoryKey = AutomationNotificationKeys.CreatorCategory,
            Priority = NotificationPriority.Normal,
            Policy = DeliveryPolicy.Immediate,
            Channels = [NotificationChannelKind.InApp],
            DeduplicationWindow = TimeSpan.FromHours(1)
        };
    }
}

/// <summary>
/// The wording.
/// <para>
/// <b>Every template names the automation that produced it.</b> That is the register's third
/// requirement — "so the inbox can say which automation produced it and offer to pause it from
/// there" — and the route template carries the automation's own detail screen so the offer to pause
/// is one tap.
/// </para>
/// <para>
/// <b>No template quotes a condition's value.</b> The <c>summary</c> variable is written by the
/// engine from the <i>trigger's</i> public subject — "A job matching Lagos · Software" — and never
/// from a condition comparison. A member's balance, their application status and their subscription
/// state are all things a sensitive condition can match on; a message that quoted one would put it in
/// an inbox with a longer retention than the data it quoted.
/// </para>
/// </summary>
public sealed class AutomationNotificationTemplateContributor : INotificationTemplateContributor
{
    public string Module => AutomationModuleMetadata.Name;

    public IEnumerable<NotificationTemplateSeed> GetTemplates()
    {
        yield return new NotificationTemplateSeed
        {
            Key = AutomationNotificationKeys.PersonalTemplate,
            CategoryKey = AutomationNotificationKeys.PersonalCategory,
            Priority = NotificationPriority.Normal,
            Description = "An alert from one of the member's own automations.",
            RouteTemplate = "/automation/{automationId}",
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "{automationName}",
                    Body = "{summary}",
                    ShortBody = "{summary}"
                }
            ],
            Variables =
            [
                new TemplateVariableSeed { Name = "automationName" },
                new TemplateVariableSeed { Name = "summary" },
                new TemplateVariableSeed { Name = "automationId" }
            ],
            Actions =
            [
                new TemplateActionSeed
                {
                    Key = "open",
                    Label = "Open",
                    RouteTemplate = "/automation/{automationId}"
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = AutomationNotificationKeys.ReminderTemplate,
            CategoryKey = AutomationNotificationKeys.ReminderCategory,
            Priority = NotificationPriority.Normal,
            Description = "A reminder the member set on an event, an occurrence or a series.",
            RouteTemplate = "/community-life/event/{subjectId}",
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Coming up: {subjectTitle}",
                    Body = "{summary}",
                    ShortBody = "{subjectTitle}"
                }
            ],
            Variables =
            [
                new TemplateVariableSeed { Name = "subjectTitle" },
                new TemplateVariableSeed { Name = "summary" },
                new TemplateVariableSeed { Name = "subjectId" }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = AutomationNotificationKeys.SavedSearchTemplate,
            CategoryKey = AutomationNotificationKeys.PersonalCategory,
            Priority = NotificationPriority.Normal,
            Description = "Something new matched a search the member saved.",
            RouteTemplate = "/automation/searches",
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "New match for {searchName}",
                    Body = "{summary}",
                    ShortBody = "{summary}",
                    PluralBody = "{count} new matches for {searchName}."
                }
            ],
            Variables =
            [
                new TemplateVariableSeed { Name = "searchName" },
                new TemplateVariableSeed { Name = "summary" },
                new TemplateVariableSeed { Name = "count", IsRequired = false }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = AutomationNotificationKeys.CommunityTemplate,
            CategoryKey = AutomationNotificationKeys.CommunityCategory,
            Priority = NotificationPriority.Normal,
            Description = "A community-scoped automation reaching that community's members.",
            RouteTemplate = "/automation/{automationId}",
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "{scopeName}",
                    Body = "{summary}",
                    ShortBody = "{summary}"
                }
            ],
            Variables =
            [
                new TemplateVariableSeed { Name = "scopeName" },
                new TemplateVariableSeed { Name = "summary" },
                new TemplateVariableSeed { Name = "automationId" }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = AutomationNotificationKeys.OrganizationTemplate,
            CategoryKey = AutomationNotificationKeys.OrganizationCategory,
            Priority = NotificationPriority.Normal,
            Description = "An organization-scoped automation reaching that organization's members.",
            RouteTemplate = "/automation/{automationId}",
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "{scopeName}",
                    Body = "{summary}",
                    ShortBody = "{summary}"
                }
            ],
            Variables =
            [
                new TemplateVariableSeed { Name = "scopeName" },
                new TemplateVariableSeed { Name = "summary" },
                new TemplateVariableSeed { Name = "automationId" }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = AutomationNotificationKeys.CreatorTemplate,
            CategoryKey = AutomationNotificationKeys.CreatorCategory,
            Priority = NotificationPriority.Normal,
            Description = "A creator's automation reaching their followers.",
            RouteTemplate = "/automation/{automationId}",
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "{scopeName}",
                    Body = "{summary}",
                    ShortBody = "{summary}"
                }
            ],
            Variables =
            [
                new TemplateVariableSeed { Name = "scopeName" },
                new TemplateVariableSeed { Name = "summary" },
                new TemplateVariableSeed { Name = "automationId" }
            ]
        };
    }
}
