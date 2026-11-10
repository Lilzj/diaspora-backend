using Diaspora.Application.Abstractions;
using Diaspora.Modules.Creator.Application.Mapping;
using Diaspora.Modules.Creator.Application.Services;
using Diaspora.Modules.Creator.Contracts.V1.Responses;
using Diaspora.Modules.Creator.Domain.Creators;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Errors;
using Diaspora.Modules.Creator.Domain.Policies;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Creator.Application.Discovery;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// CREATOR-GAP-012 and -014 — the leaderboards and the creator directory.
//
// Both gaps turn on one rule the register states twice: visibility is the server's. "A client-side
// privacy filter is one refactor away from a privacy leak", and "a private creator must not affect
// public facet counts in a way that reveals their existence." The predicate lives in
// CreatorProfileRepository.VisibleTo and in CreatorActivityRepository.Ranked, and nothing outside
// those two methods decides who appears.
//
// The directory is deliberately not the contributor directory. B8's /knowledge surface is unchanged;
// this one knows about creator status, creator categories, programme verification and creator
// discoverability, which is exactly the list the register gives for why the two are different.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// What a leaderboard's score means, in the platform's own words.
/// <para>
/// Served with every ranking, because the register asks that "a rank is never a bare number" — and
/// a label alone ("Points") does not achieve that.
/// </para>
/// </summary>
public static class LeaderboardDefinitions
{
    public static (string Title, string ScoreLabel, string Definition) For(LeaderboardScope scope) =>
        scope switch
        {
            LeaderboardScope.TopCreators => (
                "Top creators",
                "Contribution score",
                "The weighted total of every recognised contribution by a verified creator in the "
                + "period. A guide counts more than a comment; nothing here counts views."),

            LeaderboardScope.TopContributors => (
                "Top contributors",
                "Contribution score",
                "The weighted total of every recognised contribution in the period, by anybody. A "
                + "guide counts more than a comment; nothing here counts views."),

            LeaderboardScope.Country => (
                "Top in your country",
                "Contribution score",
                "The weighted total of contributions made while the creator was in this country."),

            LeaderboardScope.Community => (
                "Top in this community",
                "Contribution score",
                "The weighted total of contributions made inside this community."),

            LeaderboardScope.Challenge => (
                "Challenge leaderboard",
                "Points",
                "The weighted total of every task action recorded for this challenge."),

            _ => ("Leaderboard", "Score", "A weighted total of recognised contributions.")
        };

    public static string Label(LeaderboardPeriod period) => period switch
    {
        LeaderboardPeriod.Last7Days => "Last 7 days",
        LeaderboardPeriod.Last30Days => "Last 30 days",
        LeaderboardPeriod.Last90Days => "Last 90 days",
        LeaderboardPeriod.Year => "Last 12 months",
        _ => "All time"
    };
}

/// <summary>
/// CREATOR-GAP-012. A creator leaderboard.
/// <para>
/// <b>Five scopes and no sixth</b> — the client's own <c>LEADERBOARD_SCOPES</c>. §15 of the phase
/// brief: "Do not invent additional ranking dimensions unless required by the source contract."
/// </para>
/// <para>
/// <b><see cref="CommunityId"/> is an addition the documented contract needs rather than a new
/// dimension.</b> The register's route carries <c>country</c> but the <c>community</c> scope it also
/// lists cannot be served without knowing which community is meant. The scope is the register's; the
/// parameter is what makes it answerable.
/// </para>
/// </summary>
[RequirePermission(CreatorPermissions.ProfileRead)]
public sealed record GetCreatorLeaderboardQuery : IQuery<CreatorLeaderboardResponse>
{
    public string? Scope { get; init; }

    public string? Period { get; init; }

    public string? CountryCode { get; init; }

    public Guid? CommunityId { get; init; }

    public int? Take { get; init; }
}

public sealed class GetCreatorLeaderboardQueryHandler
    : IQueryHandler<GetCreatorLeaderboardQuery, CreatorLeaderboardResponse>
{
    private readonly CreatorContext _context;
    private readonly ICreatorActivityRepository _activity;
    private readonly ICreatorProfileRepository _creators;
    private readonly IClock _clock;

    public GetCreatorLeaderboardQueryHandler(
        CreatorContext context,
        ICreatorActivityRepository activity,
        ICreatorProfileRepository creators,
        IClock clock)
    {
        _context = context;
        _activity = activity;
        _creators = creators;
        _clock = clock;
    }

    public async Task<Result<CreatorLeaderboardResponse>> Handle(
        GetCreatorLeaderboardQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<CreatorLeaderboardResponse>(caller.Error);
        }

        var scope = ParseScope(query.Scope);

        if (scope is null)
        {
            return Result.Failure<CreatorLeaderboardResponse>(
                CreatorErrors.LeaderboardScopeUnknown);
        }

        if (scope == LeaderboardScope.Challenge)
        {
            // The challenge board is its own route — /challenges/{id}/leaderboard — because it ranks
            // enrolments rather than the activity ledger. Answering here would be a second ranking
            // of the same thing computed a different way.
            return Result.Failure<CreatorLeaderboardResponse>(
                CreatorErrors.LeaderboardScopeNeedsSubject);
        }

        if (scope == LeaderboardScope.Community && query.CommunityId is null)
        {
            return Result.Failure<CreatorLeaderboardResponse>(
                CreatorErrors.LeaderboardScopeNeedsSubject);
        }

        var creator = await _context.FindCreatorAsync(cancellationToken);

        var country = string.IsNullOrWhiteSpace(query.CountryCode) && scope == LeaderboardScope.Country
            ? creator?.CountryCode
            : query.CountryCode;

        if (scope == LeaderboardScope.Country && string.IsNullOrWhiteSpace(country))
        {
            return Result.Failure<CreatorLeaderboardResponse>(
                CreatorErrors.LeaderboardScopeNeedsSubject);
        }

        var period = Enum.TryParse<LeaderboardPeriod>(query.Period, ignoreCase: true, out var parsed)
            ? parsed
            : ParsePeriodAlias(query.Period);

        var now = _clock.UtcNow;

        var request = new LeaderboardQuery
        {
            Scope = scope.Value,
            Period = period,
            CountryCode = scope == LeaderboardScope.Country ? country : null,
            CommunityId = scope == LeaderboardScope.Community ? query.CommunityId : null,
            Take = CreatorLimits.LeaderboardSize(query.Take),
            AsOf = now
        };

        var rows = await _activity.LeaderboardAsync(request, cancellationToken);

        var profiles = (await _creators.GetManyAsync(
                rows.Select(row => row.CreatorId).ToList(), cancellationToken))
            .ToDictionary(profile => profile.Id);

        var entries = rows
            .Where(row => profiles.ContainsKey(row.CreatorId))
            .Select(row => Entry(profiles[row.CreatorId], row.Score, row.Rank))
            .ToList();

        CreatorLeaderboardEntryResponse? viewer = null;
        var optedOut = false;

        if (creator is not null)
        {
            // A creator who opted out is absent from the ranking, so their own row is absent too.
            // Saying so explicitly is what stops the client showing "unranked" and implying a zero.
            optedOut = !creator.IsDiscoverable || creator.IsSuspended || creator.IsRestricted;

            if (!optedOut)
            {
                var mine = await _activity.ViewerRowAsync(request, creator.Id, cancellationToken);

                if (mine is not null)
                {
                    viewer = Entry(creator, mine.Score, mine.Rank);
                }
            }
        }

        var (title, scoreLabel, definition) = LeaderboardDefinitions.For(scope.Value);

        return Result.Success(new CreatorLeaderboardResponse
        {
            Scope = query.Scope ?? scope.Value.ToString(),
            Title = title,
            Period = period.ToString(),
            PeriodLabel = LeaderboardDefinitions.Label(period),
            ScoreLabel = scoreLabel,
            ScoreDefinition = definition,
            Entries = entries,
            ViewerEntry = viewer,
            ViewerIsOptedOut = optedOut,

            // Computed on the request from the activity ledger, so the honest answer is now. The
            // register warns against "updated 3 hours ago" unless the server actually knows that.
            UpdatedAt = now,
            RefreshNote = "Computed when you asked for it."
        });
    }

    /// <summary>
    /// Reads the client's own scope spelling.
    /// <para>
    /// The client sends <c>top-creators</c> and <c>top-contributors</c>; the enum spells them
    /// <c>TopCreators</c> and <c>TopContributors</c>. Accepting both is what makes the register's
    /// route work unchanged.
    /// </para>
    /// </summary>
    private static LeaderboardScope? ParseScope(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return LeaderboardScope.TopCreators;
        }

        return scope.Trim().ToLowerInvariant() switch
        {
            "top-creators" or "topcreators" => LeaderboardScope.TopCreators,
            "top-contributors" or "topcontributors" => LeaderboardScope.TopContributors,
            "country" => LeaderboardScope.Country,
            "community" => LeaderboardScope.Community,
            "challenge" => LeaderboardScope.Challenge,
            _ => null
        };
    }

    /// <summary>The client's period spellings — <c>7d</c>, <c>30d</c>, <c>90d</c>, <c>year</c>.</summary>
    private static LeaderboardPeriod ParsePeriodAlias(string? period) =>
        (period ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "7d" => LeaderboardPeriod.Last7Days,
            "30d" => LeaderboardPeriod.Last30Days,
            "90d" => LeaderboardPeriod.Last90Days,
            "year" => LeaderboardPeriod.Year,
            _ => LeaderboardPeriod.AllTime
        };

    private static CreatorLeaderboardEntryResponse Entry(
        CreatorProfile creator, long score, int rank) =>
        new()
        {
            Rank = rank,
            CreatorId = creator.Id.Value,
            DisplayName = creator.DisplayName,
            AvatarUrl = creator.AvatarUrl,
            Score = score,
            IsVerifiedCreator = creator.IsVerifiedCreator,
            CountryCode = creator.CountryCode
        };
}

/// <summary>
/// CREATOR-GAP-014. The public creator directory.
/// <para>
/// <b>A creator who opted out is absent from the response, not filtered from it.</b> The facets are
/// computed under the same predicate, so a private creator does not appear in a count either.
/// </para>
/// </summary>
[RequirePermission(CreatorPermissions.ProfileRead)]
public sealed record SearchCreatorsQuery : IQuery<CreatorDirectoryResponse>
{
    public string? Query { get; init; }

    public string? CountryCode { get; init; }

    public string? Category { get; init; }

    public string? Expertise { get; init; }

    public string? Language { get; init; }

    public bool? Verified { get; init; }

    public string? Cursor { get; init; }

    public int? Take { get; init; }
}

public sealed class SearchCreatorsQueryHandler
    : IQueryHandler<SearchCreatorsQuery, CreatorDirectoryResponse>
{
    private readonly CreatorContext _context;
    private readonly ICreatorProfileRepository _creators;
    private readonly CreatorBadgeReader _badges;

    public SearchCreatorsQueryHandler(
        CreatorContext context, ICreatorProfileRepository creators, CreatorBadgeReader badges)
    {
        _context = context;
        _creators = creators;
        _badges = badges;
    }

    public async Task<Result<CreatorDirectoryResponse>> Handle(
        SearchCreatorsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<CreatorDirectoryResponse>(caller.Error);
        }

        var filter = new CreatorDirectoryFilter
        {
            Query = query.Query,
            CountryCode = query.CountryCode,
            Category = query.Category,
            Expertise = query.Expertise,
            Language = query.Language,
            Verified = query.Verified,
            Cursor = query.Cursor,
            Take = CreatorLimits.PageSize(query.Take)
        };

        var page = await _creators.SearchAsync(filter, cancellationToken);
        var facets = await _creators.FacetsAsync(filter, cancellationToken);

        var badges = await _badges.ForAsync(
            page.Creators.Select(creator => creator.Id).ToList(), cancellationToken);

        return Result.Success(new CreatorDirectoryResponse
        {
            Creators = page.Creators
                .Select(creator => creator.ToPublicResponse(
                    badges.TryGetValue(creator.Id, out var earned) ? earned : []))
                .ToList(),
            NextCursor = page.NextCursor,
            HasMore = page.HasMore,
            Facets = new CreatorFacetsResponse
            {
                Countries = facets.Countries
                    .Select(facet => new FacetCountResponse(facet.Value, facet.Count))
                    .ToList(),
                Categories = facets.Categories
                    .Select(facet => new FacetCountResponse(facet.Value, facet.Count))
                    .ToList(),
                Expertise = facets.Expertise
                    .Select(facet => new FacetCountResponse(facet.Value, facet.Count))
                    .ToList(),
                Languages = facets.Languages
                    .Select(facet => new FacetCountResponse(facet.Value, facet.Count))
                    .ToList(),
                VerifiedCount = facets.VerifiedCount,
                TotalCount = facets.TotalCount
            }
        });
    }
}
