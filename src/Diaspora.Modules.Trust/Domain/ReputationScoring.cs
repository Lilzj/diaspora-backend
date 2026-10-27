using Diaspora.SharedKernel;

namespace Diaspora.Modules.Trust.Domain;

/// <summary>
/// The action names reputation is scored against.
/// <para>
/// Constants rather than an enum because scoring rules are contributed by other modules —
/// a Marketplace module will want to score "SaleCompleted" without editing an enum in Trust.
/// A string keeps that an addition; an enum would make it an edit.
/// </para>
/// </summary>
public static class ReputationActions
{
    // Contribution
    public const string GuidePublished = "GuidePublished";
    public const string GuideApproved = "GuideApproved";
    public const string GuideUpdated = "GuideUpdated";
    public const string UpdatePublished = "UpdatePublished";
    public const string UpdateVerified = "UpdateVerified";
    public const string CommentWritten = "CommentWritten";
    public const string ReferralQualified = "ReferralQualified";
    public const string CourseCompleted = "CourseCompleted";
    public const string EventPublished = "EventPublished";
    public const string ProviderPublished = "ProviderPublished";
    public const string JobPublished = "JobPublished";
    public const string HousingPublished = "HousingPublished";
    public const string ListingPublished = "ListingPublished";
    public const string NeighborhoodInsightApproved = "NeighborhoodInsightApproved";
    public const string VolunteerHoursConfirmed = "VolunteerHoursConfirmed";
    public const string ProviderRecommendationReceived = "ProviderRecommendationReceived";

    // Verification
    public const string PhoneVerified = "PhoneVerified";
    public const string EmailVerified = "EmailVerified";
    public const string GovernmentIdVerified = "GovernmentIdVerified";
    public const string CountryVerified = "CountryVerified";
    public const string ProviderVerified = "ProviderVerified";
    public const string ProfilePhotoAdded = "ProfilePhotoAdded";
    /// <summary>Retired (D-11): no rule prices it. Kept so any event recorded under it still reads.</summary>
    public const string ProfileCompleted = "ProfileCompleted";

    // Trust
    public const string HelpfulVoteReceived = "HelpfulVoteReceived";
    public const string CommunityConfirmation = "CommunityConfirmation";
    public const string ReportResolved = "ReportResolved";
    public const string ViolationRecorded = "ViolationRecorded";
    public const string ContentRemoved = "ContentRemoved";
}

/// <summary>
/// A scoring rule: what an action is worth, on which dimension, and how often it may count.
/// <para>
/// Mirrors the frontend's <c>ScoringRule</c> type. The caps matter more than the points:
/// without <see cref="MaxOccurrences"/> and <see cref="CooldownHours"/>, writing a thousand
/// one-word comments is a valid path to Community Expert, and any reputation system that
/// permits that is decorative.
/// </para>
/// </summary>
public sealed record ReputationRule
{
    public required string ActionType { get; init; }

    public required ReputationDimension Dimension { get; init; }

    public required int Points { get; init; }

    /// <summary>How many times this can ever count. Null means uncapped.</summary>
    public int? MaxOccurrences { get; init; }

    /// <summary>Minimum gap between two awards of this action. Null means none.</summary>
    public int? CooldownHours { get; init; }

    /// <summary>
    /// How many awards of this action may count in any 24 hours. Null means no daily cap.
    /// <para>
    /// Where a cooldown is a gap after each award, this is a budget: a member who publishes two
    /// different guides ten minutes apart is paid for both, and the fourth that day for neither. It
    /// counts awards across every country, as the cooldown does, and a reversal does not hand a place
    /// back, so publishing and deleting is not a way past it.
    /// </para>
    /// </summary>
    public int? DailyCap { get; init; }

    /// <summary>Whether the points land on the country's standing as well as the global score.</summary>
    public bool IsCountryScoped { get; init; }

    public string? Description { get; init; }

    /// <summary>
    /// What a breakdown row for this action is called — "Guides published", "Policy violations".
    /// <para>
    /// A noun phrase that reads beside a count, where <see cref="Description"/> is a sentence about
    /// one occurrence. Optional so a contributed rule need not declare one; such a row is named
    /// after its action instead (see <see cref="Extensibility.ReputationRuleCatalog.LabelFor"/>).
    /// </para>
    /// </summary>
    public string? Label { get; init; }

    public bool IsActive { get; init; } = true;

    /// <summary>
    /// The reward points this action also pays, on top of its XP (D-2, D-12). Null pays none.
    /// <para>
    /// Points are paid only for confirmed outcomes: a guide that passed moderation, an update the
    /// community confirmed, a referral that qualified. Trust owns the schedule and publishes the grant;
    /// the Financial Platform holds the balance and decides when it vests. Publishing anything earns
    /// XP alone.
    /// </para>
    /// </summary>
    public int? CreditPoints { get; init; }

    /// <summary>
    /// Whether anything on the platform actually scores this rule yet.
    /// <para>
    /// A rule can be priced before any module produces it. That is fine for scoring, which simply
    /// never sees the action, and wrong for the rules a member is shown: "add a profile photo
    /// (+5 XP)" is a promise if nothing pays it. False keeps the rule out of
    /// <c>GET /trust/reputation/rules</c>. It has no effect on scoring. Whoever wires a producer
    /// for one of these flips it to true in the same change.
    /// </para>
    /// </summary>
    public bool IsAwarded { get; init; } = true;

    /// <summary>Turns this rule into the award applied to a profile.</summary>
    public ReputationAward ToAward() => new()
    {
        ActionType = ActionType,
        Dimension = Dimension,
        Points = Points,
        Description = Description,
        RuleSetVersion = DefaultReputationRules.Version
    };
}

/// <summary>One rule's effect, ready to apply to a profile.</summary>
public sealed record ReputationAward
{
    public required string ActionType { get; init; }

    public required ReputationDimension Dimension { get; init; }

    public required int Points { get; init; }

    public string? Description { get; init; }

    /// <summary>
    /// The version of the rules that priced it. Null for an award no rule priced, such as a
    /// handler's own approval award for an action with no rule.
    /// </summary>
    public int? RuleSetVersion { get; init; }

    public static ReputationAward Of(
        string actionType,
        ReputationDimension dimension,
        int points,
        string? description = null) => new()
        {
            ActionType = Guard.Against.NullOrWhiteSpace(actionType, nameof(actionType)),
            Dimension = dimension,
            Points = points,
            Description = description
        };
}

/// <summary>
/// The platform's default scoring rules.
/// <para>
/// Weighted so that verification alone reaches <c>VerifiedResident</c> (100) but not
/// <c>TrustedContributor</c> (350) — proving who you are earns standing, sustained
/// contribution earns influence, and the two are deliberately not interchangeable.
/// </para>
/// <para>
/// Additional rules are contributed through <c>IReputationRuleContributor</c>; these are
/// merely the set Trust ships with. A contributor may not override one of these — duplicate
/// action names fail at startup rather than silently letting one module re-price another
/// module's action.
/// </para>
/// </summary>
public static class DefaultReputationRules
{
    /// <summary>
    /// The version of these rules, stamped on every event they price (REP-16, D-10).
    /// <para>
    /// <b>Bump it with any change to a value</b>: points, credit points, a cap, a cooldown, a rule
    /// added or retired. The rules stay in code, a reviewed change like
    /// <see cref="ReputationThresholds"/>, and the version is what lets "why did this pay 25 and that
    /// one 50" be answered after the values move. Contributed rules are priced under the same number.
    /// </para>
    /// <list type="bullet">
    ///   <item>1, 2026-10-25: credit points for an approved guide (250) and a confirmed update (200).</item>
    ///   <item>2, 2026-10-25: a qualified referral, 25 XP and 250 credit points.</item>
    ///   <item>3, 2026-10-25: a finished Academy course, 10 XP (Q6).</item>
    ///   <item>4, 2026-10-25: "profile completed" retired (D-11).</item>
    ///   <item>5, 2026-10-25: XP for publishing an event, a service, a job, a home and a listing, for an
    ///   approved neighbourhood insight, confirmed volunteer hours and a provider recommendation
    ///   (REP-14, D-3).</item>
    ///   <item>6, 2026-10-26: an upheld report pays its reporter 5 trust XP, once an hour (REP-13).</item>
    ///   <item>7, 2026-10-27: a published guide is capped at three a day instead of one an hour, so a
    ///   second guide inside the hour is paid (REP-09). Still 50 XP.</item>
    /// </list>
    /// </summary>
    public const int Version = 7;

    public static IReadOnlyList<ReputationRule> All { get; } =
    [
        // ── Contribution ─────────────────────────────────────────────────
        new()
        {
            // Once per guide by its award key, and three guides a day (REP-09). An hourly cooldown
            // stood here and paid nothing for a second, different guide inside the hour.
            ActionType = ReputationActions.GuidePublished,
            Dimension = ReputationDimension.contribution,
            Points = 50,
            DailyCap = 3,
            IsCountryScoped = true,
            Description = "Published a community guide.",
            Label = "Guides published"
        },
        new()
        {
            ActionType = ReputationActions.GuideApproved,
            Dimension = ReputationDimension.contribution,
            Points = 25,
            CreditPoints = 250,
            IsCountryScoped = true,
            Description = "A guide passed moderation.",
            Label = "Guides approved"
        },
        new()
        {
            ActionType = ReputationActions.GuideUpdated,
            Dimension = ReputationDimension.contribution,
            Points = 10,
            CooldownHours = 24,
            IsCountryScoped = true,
            Description = "Kept a guide current.",
            Label = "Guides kept current"
        },
        new()
        {
            ActionType = ReputationActions.UpdatePublished,
            Dimension = ReputationDimension.contribution,
            Points = 10,
            CooldownHours = 1,
            IsCountryScoped = true,
            Description = "Published a community update.",
            Label = "Community updates published"
        },
        new()
        {
            ActionType = ReputationActions.UpdateVerified,
            Dimension = ReputationDimension.contribution,
            Points = 15,
            CreditPoints = 200,
            IsCountryScoped = true,
            Description = "An update was confirmed by the community.",
            Label = "Updates confirmed by the community",

            // PostVerifiedReputationConsumer, once per post, when the post becomes community-
            // verified. Each confirmation also pays CommunityConfirmation, once per confirmer: the
            // threshold and the confirmations are two facts.
        },
        new()
        {
            // Deliberately small and capped. Comments are worth something; they are not a
            // route to Expert.
            ActionType = ReputationActions.CommentWritten,
            Dimension = ReputationDimension.contribution,
            Points = 2,
            MaxOccurrences = 250,
            CooldownHours = 0,
            Description = "Wrote a comment.",
            Label = "Comments written"
        },
        new()
        {
            // ReferralQualifiedReputationConsumer, off Creator's qualification: the person invited
            // joined and made their first contribution. The referrer is paid; once per referral.
            ActionType = ReputationActions.ReferralQualified,
            Dimension = ReputationDimension.contribution,
            Points = 25,
            CreditPoints = 250,
            Description = "Someone you invited joined and contributed.",
            Label = "Referrals qualified"
        },
        new()
        {
            // CourseCompletedReputationConsumer, off Creator's completion. Once per course. A course
            // pays XP and no points (Q6); learning is not a confirmed outcome for anyone else.
            ActionType = ReputationActions.CourseCompleted,
            Dimension = ReputationDimension.contribution,
            Points = 10,
            Description = "Finished an Academy course.",
            Label = "Academy courses completed"
        },

        // ── REP-14: what the rest of the platform publishes, on today's scale (D-3) ──
        // Each pays once per entity by its award key. The ones with a cooldown are the ones that can
        // be published often. Three events name no country, so their rules are not country-scoped.
        new()
        {
            ActionType = ReputationActions.EventPublished,
            Dimension = ReputationDimension.contribution,
            Points = 10,
            CooldownHours = 1,
            IsCountryScoped = true,
            Description = "Published a community event.",
            Label = "Events published"
        },
        new()
        {
            ActionType = ReputationActions.ProviderPublished,
            Dimension = ReputationDimension.contribution,
            Points = 10,
            IsCountryScoped = true,
            Description = "Published a service.",
            Label = "Services published"
        },
        new()
        {
            ActionType = ReputationActions.JobPublished,
            Dimension = ReputationDimension.contribution,
            Points = 5,
            CooldownHours = 1,
            IsCountryScoped = true,
            Description = "Posted a job.",
            Label = "Jobs posted"
        },
        new()
        {
            ActionType = ReputationActions.HousingPublished,
            Dimension = ReputationDimension.contribution,
            Points = 5,
            CooldownHours = 1,
            IsCountryScoped = true,
            Description = "Listed a home.",
            Label = "Homes listed"
        },
        new()
        {
            ActionType = ReputationActions.ListingPublished,
            Dimension = ReputationDimension.contribution,
            Points = 5,
            CooldownHours = 1,
            IsCountryScoped = true,
            Description = "Listed something for sale.",
            Label = "Items listed for sale"
        },
        new()
        {
            ActionType = ReputationActions.NeighborhoodInsightApproved,
            Dimension = ReputationDimension.contribution,
            Points = 15,
            Description = "A neighbourhood insight was approved.",
            Label = "Neighbourhood insights approved"
        },
        new()
        {
            ActionType = ReputationActions.VolunteerHoursConfirmed,
            Dimension = ReputationDimension.contribution,
            Points = 15,
            Description = "An organizer confirmed your volunteer hours.",
            Label = "Volunteering confirmed"
        },

        // ── Verification ─────────────────────────────────────────────────
        new()
        {
            ActionType = ReputationActions.PhoneVerified,
            Dimension = ReputationDimension.verification,
            Points = 20,
            MaxOccurrences = 1,
            Description = "Linked and verified a phone number.",
            Label = "Phone verified"
        },
        new()
        {
            ActionType = ReputationActions.EmailVerified,
            Dimension = ReputationDimension.verification,
            Points = 10,
            MaxOccurrences = 1,
            Description = "Verified an email address.",
            Label = "Email verified"
        },
        new()
        {
            ActionType = ReputationActions.GovernmentIdVerified,
            Dimension = ReputationDimension.verification,
            Points = 40,
            MaxOccurrences = 1,
            Description = "Passed government ID and biometric checks.",
            Label = "Government ID verified"
        },
        new()
        {
            ActionType = ReputationActions.CountryVerified,
            Dimension = ReputationDimension.verification,
            Points = 30,
            IsCountryScoped = true,

            // Once per country, which is what a cap on a country-scoped rule now means.
            // Uncapped, this paid every time anything scored it: two producers for the same
            // approval paid thirty points twice, and ApplyAwardAsync said in a comment that
            // "verifying the same country twice should not pay twice" while nothing enforced it.
            MaxOccurrences = 1,
            Description = "Verified residency in a country community.",
            Label = "Countries verified"
        },
        new()
        {
            ActionType = ReputationActions.ProviderVerified,
            Dimension = ReputationDimension.verification,
            Points = 25,
            MaxOccurrences = 1,
            Description = "Verified as a professional or business.",
            Label = "Professional or business verified"
        },
        new()
        {
            ActionType = ReputationActions.ProfilePhotoAdded,
            Dimension = ReputationDimension.verification,
            Points = 5,
            MaxOccurrences = 1,
            Description = "Added a profile photo.",
            Label = "Profile photo added",

            // ProfilePhotoReputationConsumer, off Identity's UserAvatarChangedIntegrationEvent.
        },
        // ProfileCompleted is retired (D-11): nothing ever paid it, and the email and the photo it
        // would have rewarded already pay their own awards. The action and the profile flag stay,
        // so any history still reads.

        // ── Trust ────────────────────────────────────────────────────────
        new()
        {
            ActionType = ReputationActions.HelpfulVoteReceived,
            Dimension = ReputationDimension.trust,
            Points = 2,
            IsCountryScoped = true,
            Description = "Someone found your contribution helpful.",
            Label = "Helpful votes received"
        },
        new()
        {
            ActionType = ReputationActions.CommunityConfirmation,
            Dimension = ReputationDimension.trust,
            Points = 5,
            IsCountryScoped = true,
            Description = "The community confirmed your information.",
            Label = "Community confirmations"
        },
        new()
        {
            // Somebody vouched for a service; paid to its owner, never to the person recommending.
            ActionType = ReputationActions.ProviderRecommendationReceived,
            Dimension = ReputationDimension.trust,
            Points = 2,
            Description = "Someone recommended your service.",
            Label = "Recommendations received"
        },
        new()
        {
            // Paid by ReportUpheldReputationConsumer when any reporting module upholds a report
            // (REP-13). The cooldown keeps a burst of upheld reports from being a quick climb.
            ActionType = ReputationActions.ReportResolved,
            Dimension = ReputationDimension.trust,
            Points = 5,
            CooldownHours = 1,
            Description = "A report you raised was upheld.",
            Label = "Reports upheld"
        },
        new()
        {
            // The only large negative. A confirmed violation costs roughly a published guide
            // and a half — enough to matter, not enough to erase a good record in one strike.
            ActionType = ReputationActions.ViolationRecorded,
            Dimension = ReputationDimension.trust,
            Points = -75,
            Description = "A policy violation was confirmed.",
            Label = "Policy violations"
        },
        new()
        {
            ActionType = ReputationActions.ContentRemoved,
            Dimension = ReputationDimension.trust,
            Points = -25,
            Description = "Content was removed by moderation.",
            Label = "Content removed by moderation"
        }
    ];
}
