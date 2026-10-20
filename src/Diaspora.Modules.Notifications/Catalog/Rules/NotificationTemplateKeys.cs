namespace Diaspora.Modules.Notifications.Catalog.Rules;

/// <summary>
/// The template keys the platform ships with.
/// <para>
/// Constants so a rule and a template seed cannot disagree about a string. A rule naming a template
/// nobody published logs loudly at dispatch time and sends nothing — which is recoverable, and much
/// better caught here at compile time.
/// </para>
/// </summary>
public static class NotificationTemplateKeys
{
    public const string VerificationApproved = "trust.verification.approved";
    public const string VerificationRejected = "trust.verification.rejected";
    public const string VerificationExpired = "trust.verification.expired";
    public const string VerificationRevoked = "trust.verification.revoked";
    public const string TrustLevelChanged = "trust.level.changed";
    public const string BadgeAwarded = "trust.badge.awarded";

    public const string Welcome = "platform.welcome";
    public const string AccessStateChanged = "security.access.changed";

    public const string JobApplicationReceived = "jobs.application.received";

    /// <summary>The employer side of an application. Possible only since B13.1.</summary>
    public const string JobApplicationForEmployer = "jobs.application.new";

    public const string JobShortlisted = "jobs.application.shortlisted";
    public const string JobInterviewScheduled = "jobs.interview.scheduled";
    public const string JobInterviewReminder = "jobs.interview.reminder";
    public const string JobOfferExtended = "jobs.offer.extended";
    public const string JobOfferReminder = "jobs.offer.reminder";

    public const string MarketplaceOfferReceived = "marketplace.offer.received";
    public const string MarketplaceOfferAccepted = "marketplace.offer.accepted";
    public const string MarketplaceReservationConfirmed = "marketplace.reservation.confirmed";

    public const string HousingViewingScheduled = "housing.viewing.scheduled";
    public const string HousingViewingReminder = "housing.viewing.reminder";
    public const string HousingReservationConfirmed = "housing.reservation.confirmed";
    public const string HousingReservationCancelled = "housing.reservation.cancelled";
    public const string HousingLandlordVerified = "housing.landlord.verified";

    public const string EventRsvpConfirmed = "events.rsvp.confirmed";

    /// <summary>The organizer side of an RSVP. Possible only since B13.1.</summary>
    public const string EventRsvpForOrganizer = "events.rsvp.received";

    public const string EventCheckedIn = "events.checkin.recorded";
    public const string OrganizationMembershipApproved = "organizations.membership.approved";

    // ── Community Life, B13.3 ────────────────────────────────────────────
    //
    // The frontend's Phase 12 register named sixteen types the client already routes and tested.
    // These are the ones a published contract can actually drive: every other name on that list
    // describes a subject this platform does not have — a poll that closes, a fundraising campaign
    // with a milestone, a volunteer assignment — and a template for a fact nobody publishes is a
    // row in a table that never renders. What is missing and why is in the B13.3 report.

    /// <summary>The event is off. To everybody holding a live response.</summary>
    public const string EventCancelled = "events.cancelled";

    /// <summary>The event moved. To everybody holding a live response.</summary>
    public const string EventRescheduled = "events.rescheduled";

    /// <summary>Something material about the event changed — the place, the time, the name.</summary>
    public const string EventUpdated = "events.updated";

    /// <summary>The day before, scheduled against the start the RSVP contract now carries — B13.3.</summary>
    public const string EventReminder = "events.reminder";

    /// <summary>A membership lapsed because its renewal window passed.</summary>
    public const string OrganizationMembershipLapsed = "organizations.membership.lapsed";

    // ── Community Programme, B18 ─────────────────────────────────────────
    //
    // Six of the eight types B13.3 recorded as "waiting on a subject the platform does not have".
    // B18 supplies four of those subjects — a programme membership, a volunteer application, a
    // volunteer assignment and confirmed hours — and these are the templates that make the waiting
    // types real rather than nominal.
    //
    // **The four that still wait are the campaign's**: campaign.update, campaign.milestone,
    // poll.closing and poll.result. The first two need B15.1's campaign; the last two need a
    // scheduled reminder against a poll's closing moment, which is a sweep B18 deliberately did not
    // grow — a notification for a poll that has not closed yet is a different feature from a poll.
    // Both are recorded in the Wave 5 report rather than left as an absence somebody rediscovers.

    /// <summary>An organisation accepted a membership application.</summary>
    public const string ProgrammeMembershipApproved = "communities.membership.approved";

    /// <summary>An organisation refused one. Carries the organisation's own words.</summary>
    public const string ProgrammeMembershipRejected = "communities.membership.rejected";

    /// <summary>A membership entered its renewal window. Still grants benefits; still needs renewing.</summary>
    public const string ProgrammeMembershipRenewalDue = "communities.membership.renewal-due";

    /// <summary>A membership passed its expiry. The member may rejoin.</summary>
    public const string ProgrammeMembershipExpired = "communities.membership.expired";

    /// <summary>A volunteer application was approved, refused or waitlisted.</summary>
    public const string VolunteerApplicationDecided = "communities.volunteer.application.decided";

    /// <summary>An approved volunteer was placed on a shift.</summary>
    public const string VolunteerAssignmentCreated = "communities.volunteer.assignment.created";

    /// <summary>An organiser confirmed the hours worked. A receipt, and the credential behind it.</summary>
    public const string VolunteerHoursConfirmed = "communities.volunteer.hours.confirmed";

    // ── Closing a community, and handing one over ────────────────────────
    //
    // Two things an owner sets in motion and then stops watching a screen for: asking Diaspora to
    // close the community, and asking somebody who already helps run it to take it over. Both are
    // answered elsewhere — by an administrator reading a queue, or by the person who was asked —
    // hours or days later. Every key below exists because the person waiting is owed the answer
    // rather than left to find it.

    /// <summary>Diaspora agreed to close a community. Names the day it closes.</summary>
    public const string CommunityClosureApproved = "communities.closure.approved";

    /// <summary>Diaspora refused to. Carries the administrator's own words.</summary>
    public const string CommunityClosureRefused = "communities.closure.refused";

    /// <summary>The wait ran out on an approved request and the community is closed.</summary>
    public const string CommunityClosed = "communities.closed";

    /// <summary>Somebody was asked to take a community on. Names the day the offer lapses.</summary>
    public const string CommunityOwnershipOffered = "communities.ownership.offered";

    /// <summary>The person asked said yes. To the owner who asked.</summary>
    public const string CommunityOwnershipAccepted = "communities.ownership.accepted";

    /// <summary>The person asked said no. Carries their words, when they gave any.</summary>
    public const string CommunityOwnershipDeclined = "communities.ownership.declined";

    public const string CommunityMention = "community.mention";
    public const string CommunityReply = "community.reply";
    public const string CommunityComment = "community.comment";
    public const string CommunityReaction = "community.reaction";
    public const string CommunityModeration = "moderation.decision";

    public const string GuideFeatured = "guides.featured";

    /// <summary>A member you follow published a guide. 20 Oct 2026.</summary>
    public const string FollowedAuthorGuidePublished = "following.guide.published";
    public const string KnowledgeGuideFeatured = "learning.guide.featured";
    public const string LearningPathUpdated = "learning.path.updated";

    public const string OfficialNoticePublished = "intelligence.notice.published";

    public const string ProviderReviewPublished = "provider.review.published";
    public const string ProviderRecommendationPublished = "provider.recommendation.published";

    /// <summary>The owner side of both. Possible only since B13.1.</summary>
    public const string ProviderReviewReceived = "provider.review.received";

    public const string ProviderRecommendationReceived = "provider.recommendation.received";

    // ── Financial (B15), routed by PSR-1.1 ───────────────────────────────
    //
    // The `billing` category has existed since B13 and nothing routed to it until now. These are the
    // six contracts B15's own event catalogue named as "what B13 would most obviously act on", plus
    // the two placement contracts and the wallet freeze — every one of them a fact a member is
    // entitled to hear about their own money.

    public const string PaymentFailed = "billing.payment.failed";
    public const string RefundIssued = "billing.refund.issued";
    public const string SubscriptionActivated = "billing.subscription.activated";
    public const string SubscriptionEnded = "billing.subscription.ended";
    public const string PlacementActivated = "billing.placement.activated";
    public const string PlacementEnded = "billing.placement.ended";
    public const string DonationReceipt = "billing.donation.receipt";
    public const string ContributionReceipt = "billing.contribution.receipt";
    public const string WalletStatusChanged = "billing.wallet.status";
}
