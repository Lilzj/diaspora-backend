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

    // Verification
    public const string PhoneVerified = "PhoneVerified";
    public const string EmailVerified = "EmailVerified";
    public const string GovernmentIdVerified = "GovernmentIdVerified";
    public const string CountryVerified = "CountryVerified";
    public const string ProviderVerified = "ProviderVerified";
    public const string ProfilePhotoAdded = "ProfilePhotoAdded";
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

    /// <summary>Whether the points land on the country's standing as well as the global score.</summary>
    public bool IsCountryScoped { get; init; }

    public string? Description { get; init; }

    public bool IsActive { get; init; } = true;

    /// <summary>Turns this rule into the award applied to a profile.</summary>
    public ReputationAward ToAward() => new()
    {
        ActionType = ActionType,
        Dimension = Dimension,
        Points = Points,
        Description = Description
    };
}

/// <summary>One rule's effect, ready to apply to a profile.</summary>
public sealed record ReputationAward
{
    public required string ActionType { get; init; }

    public required ReputationDimension Dimension { get; init; }

    public required int Points { get; init; }

    public string? Description { get; init; }

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
    public static IReadOnlyList<ReputationRule> All { get; } =
    [
        // ── Contribution ─────────────────────────────────────────────────
        new()
        {
            ActionType = ReputationActions.GuidePublished,
            Dimension = ReputationDimension.contribution,
            Points = 50,
            CooldownHours = 1,
            IsCountryScoped = true,
            Description = "Published a community guide."
        },
        new()
        {
            ActionType = ReputationActions.GuideApproved,
            Dimension = ReputationDimension.contribution,
            Points = 25,
            IsCountryScoped = true,
            Description = "A guide passed moderation."
        },
        new()
        {
            ActionType = ReputationActions.GuideUpdated,
            Dimension = ReputationDimension.contribution,
            Points = 10,
            CooldownHours = 24,
            IsCountryScoped = true,
            Description = "Kept a guide current."
        },
        new()
        {
            ActionType = ReputationActions.UpdatePublished,
            Dimension = ReputationDimension.contribution,
            Points = 10,
            CooldownHours = 1,
            IsCountryScoped = true,
            Description = "Published a community update."
        },
        new()
        {
            ActionType = ReputationActions.UpdateVerified,
            Dimension = ReputationDimension.contribution,
            Points = 15,
            IsCountryScoped = true,
            Description = "An update was confirmed by the community."
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
            Description = "Wrote a comment."
        },

        // ── Verification ─────────────────────────────────────────────────
        new()
        {
            ActionType = ReputationActions.PhoneVerified,
            Dimension = ReputationDimension.verification,
            Points = 20,
            MaxOccurrences = 1,
            Description = "Linked and verified a phone number."
        },
        new()
        {
            ActionType = ReputationActions.EmailVerified,
            Dimension = ReputationDimension.verification,
            Points = 10,
            MaxOccurrences = 1,
            Description = "Verified an email address."
        },
        new()
        {
            ActionType = ReputationActions.GovernmentIdVerified,
            Dimension = ReputationDimension.verification,
            Points = 40,
            MaxOccurrences = 1,
            Description = "Passed government ID and biometric checks."
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
            Description = "Verified residency in a country community."
        },
        new()
        {
            ActionType = ReputationActions.ProviderVerified,
            Dimension = ReputationDimension.verification,
            Points = 25,
            MaxOccurrences = 1,
            Description = "Verified as a professional or business."
        },
        new()
        {
            ActionType = ReputationActions.ProfilePhotoAdded,
            Dimension = ReputationDimension.verification,
            Points = 5,
            MaxOccurrences = 1,
            Description = "Added a profile photo."
        },
        new()
        {
            ActionType = ReputationActions.ProfileCompleted,
            Dimension = ReputationDimension.verification,
            Points = 10,
            MaxOccurrences = 1,
            Description = "Completed the profile."
        },

        // ── Trust ────────────────────────────────────────────────────────
        new()
        {
            ActionType = ReputationActions.HelpfulVoteReceived,
            Dimension = ReputationDimension.trust,
            Points = 2,
            IsCountryScoped = true,
            Description = "Someone found your contribution helpful."
        },
        new()
        {
            ActionType = ReputationActions.CommunityConfirmation,
            Dimension = ReputationDimension.trust,
            Points = 5,
            IsCountryScoped = true,
            Description = "The community confirmed your information."
        },
        new()
        {
            ActionType = ReputationActions.ReportResolved,
            Dimension = ReputationDimension.trust,
            Points = 5,
            Description = "A report you raised was upheld."
        },
        new()
        {
            // The only large negative. A confirmed violation costs roughly a published guide
            // and a half — enough to matter, not enough to erase a good record in one strike.
            ActionType = ReputationActions.ViolationRecorded,
            Dimension = ReputationDimension.trust,
            Points = -75,
            Description = "A policy violation was confirmed."
        },
        new()
        {
            ActionType = ReputationActions.ContentRemoved,
            Dimension = ReputationDimension.trust,
            Points = -25,
            Description = "Content was removed by moderation."
        }
    ];
}
