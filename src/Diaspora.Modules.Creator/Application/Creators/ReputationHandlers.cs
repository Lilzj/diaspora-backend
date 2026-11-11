using System.Globalization;
using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Community.V1.Follows;
using Diaspora.Contracts.Guides.V1.Authors;
using Diaspora.Contracts.Trust.V1.Reputation;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Application.Mapping;
using Diaspora.Modules.Creator.Application.Services;
using Diaspora.Modules.Creator.Contracts.V1.Responses;
using Diaspora.Modules.Creator.Domain.Creators;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Errors;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Modules.Creator.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Creator.Application.Creators;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// A creator's reputation — added 21 Oct 2026 to replace the app's static `ALL_CREATOR_BADGES` and
// the simulated `/creators/me/reputation`.
//
// It lives here rather than in Trust because it is a projection over a creator, and Creator
// composes after every module it reads (Trust 200, Community 500, Guides 700, Creator 1700). Each
// read goes through a Diaspora.Contracts seam the owning module implements — IMemberReputationReader,
// IMemberFollowerDirectory, IAuthorGuideStatsReader — so this module references none of them.
//
// Nothing is estimated. A figure the platform does not hold goes in `unavailable` with the reason.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// The caller's own creator reputation.
/// <para>
/// <b>Opens the caller's creator record on first read, as <c>GET /creators/me</c> does</b>, so the
/// identifier served is a real one. Answering a new member with a transient record would serve a
/// creator id that resolves to nothing.
/// </para>
/// </summary>
[RequirePermission(CreatorPermissions.ProfileRead)]
[SideEffecting(Reason =
    "Opens the caller's creator record on first read, exactly as GET /creators/me does, so the "
    + "reputation it answers carries a creator identifier that exists.")]
public sealed record GetMyCreatorReputationQuery : IQuery<CreatorReputationResponse>;

public sealed class GetMyCreatorReputationQueryHandler
    : IQueryHandler<GetMyCreatorReputationQuery, CreatorReputationResponse>
{
    private readonly CreatorContext _context;
    private readonly CreatorReputationComposer _composer;
    private readonly IUnitOfWork _unitOfWork;

    public GetMyCreatorReputationQueryHandler(
        CreatorContext context, CreatorReputationComposer composer, IUnitOfWork unitOfWork)
    {
        _context = context;
        _composer = composer;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CreatorReputationResponse>> Handle(
        GetMyCreatorReputationQuery query, CancellationToken cancellationToken)
    {
        var creator = await _context.RequireCreatorAsync(cancellationToken);

        if (creator.IsFailure)
        {
            return Result.Failure<CreatorReputationResponse>(creator.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            await _composer.ComposeAsync(creator.Value, isSelf: true, cancellationToken));
    }
}

/// <summary>
/// Any creator's public reputation.
/// <para>
/// <b>The visibility rule is <c>GET /creators/{creatorId}</c>'s</b>: a creator who opted out of the
/// directory, or is suspended or restricted, is a 404 to everybody but themselves — the same error
/// a missing creator produces.
/// </para>
/// <para>
/// <b>A public read withholds two things, and says so in <c>unavailable</c>.</b> The trust dimension
/// carries moderation outcomes, which Trust's own public profile never shows; and the combined score
/// and points-to-next-tier would let anybody subtract their way back to it. The level, the other two
/// dimensions, the earned badges and the counts are public.
/// </para>
/// </summary>
[RequirePermission(CreatorPermissions.ProfileRead)]
public sealed record GetCreatorReputationQuery : IQuery<CreatorReputationResponse>
{
    public required Guid CreatorId { get; init; }
}

public sealed class GetCreatorReputationQueryHandler
    : IQueryHandler<GetCreatorReputationQuery, CreatorReputationResponse>
{
    private readonly ICreatorProfileRepository _creators;
    private readonly CreatorContext _context;
    private readonly CreatorReputationComposer _composer;

    public GetCreatorReputationQueryHandler(
        ICreatorProfileRepository creators,
        CreatorContext context,
        CreatorReputationComposer composer)
    {
        _creators = creators;
        _context = context;
        _composer = composer;
    }

    public async Task<Result<CreatorReputationResponse>> Handle(
        GetCreatorReputationQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.CreatorId == Guid.Empty)
        {
            return Result.Failure<CreatorReputationResponse>(CreatorErrors.CreatorNotFound);
        }

        var creator = await _creators.GetAsync(CreatorId.From(query.CreatorId), cancellationToken);

        if (creator is null)
        {
            return Result.Failure<CreatorReputationResponse>(CreatorErrors.CreatorNotFound);
        }

        var caller = _context.RequireUser();
        var isSelf = caller.IsSuccess && caller.Value == creator.UserId;

        var visible = isSelf
            || (creator.IsDiscoverable && !creator.IsSuspended && !creator.IsRestricted);

        if (!visible)
        {
            return Result.Failure<CreatorReputationResponse>(CreatorErrors.CreatorNotFound);
        }

        return Result.Success(await _composer.ComposeAsync(creator, isSelf, cancellationToken));
    }
}

/// <summary>
/// Composes the reputation projection from the three modules that hold its parts.
/// <para>
/// Extracted because both handlers need it and DSP1001 forbids one handler reaching another.
/// </para>
/// </summary>
public sealed class CreatorReputationComposer
{
    /// <summary>The levels' display names. A level Trust adds later is served under its own name.</summary>
    private static readonly Dictionary<string, string> TierLabels =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Member"] = "Member",
            ["VerifiedResident"] = "Verified resident",
            ["TrustedContributor"] = "Trusted contributor",
            ["CommunityExpert"] = "Community expert",
            ["CommunityAmbassador"] = "Community ambassador"
        };

    private readonly IMemberReputationReader _reputation;
    private readonly IMemberFollowerDirectory _followers;
    private readonly IAuthorGuideStatsReader _guides;
    private readonly ICreatorMilestoneRepository _milestones;
    private readonly ICreatorMilestoneProgressRepository _progress;
    private readonly ICreatorCertificateRepository _certificates;

    public CreatorReputationComposer(
        IMemberReputationReader reputation,
        IMemberFollowerDirectory followers,
        IAuthorGuideStatsReader guides,
        ICreatorMilestoneRepository milestones,
        ICreatorMilestoneProgressRepository progress,
        ICreatorCertificateRepository certificates)
    {
        _reputation = reputation;
        _followers = followers;
        _guides = guides;
        _milestones = milestones;
        _progress = progress;
        _certificates = certificates;
    }

    public async Task<CreatorReputationResponse> ComposeAsync(
        CreatorProfile creator, bool isSelf, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creator);

        var snapshot = await _reputation.GetAsync(creator.UserId, cancellationToken);
        var followers = await _followers.CountFollowersAsync(creator.UserId, cancellationToken);
        var guides = await _guides.GetAsync(creator.UserId, cancellationToken);
        var certificates = await _certificates.ListForCreatorAsync(creator.Id, cancellationToken);

        // A member with no reputation record is Trust's "Member on zero", served by Trust itself so
        // this module never restates a threshold.
        var figures = snapshot.Reputation;
        var level = figures.Level;
        var nextLevel = figures.NextLevel;

        var dimensions = new List<CreatorReputationDimensionResponse>
        {
            new()
            {
                Key = "contribution",
                Label = "Contribution",
                Score = figures.ContributionScore,
                Description = "Guides, updates and comments, as scored by the reputation ledger."
            },
            new()
            {
                Key = "verification",
                Label = "Verification",
                Score = figures.VerificationScore,
                Description = "Phone, email, identity and country verification."
            }
        };

        var unavailable = new List<CreatorReputationUnavailableResponse>
        {
            new()
            {
                Key = "reach",
                Label = "Reach beyond guides",
                Reason = "Views are recorded for guides only. How many people saw a creator's "
                    + "events, listings or posts is not measured, so no reach figure exists."
            }
        };

        if (isSelf)
        {
            dimensions.Add(new CreatorReputationDimensionResponse
            {
                Key = "trust",
                Label = "Trust",
                Score = figures.TrustScore,
                Description = "Helpful votes and community confirmations, less any violations."
            });
        }
        else
        {
            unavailable.Add(new CreatorReputationUnavailableResponse
            {
                Key = "trust",
                Label = "Trust",
                Reason = "Includes moderation outcomes, which are shown only to the creator."
            });

            unavailable.Add(new CreatorReputationUnavailableResponse
            {
                Key = "score",
                Label = "Combined score",
                Reason = "Withheld on a public read: with the other two dimensions it would reveal "
                    + "the trust dimension."
            });
        }

        var signals = new List<CreatorReputationSignalResponse>
        {
            new()
            {
                Key = "followers",
                Label = "Followers",
                Value = followers,
                Source = "community",
                Description = "Members following this creator."
            },
            new()
            {
                Key = "guides_published",
                Label = "Guides published",
                Value = guides.PublishedGuides,
                Source = "guides",
                Description = "Guides live now."
            },
            new()
            {
                Key = "guide_views",
                Label = "Guide views",
                Value = guides.Views,
                Source = "guides",
                Description = "Every recorded open of the creator's published guides."
            },
            new()
            {
                Key = "guide_unique_views",
                Label = "Guide readers",
                Value = guides.UniqueViews,
                Source = "guides",
                Description = "Opens the app reported as a reader's first. An approximation."
            },
            new()
            {
                Key = "helpful_votes",
                Label = "Helpful votes",
                Value = guides.HelpfulVotes,
                Source = "guides",
                Description = "Readers who marked a published guide helpful."
            },
            new()
            {
                Key = "guide_bookmarks",
                Label = "Guide bookmarks",
                Value = guides.Bookmarks,
                Source = "guides"
            },
            new()
            {
                Key = "guides_verified",
                Label = "Guides verified",
                Value = figures.GuidesApproved,
                Source = "trust",
                Description = "Guide verifications the reputation ledger has scored."
            },
            new()
            {
                Key = "community_confirmations",
                Label = "Community confirmations",
                Value = figures.CommunityConfirmationsReceived,
                Source = "trust"
            },
            new()
            {
                Key = "content_published",
                Label = "Contributions published",
                Value = creator.TotalContentPublished,
                Source = "creator",
                Description = "Everything the creator programme counts as a publication."
            },
            new()
            {
                Key = "academy_certificates",
                Label = "Academy certificates",
                Value = certificates.Count,
                Source = "creator"
            }
        };

        var badges = new List<CreatorReputationBadgeResponse>();

        badges.AddRange(snapshot.Badges.Select(award => new CreatorReputationBadgeResponse
        {
            BadgeId = string.IsNullOrWhiteSpace(award.CountryCode)
                ? $"trust:{award.Type}"
                : $"trust:{award.Type}:{award.CountryCode}",
            Key = award.Type,
            Label = award.Label,
            IsEarned = true,
            EarnedAt = award.AwardedAt,
            Source = "trust",
            CountryCode = award.CountryCode
        }));

        // The creator's own read adds what is still to earn, and how; a public read does not, for
        // the reason the milestone badges below give.
        if (isSelf)
        {
            badges.AddRange(snapshot.Unearned.Select(goal => new CreatorReputationBadgeResponse
            {
                BadgeId = $"trust:{goal.Type}",
                Key = goal.Type,
                Label = goal.Label,
                Description = goal.HowToEarn,
                IsEarned = false,
                Source = "trust"
            }));
        }

        badges.AddRange(await MilestoneBadgesAsync(creator.Id, isSelf, cancellationToken));

        return new CreatorReputationResponse
        {
            CreatorId = creator.Id.Value,
            UserId = creator.UserId,
            IsSelf = isSelf,
            Score = isSelf ? figures.CombinedScore : null,
            Tier = level,
            TierLabel = LabelFor(level),
            Dimensions = dimensions,
            Badges = badges,
            PointsToNextTier = isSelf ? figures.PointsToNextLevel : null,
            NextTier = nextLevel,
            NextTierLabel = nextLevel is null ? null : LabelFor(nextLevel),
            Signals = signals,
            Unavailable = unavailable,
            UpdatedAt = figures.HasRecord ? figures.LastCalculatedAt : creator.UpdatedAt
        };
    }

    private static string LabelFor(string level) =>
        TierLabels.TryGetValue(level, out var label) ? label : level;

    /// <summary>
    /// Badges from the milestone catalogue: a milestone whose reward is a badge.
    /// <para>
    /// The rule <c>CreatorBadgeReader</c> applies to the public profile — earned means claimed, and
    /// only active definitions count — so the two never disagree about what was earned. The
    /// creator's own read adds the unearned ones with their progress; a public read does not, since
    /// a list of what somebody has not done is theirs to look at.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<CreatorReputationBadgeResponse>> MilestoneBadgesAsync(
        CreatorId creatorId, bool isSelf, CancellationToken cancellationToken)
    {
        var definitions = (await _milestones.ListActiveAsync(cancellationToken))
            .Where(definition => !string.IsNullOrWhiteSpace(definition.RewardBadgeKey))
            .OrderBy(definition => definition.SortOrder)
            .ToList();

        if (definitions.Count == 0)
        {
            return [];
        }

        var rows = (await _progress.ListForCreatorAsync(creatorId, cancellationToken))
            .GroupBy(row => row.MilestoneId)
            .ToDictionary(group => group.Key, group => group.First());

        var result = new List<CreatorReputationBadgeResponse>();

        foreach (var definition in definitions)
        {
            rows.TryGetValue(definition.Id, out var row);

            var earned = row?.State == CreatorMilestoneState.Claimed;

            if (!earned && !isSelf)
            {
                continue;
            }

            var progress = row?.Progress(definition.Unit)
                ?? CreatorProgress.Of(0, definition.Target, definition.Unit);

            result.Add(new CreatorReputationBadgeResponse
            {
                BadgeId = definition.Id.Value.ToString("D", CultureInfo.InvariantCulture),
                Key = definition.RewardBadgeKey!,
                Label = definition.Title,
                Description = definition.Description,
                Icon = definition.Icon,
                IsEarned = earned,
                EarnedAt = earned ? row!.ClaimedAt : null,
                Progress = earned ? null : progress.ToResponse(),
                Source = "milestone"
            });
        }

        return result;
    }
}
