using Diaspora.Modules.Notifications.Catalog.Rules;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.ValueObjects;
using Diaspora.Modules.Notifications.Extensibility.Templates;

namespace Diaspora.Modules.Notifications.Catalog.Templates;

/// <summary>
/// The wording for B18's messages.
/// <para>
/// <b>None of these names the organisation, and none names the person who decided.</b> "The
/// organisation has accepted your application" rather than "The Nigerian Community Association has":
/// the published contracts carry identifiers and not display names, and resolving one would mean this
/// module querying B12 for a name to put in a push banner. The client resolves it when the member
/// opens the screen, where it belongs.
/// </para>
/// <para>
/// <b>Every route sends the member to a real screen.</b> The frontend's <c>notification-routes.ts</c>
/// already resolves the <c>/community-life</c> prefix, so a membership decision lands where the
/// member expects rather than on a hub they have to navigate from.
/// </para>
/// </summary>
public sealed class CommunityProgrammeTemplateContributor : INotificationTemplateContributor
{
    public string Module => NotificationsModuleMetadata.Name;

    public IEnumerable<NotificationTemplateSeed> GetTemplates()
    {
        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.ProgrammeMembershipApproved,
            CategoryKey = NotificationCategoryKeys.Organizations,
            Priority = NotificationPriority.High,
            RouteTemplate = "/community-life/membership/{membershipid}",
            Description = "An organisation accepted a membership application.",
            Variables =
            [
                new TemplateVariableSeed { Name = "membershipid", Sample = "8f2c" },
                new TemplateVariableSeed
                {
                    Name = "organizationid", IsRequired = false, Sample = "3a91"
                }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Your membership was approved",
                    Body = "The organisation has accepted your application. "
                        + "Open it to see what happens next.",
                    ShortBody = "Your membership application was approved."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.ProgrammeMembershipRejected,
            CategoryKey = NotificationCategoryKeys.Organizations,

            // Normal rather than high. A refusal is not urgent, and interrupting somebody's evening
            // with one is the opposite of kind.
            Priority = NotificationPriority.Normal,
            RouteTemplate = "/community-life/membership/{membershipid}",
            Description = "An organisation refused a membership application.",
            Variables =
            [
                new TemplateVariableSeed { Name = "membershipid", Sample = "8f2c" },

                // The organisation's own words, when it gave any. Optional, because it may not have.
                new TemplateVariableSeed { Name = "reason", IsRequired = false, Sample = "" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Your membership application was not accepted",
                    Body = "The organisation did not accept this application. "
                        + "Open it to see what they said.",
                    ShortBody = "Your membership application was not accepted."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.ProgrammeMembershipRenewalDue,
            CategoryKey = NotificationCategoryKeys.Organizations,
            Priority = NotificationPriority.Normal,
            RouteTemplate = "/community-life/membership/{membershipid}",
            Description = "A membership entered its renewal window.",
            Variables = [new TemplateVariableSeed { Name = "membershipid", Sample = "8f2c" }],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Your membership needs renewing",
                    Body = "Your membership is due for renewal. "
                        + "It still works in the meantime — renewing keeps it that way.",
                    ShortBody = "Your membership is due for renewal."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.ProgrammeMembershipExpired,
            CategoryKey = NotificationCategoryKeys.Organizations,
            Priority = NotificationPriority.Normal,
            RouteTemplate = "/community-life/membership/{membershipid}",
            Description = "A membership passed its expiry.",
            Variables = [new TemplateVariableSeed { Name = "membershipid", Sample = "8f2c" }],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Your membership has ended",
                    Body = "This membership has ended. You can rejoin whenever you like.",
                    ShortBody = "Your membership has ended."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.VolunteerApplicationDecided,
            CategoryKey = NotificationCategoryKeys.Community,
            Priority = NotificationPriority.Normal,
            RouteTemplate = "/community-life/volunteer/applications/{applicationid}",
            Description = "A volunteer application was approved, refused or waitlisted.",
            Variables =
            [
                new TemplateVariableSeed { Name = "applicationid", Sample = "8f2c" },
                new TemplateVariableSeed { Name = "decision", Sample = "Approved" },
                new TemplateVariableSeed { Name = "reason", IsRequired = false, Sample = "" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "There is news about your volunteering",
                    Body = "The organisers have made a decision about your application. "
                        + "Open it to see.",
                    ShortBody = "Your volunteer application was decided."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.VolunteerAssignmentCreated,
            CategoryKey = NotificationCategoryKeys.Community,
            Priority = NotificationPriority.High,
            RouteTemplate = "/community-life/volunteer/assignments/{assignmentid}",
            Description = "An approved volunteer was placed on a shift.",
            Variables = [new TemplateVariableSeed { Name = "assignmentid", Sample = "8f2c" }],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "You have been given a shift",
                    Body = "You have been placed on a volunteering shift. "
                        + "Open it to check the time and the place.",
                    ShortBody = "You have been given a volunteering shift."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.VolunteerHoursConfirmed,
            CategoryKey = NotificationCategoryKeys.Community,

            // Low, and digest-bound by rule. A receipt that interrupts somebody who has just walked
            // home from the shift it refers to is the clearest example of a notification nobody asked
            // for — the same reasoning B13 applies to an event check-in.
            Priority = NotificationPriority.Low,
            RouteTemplate = "/community-life/volunteer/hours",
            Description = "An organiser confirmed the hours a volunteer worked.",
            Variables =
            [
                new TemplateVariableSeed { Name = "assignmentid", Sample = "8f2c" },
                new TemplateVariableSeed { Name = "hours", Sample = "3.5" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Your volunteering hours were confirmed",
                    Body = "An organiser confirmed {hours} hours. They now count towards your record.",
                    ShortBody = "{hours} volunteering hours were confirmed."
                }
            ]
        };

        // ── Closing a community, and handing one over ────────────────────────────────────────
        //
        // Six messages read by one of two people: an owner who asked for their community to be
        // closed, or a moderator being asked to take one on. Neither is a routine update, and the
        // first four are read by somebody who has already decided something difficult.
        //
        // **The slug rather than the identifier, in every route.** These are the only templates in
        // this file that link to a community rather than to a programme, and the client's community
        // screens are addressed by slug. The identifier still travels on the trigger's subject,
        // where a later reminder rule can key on it.
        //
        // **Nobody is named**, as everywhere else in this module: the contracts carry identifiers,
        // and "the person you asked" says all the owner needs to open the screen and see who.

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.CommunityClosureApproved,
            CategoryKey = NotificationCategoryKeys.Community,

            // High. There is a window here and it closes: the community goes on the day named, and
            // the owner can still stop it until then. A message they read a week late is the one
            // fact this template exists to prevent.
            Priority = NotificationPriority.High,
            RouteTemplate = "/community/{communityslug}",
            Description = "Diaspora agreed to close a community.",
            Variables =
            [
                new TemplateVariableSeed
                {
                    Name = "communityname", Sample = "Nigerians in Leeds"
                },
                new TemplateVariableSeed
                {
                    Name = "communityslug", Sample = "nigerians-in-leeds"
                },

                // The date rather than the instant: a closure lands on a day, and a wall clock on
                // it would invite somebody to count the hours.
                new TemplateVariableSeed
                {
                    Name = "effectiveat",
                    Format = TemplateVariable.DateFormat,
                    Sample = "26 September"
                }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "{communityname} will close on {effectiveat}",
                    Body = "Diaspora has agreed to close this community. It closes on "
                        + "{effectiveat}. You can still change your mind until then — withdrawing "
                        + "the request keeps the community open.",
                    ShortBody = "{communityname} closes on {effectiveat}."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.CommunityClosureRefused,
            CategoryKey = NotificationCategoryKeys.Community,

            // Normal rather than high, on the same reasoning as a refused membership: nothing is
            // about to happen, and interrupting somebody's evening with a no is unkind.
            Priority = NotificationPriority.Normal,
            RouteTemplate = "/community/{communityslug}",
            Description = "Diaspora refused a request to close a community.",
            Variables =
            [
                new TemplateVariableSeed
                {
                    Name = "communityname", Sample = "Nigerians in Leeds"
                },
                new TemplateVariableSeed
                {
                    Name = "communityslug", Sample = "nigerians-in-leeds"
                },

                // The administrator's own sentence, quoted rather than summarised — an owner told
                // no is owed the words somebody wrote and not a code. Optional because the contract
                // types it as nullable; a refusal that arrives without one still reaches the owner
                // with everything else intact, which is the behaviour worth having.
                new TemplateVariableSeed
                {
                    Name = "reason",
                    IsRequired = false,
                    Sample = "This community is active and its members have not been consulted."
                }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "{communityname} is staying open",
                    Body = "Diaspora is not closing this community. {reason} If you would rather "
                        + "not run it any more, you can hand it to one of its moderators or "
                        + "administrators instead.",
                    ShortBody = "Diaspora is not closing {communityname}."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.CommunityClosed,
            CategoryKey = NotificationCategoryKeys.Community,

            // Normal. It has happened, there is nothing to do about it, and the owner asked for it.
            Priority = NotificationPriority.Normal,
            RouteTemplate = "/community/{communityslug}",
            Description = "An approved closure came due and the community is closed.",
            Variables =
            [
                new TemplateVariableSeed
                {
                    Name = "communityname", Sample = "Nigerians in Leeds"
                },
                new TemplateVariableSeed
                {
                    Name = "communityslug", Sample = "nigerians-in-leeds"
                }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "{communityname} has closed",
                    Body = "This community is now closed, as you asked. Its posts, events and "
                        + "member list are no longer on Diaspora.",
                    ShortBody = "{communityname} has closed."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.CommunityOwnershipOffered,
            CategoryKey = NotificationCategoryKeys.Community,

            // High, and the one template here whose reader has to do something. An offer that
            // lapses because nobody saw it costs the owner their way out.
            Priority = NotificationPriority.High,
            RouteTemplate = "/community/{communityslug}/ownership",
            Description = "An owner asked somebody to take their community over.",
            Variables =
            [
                new TemplateVariableSeed
                {
                    Name = "communityname", Sample = "Nigerians in Leeds"
                },
                new TemplateVariableSeed
                {
                    Name = "communityslug", Sample = "nigerians-in-leeds"
                },
                new TemplateVariableSeed
                {
                    Name = "expiresat",
                    Format = TemplateVariable.DateFormat,
                    Sample = "26 September"
                }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "You have been asked to take over {communityname}",
                    Body = "The owner of this community has asked you to take it on. You can "
                        + "accept or turn it down, and the offer runs out on {expiresat}.",
                    ShortBody = "You have been asked to take over {communityname}."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.CommunityOwnershipAccepted,
            CategoryKey = NotificationCategoryKeys.Community,

            // High. Who runs a community has changed, and the person told is the one it changed
            // for. They should not learn it from a screen the next time they open one.
            Priority = NotificationPriority.High,
            RouteTemplate = "/community/{communityslug}",
            Description = "The person asked accepted, and the community changed hands.",
            Variables =
            [
                new TemplateVariableSeed
                {
                    Name = "communityname", Sample = "Nigerians in Leeds"
                },
                new TemplateVariableSeed
                {
                    Name = "communityslug", Sample = "nigerians-in-leeds"
                }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "{communityname} has a new owner",
                    Body = "The person you asked has accepted, and this community is now theirs. "
                        + "You are still one of its administrators.",
                    ShortBody = "{communityname} has a new owner."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.CommunityOwnershipDeclined,
            CategoryKey = NotificationCategoryKeys.Community,
            Priority = NotificationPriority.Normal,
            RouteTemplate = "/community/{communityslug}/ownership",
            Description = "The person asked turned the community down.",
            Variables =
            [
                new TemplateVariableSeed
                {
                    Name = "communityname", Sample = "Nigerians in Leeds"
                },
                new TemplateVariableSeed
                {
                    Name = "communityslug", Sample = "nigerians-in-leeds"
                },

                // Their own words, quoted. Optional, because somebody may simply decline.
                new TemplateVariableSeed
                {
                    Name = "reason",
                    IsRequired = false,
                    Sample = "I am moving away next month."
                }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Your offer to hand over {communityname} was turned down",
                    Body = "The person you asked has turned the offer down. {reason} You are still "
                        + "the owner, and you can ask somebody else who helps run this community.",
                    ShortBody = "Your offer to hand over {communityname} was turned down."
                }
            ]
        };
    }
}
