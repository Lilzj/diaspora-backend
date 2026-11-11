using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityProgramme.Application.Services;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityProgramme.Application.Communities;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// A community in numbers, and communities side by side — FRONTEND-PHASE13-GAP-007/-008, 21 Oct 2026.
//
// **Who may read.** A public or unlisted community's figures are as public as its member count
// already is; a private or invite-only community's are its current members'. Restricted figures —
// departures and members' activity — go only to callers `CanViewPrivateStatistics` names (moderators,
// administrators, the owner and staff), and everybody else never receives them at all.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>One community's figures for a period.</summary>
[AllowAnonymous(
    "Public discovery. The route already says AllowAnonymous and this is the same statement at " +
    "the other end of the pipeline: a listing nobody can see without signing up is a listing " +
    "nobody answers.")]
public sealed record GetCommunityStatisticsQuery : IQuery<CommunityStatisticsResponse>
{
    public required string IdOrSlug { get; init; }

    /// <summary>7d, 30d, 90d, 12m or all. Defaults to 30d.</summary>
    public string? Period { get; init; }
}

public sealed class GetCommunityStatisticsQueryHandler
    : IQueryHandler<GetCommunityStatisticsQuery, CommunityStatisticsResponse>
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly CommunityProgrammeAccessService _access;
    private readonly CommunityStatisticsComposer _composer;

    public GetCommunityStatisticsQueryHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        CommunityProgrammeAccessService access,
        CommunityStatisticsComposer composer)
    {
        _communities = communities;
        _memberships = memberships;
        _access = access;
        _composer = composer;
    }

    public async Task<Result<CommunityStatisticsResponse>> Handle(
        GetCommunityStatisticsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var period = CommunityStatisticsComposer.ParsePeriod(query.Period);

        if (period.IsFailure)
        {
            return Result.Failure<CommunityStatisticsResponse>(period.Error);
        }

        // The notice board's reader rule is the statistics' rule too: open communities to anybody,
        // closed ones to their current members.
        var readable = await CommunityNoticeAccess.ResolveReaderAsync(
            query.IdOrSlug, _communities, _memberships, _access, cancellationToken);

        if (readable is null)
        {
            return Result.Failure<CommunityStatisticsResponse>(CommunityErrors.NotFound);
        }

        var window = CommunityStatisticsComposer.WindowFor(
            period.Value, readable.Community, DateTimeOffset.UtcNow);

        return Result.Success(await _composer.ComposeAsync(
            readable.Community,
            window,
            includeRestricted: readable.Capabilities.CanViewPrivateStatistics,
            cancellationToken));
    }
}

/// <summary>Two to four communities' figures, aligned by metric. No winner and no score.</summary>
[AllowAnonymous(
    "Public discovery. The route already says AllowAnonymous and this is the same statement at " +
    "the other end of the pipeline: a listing nobody can see without signing up is a listing " +
    "nobody answers.")]
public sealed record CompareCommunitiesQuery : IQuery<CommunityComparisonResponse>
{
    /// <summary>Comma-joined identifiers or handles, as the client sends them.</summary>
    public string? Ids { get; init; }

    public string? Period { get; init; }
}

public sealed class CompareCommunitiesQueryHandler
    : IQueryHandler<CompareCommunitiesQuery, CommunityComparisonResponse>
{
    /// <summary>The comparison screen's own limit. More is a table nobody reads on a phone.</summary>
    public const int MaximumCommunities = 4;

    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly CommunityProgrammeAccessService _access;
    private readonly CommunityStatisticsComposer _composer;

    public CompareCommunitiesQueryHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        CommunityProgrammeAccessService access,
        CommunityStatisticsComposer composer)
    {
        _communities = communities;
        _memberships = memberships;
        _access = access;
        _composer = composer;
    }

    public async Task<Result<CommunityComparisonResponse>> Handle(
        CompareCommunitiesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var period = CommunityStatisticsComposer.ParsePeriod(query.Period);

        if (period.IsFailure)
        {
            return Result.Failure<CommunityComparisonResponse>(period.Error);
        }

        var ids = (query.Ids ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ids.Count < 2)
        {
            return Result.Failure<CommunityComparisonResponse>(CommunityGovernanceErrors.CompareTooFew);
        }

        if (ids.Count > MaximumCommunities)
        {
            return Result.Failure<CommunityComparisonResponse>(CommunityGovernanceErrors.CompareTooMany);
        }

        var now = DateTimeOffset.UtcNow;
        var communities = new List<CommunityRefResponse>(ids.Count);
        var statistics = new List<CommunityStatisticsResponse>(ids.Count);

        // Sequential: one scoped DbContext.
        foreach (var id in ids)
        {
            var readable = await CommunityNoticeAccess.ResolveReaderAsync(
                id, _communities, _memberships, _access, cancellationToken);

            // One unreadable community fails the whole comparison with the community's own
            // not-found. Leaving it out would shift the columns, and naming which one failed would
            // confirm a private community exists.
            if (readable is null)
            {
                return Result.Failure<CommunityComparisonResponse>(CommunityErrors.NotFound);
            }

            // The same community twice, under its id and its handle, is one column.
            if (communities.Any(existing => existing.Id == readable.Community.Id.Value))
            {
                continue;
            }

            var full = CommunityMapper.ToResponse(readable.Community);

            communities.Add(new CommunityRefResponse
            {
                Id = full.Id,
                Slug = full.Slug,
                Name = full.Name,
                Type = full.Type,
                Origin = full.Origin,
                Identity = full.Identity
            });

            // Restricted figures are never compared, even for a caller who may see them: three
            // communities side by side make a suppressed cell obvious by subtraction.
            statistics.Add(await _composer.ComposeAsync(
                readable.Community,
                CommunityStatisticsComposer.WindowFor(period.Value, readable.Community, now),
                includeRestricted: false,
                cancellationToken));
        }

        if (communities.Count < 2)
        {
            return Result.Failure<CommunityComparisonResponse>(CommunityGovernanceErrors.CompareTooFew);
        }

        var keys = statistics
            .SelectMany(entry => entry.Metrics)
            .Where(metric => !metric.IsRestricted)
            .Select(metric => (metric.Key, metric.Label, metric.Unit))
            .DistinctBy(metric => metric.Key)
            .ToList();

        return Result.Success(new CommunityComparisonResponse
        {
            Period = period.Value,
            Communities = communities,
            Rows =
            [
                .. keys.Select(metric => new CommunityComparisonRowResponse
                {
                    Key = metric.Key,
                    Label = metric.Label,
                    Unit = metric.Unit,
                    Values =
                    [
                        .. statistics.Select(entry =>
                            entry.Metrics.FirstOrDefault(candidate => candidate.Key == metric.Key)?.Value)
                    ]
                })
            ],
            Source = "backend",
            Freshness = new DataFreshnessResponse
            {
                ComputedAt = now,
                SourceName = "Diaspora community records",
                Methodology =
                    "Each community's figures are those of its own statistics for the same period. No "
                    + "winner, ranking or composite score is computed.",
                Limitations =
                [
                    "A period of 'all' covers each community since it began, so those columns span "
                    + "different lengths of time."
                ]
            }
        });
    }
}
