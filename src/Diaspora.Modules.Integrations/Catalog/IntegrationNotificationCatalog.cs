using Diaspora.Modules.Notifications.Catalog;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Extensibility.Catalog;
using Diaspora.Modules.Notifications.Extensibility.Routing;
using Diaspora.Modules.Notifications.Extensibility.Templates;

// Aliased for the same reason Conversations aliases its own: inside
// Diaspora.Modules.Integrations.*, the identifier `Contracts` binds to this module's own Contracts
// namespace before it reaches Diaspora.Contracts, so the unaliased path resolves to something that
// does not exist and the compiler names the wrong namespace.
using IntegrationEvents = Diaspora.Contracts.Integrations.V1.Events;

namespace Diaspora.Modules.Integrations.Catalog;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// FRONTEND-PHASE16-GAP-017 — the `integrations` category and its templates.
//
// B13.3 classified this EXTERNAL DEPENDENCY on "grant and sync lifecycle events", and separated one
// of the five: "'a new sign-in method was linked' is a *security* notification, belongs in the
// mandatory set, and is Identity's to publish — it does not wait on the integration platform."
//
// Both halves are honoured here. The four operational notices go in a new, mutable `integrations`
// category. The security one goes in B13's existing mandatory `security` category, and it is routed
// on this module's own connected contract rather than waiting on Identity — because the fact it
// announces is one this module now publishes, and a member must not be able to switch off the alert
// that tells them somebody attached an account to theirs.
//
// Nothing about B13 changed. These are three contributor implementations in this module's assembly.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>The keys this module contributes to B13's catalogues.</summary>
public static class IntegrationNotificationKeys
{
    /// <summary>The operational category. Mutable — see the contributor.</summary>
    public const string Category = "integrations";

    public const string GrantExpiringTemplate = "integrations.grant.expiring";
    public const string ReauthorizationRequiredTemplate = "integrations.grant.reauthorization-required";
    public const string SyncFailedTemplate = "integrations.sync.failed";
    public const string DisconnectedTemplate = "integrations.disconnected";

    /// <summary>The security one. Lives in B13's mandatory <c>security</c> category.</summary>
    public const string SignInMethodLinkedTemplate = "integrations.identity.linked";

    // Rule event names are the contract's type name, written as `nameof` so a rename is a compile
    // error rather than a rule that silently stops matching. B13's ModuleEventConsumer resolves on
    // this.

    public static readonly string GrantExpiringEvent =
        nameof(IntegrationEvents.IntegrationGrantExpiringIntegrationEvent);

    public static readonly string ReauthorizationRequiredEvent =
        nameof(IntegrationEvents.IntegrationReauthorizationRequiredIntegrationEvent);

    public static readonly string SyncFailedEvent =
        nameof(IntegrationEvents.IntegrationSyncFailedIntegrationEvent);

    public static readonly string DisconnectedEvent =
        nameof(IntegrationEvents.IntegrationDisconnectedIntegrationEvent);

    public static readonly string ConnectedEvent =
        nameof(IntegrationEvents.IntegrationConnectedIntegrationEvent);
}

/// <summary>
/// The <c>integrations</c> category.
/// <para>
/// <b>Not mandatory, and the contrast with the security notification is the whole point.</b> A
/// member who switches off "your calendar grant expires in three days" has made a legitimate choice
/// about their own attention. A member who could switch off "somebody attached an account to yours"
/// has made a choice an attacker would make for them — so that one is not in this category.
/// </para>
/// <para>
/// <b>Digest is on by default.</b> A grant expiring in seventy-two hours is not a reason to
/// interrupt somebody, and the register's own priority for the whole GAP-017 family is medium. The
/// two entries that genuinely cannot wait — a revoked grant and a failed sync needing attention —
/// override the policy on their own rules rather than by making the whole category loud.
/// </para>
/// </summary>
public sealed class IntegrationNotificationCategoryContributor : INotificationCategoryContributor
{
    public string Module => IntegrationsModuleMetadata.Name;

    public IEnumerable<NotificationCategorySeed> GetCategories()
    {
        yield return new NotificationCategorySeed
        {
            Key = IntegrationNotificationKeys.Category,
            DisplayName = "Connected accounts",
            Description =
                "Expiring authorizations, accounts that need reconnecting, and synchronisation "
                + "problems.",
            DefaultPriority = NotificationPriority.Normal,
            IsMandatory = false,
            DigestByDefault = true,
            Position = 60
        };
    }
}

/// <summary>
/// How an integration fact becomes a notification.
/// <para>
/// <b>Five rules, and the two that are not in the <c>integrations</c> category are the interesting
/// ones.</b> A newly linked sign-in method is a security notification in B13's mandatory category. A
/// disconnection is routed only when the <em>provider</em> initiated it — a member who disconnected
/// does not need telling, and the rule's audience is what makes that distinction without a second
/// contract.
/// </para>
/// <para>
/// <b>Every rule has a deduplication window.</b> The expiry sweep announces once per grant per
/// expiry, but a sweep that ran twice across a deployment restart would otherwise send twice, and a
/// window is a cheaper guarantee than a "have I announced this" column on the connection.
/// </para>
/// </summary>
public sealed class IntegrationNotificationRuleContributor : INotificationRuleContributor
{
    public string Module => IntegrationsModuleMetadata.Name;

    public IEnumerable<NotificationRuleSeed> GetRules()
    {
        // A security notification, in the mandatory category, and it cannot be switched off.
        // "A member must not be able to switch off the alert that tells them somebody attached an
        // account to theirs."
        yield return new NotificationRuleSeed
        {
            Key = "integrations.identity.linked",
            EventName = IntegrationNotificationKeys.ConnectedEvent,
            Audience = "identity",
            TemplateKey = IntegrationNotificationKeys.SignInMethodLinkedTemplate,
            CategoryKey = NotificationCategoryKeys.Security,
            Priority = NotificationPriority.High,
            Policy = DeliveryPolicy.Immediate,
            Channels = [NotificationChannelKind.InApp, NotificationChannelKind.Push],
            DeduplicationWindow = TimeSpan.FromMinutes(5)
        };

        // The grant is gone. Immediate and high, because the member's calendar export has stopped
        // working right now and every attempt until they act will fail.
        yield return new NotificationRuleSeed
        {
            Key = "integrations.grant.reauthorization-required",
            EventName = IntegrationNotificationKeys.ReauthorizationRequiredEvent,
            TemplateKey = IntegrationNotificationKeys.ReauthorizationRequiredTemplate,
            CategoryKey = IntegrationNotificationKeys.Category,
            Priority = NotificationPriority.High,
            Policy = DeliveryPolicy.Immediate,
            Channels = [NotificationChannelKind.InApp, NotificationChannelKind.Push],

            // A day. A revocation and a subsequent failed call would otherwise both announce, and
            // they are one event to the member.
            DeduplicationWindow = TimeSpan.FromHours(24)
        };

        // The grant will go. Batched, because seventy-two hours' notice is not an interruption.
        yield return new NotificationRuleSeed
        {
            Key = "integrations.grant.expiring",
            EventName = IntegrationNotificationKeys.GrantExpiringEvent,
            TemplateKey = IntegrationNotificationKeys.GrantExpiringTemplate,
            CategoryKey = IntegrationNotificationKeys.Category,
            Priority = NotificationPriority.Normal,
            Policy = DeliveryPolicy.Digest,
            Channels = [NotificationChannelKind.InApp],

            // Three days, matching the warning window. A sweep running every fifteen minutes must
            // not produce a notification every fifteen minutes.
            DeduplicationWindow = TimeSpan.FromDays(3)
        };

        yield return new NotificationRuleSeed
        {
            Key = "integrations.sync.failed",
            EventName = IntegrationNotificationKeys.SyncFailedEvent,
            TemplateKey = IntegrationNotificationKeys.SyncFailedTemplate,
            CategoryKey = IntegrationNotificationKeys.Category,
            Priority = NotificationPriority.Normal,
            Policy = DeliveryPolicy.Immediate,
            Channels = [NotificationChannelKind.InApp],
            DeduplicationWindow = TimeSpan.FromHours(6)
        };

        // Only when the provider initiated it. A member who disconnected knows.
        yield return new NotificationRuleSeed
        {
            Key = "integrations.disconnected.by-provider",
            EventName = IntegrationNotificationKeys.DisconnectedEvent,
            Audience = "provider-initiated",
            TemplateKey = IntegrationNotificationKeys.DisconnectedTemplate,
            CategoryKey = IntegrationNotificationKeys.Category,
            Priority = NotificationPriority.Normal,
            Policy = DeliveryPolicy.Immediate,
            Channels = [NotificationChannelKind.InApp],
            DeduplicationWindow = TimeSpan.FromHours(24)
        };
    }
}

/// <summary>
/// The wording.
/// <para>
/// <b>No account label appears in any of these, and the security one is the reason.</b> The
/// register: "The body must not contain the linked address in a lock-screen preview." That
/// constraint is easiest to honour by applying it to all five — a template that names the provider
/// and not the account is renderable on a lock screen without disclosing which email address
/// somebody uses.
/// </para>
/// <para>
/// <b>No calendar title either.</b> A sync-failure notice says how many entries failed, never which
/// — a notification body is a durable record with a longer retention than the data it would quote.
/// </para>
/// <para>
/// Every route resolves into <c>/settings/integrations</c>, which
/// <c>notification-routes.ts</c> already treats as a resolvable prefix — the client half of GAP-017
/// is complete and has been since Phase 16 shipped.
/// </para>
/// </summary>
public sealed class IntegrationNotificationTemplateContributor : INotificationTemplateContributor
{
    public string Module => IntegrationsModuleMetadata.Name;

    public IEnumerable<NotificationTemplateSeed> GetTemplates()
    {
        yield return new NotificationTemplateSeed
        {
            Key = IntegrationNotificationKeys.SignInMethodLinkedTemplate,
            CategoryKey = NotificationCategoryKeys.Security,
            Priority = NotificationPriority.High,
            RouteTemplate = "/settings/integrations/identity",
            Description = "A new sign-in method was attached to this account.",
            Variables =
            [
                new TemplateVariableSeed { Name = "provider", Sample = "identity.google" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "A new sign-in method was added",
                    Body =
                        "A {provider} account was connected to your Diaspora account. If this was "
                        + "not you, open your sign-in settings now.",
                    ShortBody = "New sign-in method added"
                },
                new TemplateLocalizationSeed
                {
                    Language = "fr",
                    Title = "Une nouvelle méthode de connexion a été ajoutée",
                    Body =
                        "Un compte {provider} a été connecté à votre compte Diaspora. Si ce n'était "
                        + "pas vous, ouvrez vos paramètres de connexion maintenant.",
                    ShortBody = "Nouvelle méthode de connexion"
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = IntegrationNotificationKeys.ReauthorizationRequiredTemplate,
            CategoryKey = IntegrationNotificationKeys.Category,
            Priority = NotificationPriority.High,
            RouteTemplate = "/settings/integrations",
            Description = "A grant stopped working and the member has to reconnect.",
            Variables =
            [
                new TemplateVariableSeed { Name = "provider", Sample = "calendar.google" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Reconnect your account",
                    Body =
                        "Your {provider} connection has stopped working and needs to be set up "
                        + "again.",
                    ShortBody = "Reconnect your account"
                },
                new TemplateLocalizationSeed
                {
                    Language = "fr",
                    Title = "Reconnectez votre compte",
                    Body =
                        "Votre connexion {provider} ne fonctionne plus et doit être rétablie.",
                    ShortBody = "Reconnectez votre compte"
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = IntegrationNotificationKeys.GrantExpiringTemplate,
            CategoryKey = IntegrationNotificationKeys.Category,
            Priority = NotificationPriority.Normal,
            RouteTemplate = "/settings/integrations",
            Description = "A grant is about to lapse.",
            Variables =
            [
                new TemplateVariableSeed { Name = "provider", Sample = "calendar.google" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "A connection is about to expire",
                    Body =
                        "Your {provider} connection expires soon. Reconnect it to keep it working.",
                    ShortBody = "Connection expiring"
                },
                new TemplateLocalizationSeed
                {
                    Language = "fr",
                    Title = "Une connexion va bientôt expirer",
                    Body =
                        "Votre connexion {provider} expire bientôt. Reconnectez-la pour qu'elle "
                        + "continue de fonctionner.",
                    ShortBody = "Connexion bientôt expirée"
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = IntegrationNotificationKeys.SyncFailedTemplate,
            CategoryKey = IntegrationNotificationKeys.Category,
            Priority = NotificationPriority.Normal,
            RouteTemplate = "/settings/integrations/calendar",
            Description = "A synchronisation ended other than cleanly.",
            Variables =
            [
                new TemplateVariableSeed { Name = "provider", Sample = "calendar.google" },
                new TemplateVariableSeed { Name = "itemsfailed", IsRequired = false, Sample = "2" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "A synchronisation did not finish",
                    Body =
                        "{itemsfailed} entries could not be synchronised with {provider}. Open your "
                        + "calendar settings to see why.",
                    ShortBody = "Synchronisation problem"
                },
                new TemplateLocalizationSeed
                {
                    Language = "fr",
                    Title = "Une synchronisation ne s'est pas terminée",
                    Body =
                        "{itemsfailed} éléments n'ont pas pu être synchronisés avec {provider}. "
                        + "Ouvrez vos paramètres de calendrier pour en savoir plus.",
                    ShortBody = "Problème de synchronisation"
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = IntegrationNotificationKeys.DisconnectedTemplate,
            CategoryKey = IntegrationNotificationKeys.Category,
            Priority = NotificationPriority.Normal,
            RouteTemplate = "/settings/integrations",
            Description = "A provider removed a grant this member had given.",
            Variables =
            [
                new TemplateVariableSeed { Name = "provider", Sample = "calendar.google" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "A connection was removed",
                    Body =
                        "Your {provider} connection was removed at the provider. Connect it again "
                        + "if you still want it.",
                    ShortBody = "Connection removed"
                },
                new TemplateLocalizationSeed
                {
                    Language = "fr",
                    Title = "Une connexion a été supprimée",
                    Body =
                        "Votre connexion {provider} a été supprimée chez le fournisseur. "
                        + "Reconnectez-la si vous en avez encore besoin.",
                    ShortBody = "Connexion supprimée"
                }
            ]
        };
    }
}
