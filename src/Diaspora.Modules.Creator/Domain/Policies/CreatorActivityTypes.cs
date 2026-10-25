namespace Diaspora.Modules.Creator.Domain.Policies;

/// <summary>
/// The actions the creator programme recognises, as strings.
/// <para>
/// <b>Strings rather than an enum, because a milestone, a challenge task and a point rule are all
/// data.</b> §9 of the phase brief: "Do not hard-code challenge rules into handlers. Challenge
/// rules should be data-driven." A seeded challenge names an action type in a column; adding a
/// challenge that counts a new action is a seed change, and adding the action itself is a consumer
/// and a constant here rather than a new handler.
/// </para>
/// <para>
/// <b>Every one of these arrives as a published integration event from a module this one does not
/// reference.</b> That is what lets the creator programme count a guide without knowing B8 exists.
/// The constants are the vocabulary; <c>Application.Consumers</c> is where each event becomes an
/// entry in the recognised-activity ledger.
/// </para>
/// <para>
/// <b>None of these is an audience measurement.</b> There is no view, no impression, no reach and
/// no engagement rate — those are CREATOR-GAP-007's and belong to B19. A creator is recognised for
/// what they did, not for what happened to it.
/// </para>
/// </summary>
public static class CreatorActivityTypes
{
    /// <summary>Published a guide. B8's <c>GuidePublishedIntegrationEvent</c>.</summary>
    public const string GuidePublished = "guide.published";

    /// <summary>A guide of theirs was featured. B8's <c>GuideFeaturedIntegrationEvent</c>.</summary>
    public const string GuideFeatured = "guide.featured";

    /// <summary>Published a community post. B6's <c>CommunityPostPublishedIntegrationEvent</c>.</summary>
    public const string PostPublished = "post.published";

    /// <summary>Answered somebody. B6's <c>CommunityCommentAddedIntegrationEvent</c>.</summary>
    public const string CommentAdded = "comment.added";

    /// <summary>Published an event. B12's <c>EventPublishedIntegrationEvent</c>.</summary>
    public const string EventPublished = "event.published";

    /// <summary>Ran an event to completion. B12's <c>EventCompletedIntegrationEvent</c>.</summary>
    public const string EventHosted = "event.hosted";

    /// <summary>Registered as a provider and was published. B7.</summary>
    public const string ProviderPublished = "provider.published";

    /// <summary>Published a marketplace listing. B10.</summary>
    public const string ListingPublished = "listing.published";

    /// <summary>Published a job. B9.</summary>
    public const string JobPublished = "job.published";

    /// <summary>Published a housing listing. B11.</summary>
    public const string HousingPublished = "housing.published";

    /// <summary>Volunteered, and the hours were confirmed. B18.</summary>
    public const string VolunteerHoursConfirmed = "volunteer.hours-confirmed";

    /// <summary>A referral of theirs qualified. This module's own.</summary>
    public const string ReferralQualified = "referral.qualified";

    /// <summary>Finished an Academy course. This module's own.</summary>
    public const string CourseCompleted = "course.completed";

    /// <summary>Every recognised action, for validating seeded rules at startup.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        GuidePublished,
        GuideFeatured,
        PostPublished,
        CommentAdded,
        EventPublished,
        EventHosted,
        ProviderPublished,
        ListingPublished,
        JobPublished,
        HousingPublished,
        VolunteerHoursConfirmed,
        ReferralQualified,
        CourseCompleted
    ];

    /// <summary>
    /// The subset that counts as "publishing something", for the contribution counters a status
    /// and a leaderboard read.
    /// </summary>
    public static readonly IReadOnlyList<string> Publications =
    [
        GuidePublished,
        PostPublished,
        EventPublished,
        ProviderPublished,
        ListingPublished,
        JobPublished,
        HousingPublished
    ];

    /// <summary>
    /// Contributions to the community: everything except this programme's own records of a
    /// referral qualifying or a course finished. What a referral's qualification counts, because the
    /// rule is "their first contribution to the community" and neither of those is one.
    /// </summary>
    public static readonly IReadOnlyList<string> CommunityContributions =
        [.. All.Where(type => type is not (ReferralQualified or CourseCompleted))];

    public static bool IsKnown(string? actionType) =>
        actionType is not null && All.Contains(actionType, StringComparer.Ordinal);
}
