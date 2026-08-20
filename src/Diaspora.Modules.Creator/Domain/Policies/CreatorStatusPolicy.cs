using Diaspora.Modules.Creator.Domain.Creators;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.ValueObjects;

namespace Diaspora.Modules.Creator.Domain.Policies;

/// <summary>
/// The rule set that moves a member up the creator ladder.
/// <para>
/// <b>This class is CREATOR-GAP-001's answer.</b> The register: "A documented rule set for what
/// moves a member between statuses, <b>served as <c>nextStatus.requirements</c></b> rather than
/// implemented in the client." Both halves are here — <see cref="Resolve"/> decides, and
/// <see cref="NextTarget"/> renders the same thresholds as sentences the app shows without
/// holding.
/// </para>
/// <para>
/// <b>Overrides are resolved before rungs, and the order is the design.</b> A suspended Top Creator
/// is Suspended. A restricted creator is Restricted. Only then is the earned rung considered, and
/// the earned rung is persisted separately so that lifting a suspension restores what somebody
/// worked for rather than starting them again at the bottom.
/// </para>
/// <para>
/// <b><see cref="CreatorStatus.PendingVerification"/> only ever replaces a rung below
/// <see cref="CreatorStatus.VerifiedCreator"/>.</b> An Established Creator whose annual
/// re-verification is with a reviewer keeps their standing while it is looked at; showing them as
/// "pending" would be a demotion for doing the right thing on time.
/// </para>
/// <para>
/// <b>The thresholds are constants in this file and nowhere else.</b> Not configuration, because a
/// deployment that quietly moved the bar would make one member's Established Creator a different
/// achievement from another's; not a database table, because then the rule that decides standing
/// would be editable without a release. Changing them is a code change with a version bump on
/// <see cref="Version"/>, which is what the client reads to know the ladder it was told about is
/// the one being applied.
/// </para>
/// </summary>
public static class CreatorStatusPolicy
{
    /// <summary>
    /// The ladder's version. Bumped whenever a threshold below changes, and served on the profile
    /// so a client can tell a changed rule from a changed member.
    /// </summary>
    public const string Version = "creator-status/1";

    // ── Thresholds ──────────────────────────────────────────────────────────────────────────

    private const long ContributorActions = 1;

    private const long EmergingPublications = 5;
    private const long EmergingFollowers = 25;

    private const long EstablishedPublications = 25;
    private const long EstablishedFollowers = 250;

    private const long TopPublications = 100;
    private const long TopFollowers = 1_000;
    private const long TopWeightedScore = 2_500;

    /// <summary>
    /// What the platform calls this member, and what their work earned them.
    /// </summary>
    /// <returns>
    /// <c>Earned</c> is the rung; <c>Served</c> is what the profile shows, which is the rung unless
    /// the platform is saying something else about the account.
    /// </returns>
    public static (CreatorStatus Earned, CreatorStatus Served) Resolve(CreatorProfile creator)
    {
        ArgumentNullException.ThrowIfNull(creator);

        var earned = EarnedRung(creator);

        if (creator.IsSuspended)
        {
            return (earned, CreatorStatus.Suspended);
        }

        if (creator.IsRestricted)
        {
            return (earned, CreatorStatus.Restricted);
        }

        if (creator.HasVerificationInReview && earned < CreatorStatus.VerifiedCreator)
        {
            return (earned, CreatorStatus.PendingVerification);
        }

        return (earned, earned);
    }

    /// <summary>
    /// The rung this creator's work has bought, ignoring standing entirely.
    /// <para>
    /// Ordered highest first, so a creator who satisfies Top is Top rather than Emerging.
    /// </para>
    /// </summary>
    private static CreatorStatus EarnedRung(CreatorProfile creator)
    {
        if (creator.IsVerifiedCreator
            && creator.TotalContentPublished >= TopPublications
            && creator.Followers >= TopFollowers
            && creator.WeightedContributionScore >= TopWeightedScore)
        {
            return CreatorStatus.TopCreator;
        }

        if (creator.IsVerifiedCreator
            && creator.TotalContentPublished >= EstablishedPublications
            && creator.Followers >= EstablishedFollowers)
        {
            return CreatorStatus.EstablishedCreator;
        }

        if (creator.IsVerifiedCreator)
        {
            return CreatorStatus.VerifiedCreator;
        }

        if (creator.TotalContentPublished >= EmergingPublications
            && creator.Followers >= EmergingFollowers)
        {
            return CreatorStatus.EmergingCreator;
        }

        return creator.RecognisedActions >= ContributorActions
            ? CreatorStatus.Contributor
            : CreatorStatus.NotACreator;
    }

    /// <summary>
    /// What the platform says has to happen to reach the next rung.
    /// <para>
    /// <b>Served in words with the counters attached, so the app can explain progression without
    /// embedding a single threshold.</b> Null at the top of the ladder, and null for a suspended
    /// account — telling somebody how to reach Established Creator while they are suspended would
    /// be answering a question they did not ask.
    /// </para>
    /// </summary>
    public static CreatorStatusTarget? NextTarget(CreatorProfile creator)
    {
        ArgumentNullException.ThrowIfNull(creator);

        if (creator.IsSuspended || creator.IsRestricted)
        {
            return null;
        }

        var earned = EarnedRung(creator);

        return earned switch
        {
            CreatorStatus.NotACreator => new CreatorStatusTarget(
                CreatorStatus.Contributor,
                "Contributor",
                [
                    CreatorRequirement.Counted(
                        "recognised-actions",
                        "Contribute something to the community",
                        creator.RecognisedActions,
                        ContributorActions,
                        "Publishing a guide, answering a question or hosting an event all count.",
                        "/knowledge/create",
                        "Write a guide")
                ]),

            CreatorStatus.Contributor => new CreatorStatusTarget(
                CreatorStatus.EmergingCreator,
                "Emerging Creator",
                [
                    CreatorRequirement.Counted(
                        "publications",
                        "Publish five pieces of content",
                        creator.TotalContentPublished,
                        EmergingPublications,
                        actionRoute: "/knowledge/create",
                        actionLabel: "Write a guide"),
                    CreatorRequirement.Counted(
                        "followers",
                        "Reach twenty-five followers",
                        creator.Followers,
                        EmergingFollowers)
                ]),

            CreatorStatus.EmergingCreator => new CreatorStatusTarget(
                CreatorStatus.VerifiedCreator,
                "Verified Creator",
                [
                    CreatorRequirement.Flag(
                        "creator-verification",
                        "Pass the creator programme verification",
                        creator.IsVerifiedCreator,
                        "A separate step from identity verification, which it requires.",
                        "/creator/verification",
                        "Apply")
                ]),

            CreatorStatus.VerifiedCreator => new CreatorStatusTarget(
                CreatorStatus.EstablishedCreator,
                "Established Creator",
                [
                    CreatorRequirement.Counted(
                        "publications",
                        "Publish twenty-five pieces of content",
                        creator.TotalContentPublished,
                        EstablishedPublications),
                    CreatorRequirement.Counted(
                        "followers",
                        "Reach two hundred and fifty followers",
                        creator.Followers,
                        EstablishedFollowers)
                ]),

            CreatorStatus.EstablishedCreator => new CreatorStatusTarget(
                CreatorStatus.TopCreator,
                "Top Creator",
                [
                    CreatorRequirement.Counted(
                        "publications",
                        "Publish one hundred pieces of content",
                        creator.TotalContentPublished,
                        TopPublications),
                    CreatorRequirement.Counted(
                        "followers",
                        "Reach a thousand followers",
                        creator.Followers,
                        TopFollowers),
                    CreatorRequirement.Counted(
                        "contribution-score",
                        "Build a contribution score of 2,500",
                        creator.WeightedContributionScore,
                        TopWeightedScore,
                        "Every recognised contribution adds to this, weighted by what it is.")
                ]),

            // TopCreator, and the three standing states, which NextTarget returned null for above.
            _ => null
        };
    }

    /// <summary>
    /// How complete the creator profile is, and what is missing.
    /// <para>
    /// Served rather than computed on the device so the app and a future web dashboard agree, which
    /// is the register's stated reason for putting the percentage on the wire.
    /// </para>
    /// </summary>
    public static (int Percentage, IReadOnlyList<CreatorRequirement> Missing) Completeness(
        CreatorProfile creator)
    {
        ArgumentNullException.ThrowIfNull(creator);

        var checks = new List<CreatorRequirement>
        {
            CreatorRequirement.Flag(
                "bio", "Write a short bio", !string.IsNullOrWhiteSpace(creator.Bio),
                actionRoute: "/creator/profile", actionLabel: "Edit profile"),
            CreatorRequirement.Flag(
                "categories", "Choose the areas you contribute in",
                creator.TagValues(CreatorTagKind.Category).Count > 0,
                actionRoute: "/creator/profile", actionLabel: "Edit profile"),
            CreatorRequirement.Flag(
                "expertise", "List your expertise",
                creator.TagValues(CreatorTagKind.Expertise).Count > 0,
                actionRoute: "/creator/profile", actionLabel: "Edit profile"),
            CreatorRequirement.Flag(
                "languages", "Say which languages you write in",
                creator.TagValues(CreatorTagKind.Language).Count > 0,
                actionRoute: "/creator/profile", actionLabel: "Edit profile"),
            CreatorRequirement.Flag(
                "cover-image", "Add a cover image",
                !string.IsNullOrWhiteSpace(creator.CoverImageUrl),
                actionRoute: "/creator/profile", actionLabel: "Edit profile"),
            CreatorRequirement.Flag(
                "first-contribution", "Publish something",
                creator.TotalContentPublished > 0,
                actionRoute: "/knowledge/create", actionLabel: "Write a guide")
        };

        var met = checks.Count(check => check.IsMet);
        var percentage = (int)Math.Round(met * 100d / checks.Count, MidpointRounding.AwayFromZero);

        return (percentage, checks.Where(check => !check.IsMet).ToList());
    }
}

/// <summary>What the platform says the next rung needs. Presented, never evaluated by a client.</summary>
public sealed record CreatorStatusTarget(
    CreatorStatus Status,
    string Label,
    IReadOnlyList<CreatorRequirement> Requirements);
