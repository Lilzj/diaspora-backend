using Diaspora.Modules.Analytics.Domain.Enums;

namespace Diaspora.Modules.Analytics.Domain.Policies;

/// <summary>
/// The journeys this platform can follow, step by step — FRONTEND-PHASE15-GAP-016.
/// <para>
/// <b>The client's five journeys, and only the steps something records.</b> The app names five
/// funnels (§32): job, marketplace, housing, provider and monetization. Each step here is an event
/// type the raw store actually holds for that kind of content. A step the app names and nothing
/// records — a shortlist, an offer, an impression — is listed as unavailable with its reason, never
/// filled in, and never skipped silently: a funnel that dropped a middle step without saying so would
/// show a conversion rate between two steps that are not adjacent.
/// </para>
/// </summary>
public static class AnalyticsFunnelCatalog
{
    private const string NotRecorded = "Nothing on this platform records this step yet.";
    private const string NoImpressions =
        "Being shown in a list or a search is not recorded yet, so the journey starts when somebody opens the item.";

    public static readonly IReadOnlyList<AnalyticsFunnelDefinition> All =
    [
        new(
            "job",
            "Job application funnel",
            AnalyticsContentType.Job,
            "employer",
            [
                new("view", "Viewed", AnalyticsEventTypes.ContentViewed, []),
                new("apply", "Applied", AnalyticsEventTypes.ContentConverted, ["view"])
            ],
            [
                new("shortlist", "Shortlisted", "Shortlisting is not recorded for analytics yet."),
                new("interview", "Interviewed", "Interviews are not recorded for analytics yet."),
                new("hire", "Hired", "Hires are not recorded for analytics yet.")
            ]),
        new(
            "marketplace",
            "Marketplace funnel",
            AnalyticsContentType.Marketplace,
            "seller",
            [
                new("view", "Viewed", AnalyticsEventTypes.ContentViewed, []),
                new("save", "Saved", AnalyticsEventTypes.ContentSaved, ["view"]),
                new("contact", "Contacted", AnalyticsEventTypes.ContentEnquired, ["view"]),
                new("purchase", "Bought", AnalyticsEventTypes.ContentConverted, ["view"])
            ],
            [
                new("impression", "Shown", NoImpressions),
                new("offer", "Offer made", "Marketplace has no offers to record."),
                new("reservation", "Reserved", "Marketplace has no reservations to record.")
            ]),
        new(
            "housing",
            "Housing enquiry funnel",
            AnalyticsContentType.Housing,
            "landlord",
            [
                new("view", "Viewed", AnalyticsEventTypes.ContentViewed, []),
                new("save", "Saved", AnalyticsEventTypes.ContentSaved, ["view"]),
                new("inquiry", "Enquired", AnalyticsEventTypes.ContentEnquired, ["view"]),
                new("viewing", "Viewing booked", AnalyticsEventTypes.ContentConverted, ["view"])
            ],
            [
                new("impression", "Shown", NoImpressions),
                new(
                    "application",
                    "Applied",
                    "Tenancy applications are not recorded; the last step counted is a booked viewing.")
            ]),
        new(
            "provider",
            "Provider discovery funnel",
            AnalyticsContentType.Provider,
            "provider",
            [
                new("profile", "Profile opened", AnalyticsEventTypes.ContentViewed, []),
                new("contact", "Contacted", AnalyticsEventTypes.ContentEnquired, ["profile"])
            ],
            [
                new("impression", "Shown", NoImpressions),
                new("lead", "Replied to", "Replies to an enquiry are not recorded for analytics yet."),
                new("conversion", "Converted", NotRecorded)
            ]),
        new(
            "monetization",
            "Placement funnel",
            AnalyticsContentType.Unknown,
            null,
            [],
            [
                new("impression", "Shown", "Placement delivery is not recorded in analytics."),
                new("click", "Opened", "Placement delivery is not recorded in analytics."),
                new("purchase", "Bought", NotRecorded),
                new("conversion", "Converted", NotRecorded)
            ])
    ];

    public static AnalyticsFunnelDefinition? Find(string? key) =>
        All.FirstOrDefault(funnel => string.Equals(funnel.Key, key?.Trim(), StringComparison.Ordinal));
}

/// <param name="Key">The client's funnel key.</param>
/// <param name="ContentType">The kind of content the journey is about; Unknown where it is about none.</param>
/// <param name="OwnerSubjectType">The role subject whose own items this journey covers, if any.</param>
/// <param name="Steps">The steps counted, in order.</param>
/// <param name="Unavailable">The steps the client names that nothing records.</param>
public sealed record AnalyticsFunnelDefinition(
    string Key,
    string Label,
    AnalyticsContentType ContentType,
    string? OwnerSubjectType,
    IReadOnlyList<AnalyticsFunnelStepDefinition> Steps,
    IReadOnlyList<AnalyticsFunnelUnavailableStep> Unavailable);

/// <param name="Requires">
/// The earlier steps a journey must also have reached to count here. Every step after the first
/// requires the first — the person opened the item — and nothing else, because the later steps are
/// not nested in one another: somebody can buy without saving, or book a viewing without writing.
/// </param>
public sealed record AnalyticsFunnelStepDefinition(
    string Key, string Label, string EventType, IReadOnlyList<string> Requires);

public sealed record AnalyticsFunnelUnavailableStep(string Key, string Label, string Reason);
