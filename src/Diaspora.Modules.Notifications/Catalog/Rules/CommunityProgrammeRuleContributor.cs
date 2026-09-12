using Diaspora.Contracts.CommunityProgramme.V1.Events;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Extensibility.Routing;

namespace Diaspora.Modules.Notifications.Catalog.Rules;

/// <summary>
/// Which of B18's facts become messages, to whom, and how loudly.
/// <para>
/// <b>Reference data rather than code, which is the point of the seam.</b> A deployment that decides a
/// renewal reminder should be email as well as push changes a row; it does not change a consumer, and
/// it does not need a release.
/// </para>
/// <para>
/// <b>The membership event carries four rules on one contract, keyed by audience.</b> B18 publishes
/// one <c>ProgrammeMembershipStatusChanged</c> for four facts, and the consumer picks an audience
/// name from the status — so an approval reaches the member at high priority and a rejection reaches
/// them at normal, from one payload. Four contracts would have made B18's relay four times the size
/// for the same result.
/// </para>
/// </summary>
public sealed class CommunityProgrammeRuleContributor : INotificationRuleContributor
{
    private static readonly NotificationChannelKind[] InAppAndPush =
        [NotificationChannelKind.InApp, NotificationChannelKind.Push];

    private static readonly NotificationChannelKind[] InAppOnly =
        [NotificationChannelKind.InApp];

    /// <summary>
    /// In-app and email, and deliberately no push.
    /// <para>
    /// The six closure and hand-over rules below are the only ones in this file that send mail. Each
    /// of them is a decision about who owns a community or whether it survives — the kind of thing a
    /// member forwards, keeps, or comes back to in a month to check what they were told. A push
    /// banner is read once and gone, and none of these fits in one.
    /// </para>
    /// </summary>
    private static readonly NotificationChannelKind[] InAppAndEmail =
        [NotificationChannelKind.InApp, NotificationChannelKind.Email];

    public string Module => NotificationsModuleMetadata.Name;

    public IEnumerable<NotificationRuleSeed> GetRules()
    {
        var membershipEvent =
            typeof(ProgrammeMembershipStatusChangedIntegrationEvent).FullName!;

        yield return new NotificationRuleSeed
        {
            Key = "communities.membership.approved",
            EventName = membershipEvent,
            Audience = "membership-approved",
            TemplateKey = NotificationTemplateKeys.ProgrammeMembershipApproved,
            CategoryKey = NotificationCategoryKeys.Organizations,
            Priority = NotificationPriority.High,
            Channels = InAppAndPush
        };

        yield return new NotificationRuleSeed
        {
            Key = "communities.membership.rejected",
            EventName = membershipEvent,
            Audience = "membership-rejected",
            TemplateKey = NotificationTemplateKeys.ProgrammeMembershipRejected,
            CategoryKey = NotificationCategoryKeys.Organizations,

            // In-app only. A refusal is somebody's answer to a request, and it does not need to
            // arrive on a lock screen.
            Priority = NotificationPriority.Normal,
            Channels = InAppOnly
        };

        yield return new NotificationRuleSeed
        {
            Key = "communities.membership.renewal-due",
            EventName = membershipEvent,
            Audience = "membership-renewal-due",
            TemplateKey = NotificationTemplateKeys.ProgrammeMembershipRenewalDue,
            CategoryKey = NotificationCategoryKeys.Organizations,
            Priority = NotificationPriority.Normal,
            Channels = InAppAndPush,

            // A day. The lifecycle sweep runs every fifteen minutes and moves a membership to
            // RenewalDue exactly once — but a sweep that was down and catches up, or a membership
            // reinstated and re-expiring, would otherwise tell the member twice in an hour.
            DeduplicationWindow = TimeSpan.FromDays(1)
        };

        yield return new NotificationRuleSeed
        {
            Key = "communities.membership.expired",
            EventName = membershipEvent,
            Audience = "membership-expired",
            TemplateKey = NotificationTemplateKeys.ProgrammeMembershipExpired,
            CategoryKey = NotificationCategoryKeys.Organizations,
            Priority = NotificationPriority.Normal,
            Channels = InAppAndPush,
            DeduplicationWindow = TimeSpan.FromDays(1)
        };

        yield return new NotificationRuleSeed
        {
            Key = "communities.volunteer.application.decided",
            EventName = typeof(VolunteerApplicationDecidedIntegrationEvent).FullName!,
            Audience = "volunteer",
            TemplateKey = NotificationTemplateKeys.VolunteerApplicationDecided,
            CategoryKey = NotificationCategoryKeys.Community,
            Priority = NotificationPriority.Normal,
            Channels = InAppAndPush
        };

        yield return new NotificationRuleSeed
        {
            Key = "communities.volunteer.assignment.created",
            EventName = typeof(VolunteerAssignmentCreatedIntegrationEvent).FullName!,
            Audience = "volunteer",
            TemplateKey = NotificationTemplateKeys.VolunteerAssignmentCreated,
            CategoryKey = NotificationCategoryKeys.Community,

            // High: it names a time and a place somebody has agreed to be at, and a volunteer who
            // does not see it does not turn up.
            Priority = NotificationPriority.High,
            Channels = InAppAndPush
        };

        yield return new NotificationRuleSeed
        {
            Key = "communities.volunteer.hours.confirmed",
            EventName = typeof(VolunteerHoursConfirmedIntegrationEvent).FullName!,
            Audience = "volunteer",
            TemplateKey = NotificationTemplateKeys.VolunteerHoursConfirmed,
            CategoryKey = NotificationCategoryKeys.Community,

            // In-app only, low priority. A receipt for work already done, delivered to somebody who
            // was there — the same call B13 makes for an event check-in.
            Priority = NotificationPriority.Low,
            Channels = InAppOnly
        };

        // ── Closing a community, and handing one over ────────────────────────────────────────
        //
        // Two contracts carry two outcomes each, and the consumer picks the audience from the
        // outcome — the same shape as the membership rules above, and for the same reason. A
        // refusal and an approval are not the same message at a different volume; they say
        // different things, to somebody in a different position, and one of them has a deadline.

        var deletionDecidedEvent =
            typeof(CommunityDeletionDecidedIntegrationEvent).FullName!;

        var ownershipDecidedEvent =
            typeof(CommunityOwnershipDecidedIntegrationEvent).FullName!;

        yield return new NotificationRuleSeed
        {
            Key = "communities.closure.approved",
            EventName = deletionDecidedEvent,
            Audience = "closure-approved",
            TemplateKey = NotificationTemplateKeys.CommunityClosureApproved,
            CategoryKey = NotificationCategoryKeys.Community,
            Priority = NotificationPriority.High,
            Channels = InAppAndEmail
        };

        yield return new NotificationRuleSeed
        {
            Key = "communities.closure.refused",
            EventName = deletionDecidedEvent,
            Audience = "closure-refused",
            TemplateKey = NotificationTemplateKeys.CommunityClosureRefused,
            CategoryKey = NotificationCategoryKeys.Community,
            Priority = NotificationPriority.Normal,

            // Email on a refusal too. The reason is the message, and an owner who was told no is
            // owed a copy of it they can read again rather than a card they swipe away.
            Channels = InAppAndEmail
        };

        yield return new NotificationRuleSeed
        {
            Key = "communities.closed",
            EventName = typeof(CommunityDeletedIntegrationEvent).FullName!,
            Audience = "owner",
            TemplateKey = NotificationTemplateKeys.CommunityClosed,
            CategoryKey = NotificationCategoryKeys.Community,
            Priority = NotificationPriority.Normal,
            Channels = InAppAndEmail,

            // A day, on the same reasoning as the membership sweeps: this is raised by the sweep
            // that closes a community when its wait runs out, and a sweep that was down and catches
            // up must not tell somebody twice that the thing they built is gone.
            DeduplicationWindow = TimeSpan.FromDays(1)
        };

        yield return new NotificationRuleSeed
        {
            Key = "communities.ownership.offered",
            EventName = typeof(CommunityOwnershipOfferedIntegrationEvent).FullName!,

            // Not "owner": this one goes to the person being asked, who is a moderator or an
            // administrator today and may be the owner tomorrow.
            Audience = "successor",
            TemplateKey = NotificationTemplateKeys.CommunityOwnershipOffered,
            CategoryKey = NotificationCategoryKeys.Community,
            Priority = NotificationPriority.High,
            Channels = InAppAndEmail
        };

        yield return new NotificationRuleSeed
        {
            Key = "communities.ownership.accepted",
            EventName = ownershipDecidedEvent,
            Audience = "ownership-accepted",
            TemplateKey = NotificationTemplateKeys.CommunityOwnershipAccepted,
            CategoryKey = NotificationCategoryKeys.Community,
            Priority = NotificationPriority.High,

            // Email, because it changes who runs the community. The member who was the owner an
            // hour ago should have a record of when that stopped being true.
            Channels = InAppAndEmail
        };

        yield return new NotificationRuleSeed
        {
            Key = "communities.ownership.declined",
            EventName = ownershipDecidedEvent,
            Audience = "ownership-declined",
            TemplateKey = NotificationTemplateKeys.CommunityOwnershipDeclined,
            CategoryKey = NotificationCategoryKeys.Community,
            Priority = NotificationPriority.Normal,

            // In-app only. Nothing changed — the owner is still the owner — and a no does not need
            // to follow somebody into their inbox.
            Channels = InAppOnly
        };
    }
}
