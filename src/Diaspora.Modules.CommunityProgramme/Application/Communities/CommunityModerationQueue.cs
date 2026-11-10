using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Community.V1.Moderation;
using Diaspora.Modules.CommunityProgramme.Application.Services;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.SharedKernel.Results;

using CommunityAggregate = Diaspora.Modules.CommunityProgramme.Domain.Communities.Community;

namespace Diaspora.Modules.CommunityProgramme.Application.Communities;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// FRONTEND-PHASE13-GAP-017 — a community's own moderation queue (21 Oct 2026).
//
// GET  /communities/{idOrSlug}/reports
// POST /communities/{idOrSlug}/reports/{reportId}/decision
//
// Split along the line the two modules already draw. This module answers "may the caller moderate
// this community" from the capability set the client already reads; Community answers "which
// reports are about this community's content" and applies the decision, through
// IPublisherReportQueue. A community's content is what was published as the community — a post
// whose publisher is the community, and the comments under it — and nothing the caller sends can
// change that.
// ─────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>A page of the community's queue, and where the next one starts.</summary>
public sealed record CommunityReportPage(
    IReadOnlyList<CommunityReportResponse> Items, string? NextCursor, bool HasMore);

/// <summary>Reports about a community's content, for its moderators and administrators.</summary>
[RequirePermission(CommunityProgrammePermissions.Read)]
public sealed record GetCommunityReportsQuery : IQuery<CommunityReportPage>
{
    public required string IdOrSlug { get; init; }

    public string? Status { get; init; }

    public string? Cursor { get; init; }

    public int Limit { get; init; } = CommunityModerationAccess.DefaultLimit;
}

public sealed class GetCommunityReportsQueryHandler
    : IQueryHandler<GetCommunityReportsQuery, CommunityReportPage>
{
    private readonly CommunityModerationAccess _access;
    private readonly IPublisherReportQueue _queue;

    public GetCommunityReportsQueryHandler(
        CommunityModerationAccess access, IPublisherReportQueue queue)
    {
        _access = access;
        _queue = queue;
    }

    public async Task<Result<CommunityReportPage>> Handle(
        GetCommunityReportsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var community = await _access.RequireModeratorAsync(
            query.IdOrSlug, capabilities => capabilities.CanViewModerationQueue, cancellationToken);

        if (community.IsFailure)
        {
            return Result.Failure<CommunityReportPage>(community.Error);
        }

        var communityId = community.Value.Id.Value;

        var page = await _queue.ListAsync(
            communityId, query.Status, query.Cursor, query.Limit, cancellationToken);

        return page.IsFailure
            ? Result.Failure<CommunityReportPage>(page.Error)
            : Result.Success(new CommunityReportPage(
                [.. page.Value.Items.Select(item => CommunityModerationAccess.ToResponse(communityId, item))],
                page.Value.NextCursor,
                page.Value.HasMore));
    }
}

/// <summary>A community moderator deciding a report about the community's content.</summary>
[RequirePermission(CommunityProgrammePermissions.Read)]
[Transactional]
public sealed record DecideCommunityReportCommand : ICommand<CommunityReportResponse>
{
    public required string IdOrSlug { get; init; }

    public required Guid ReportId { get; init; }

    public string? Decision { get; init; }

    public string? Note { get; init; }
}

public sealed class DecideCommunityReportCommandHandler
    : ICommandHandler<DecideCommunityReportCommand, CommunityReportResponse>
{
    private readonly CommunityModerationAccess _access;
    private readonly IPublisherReportQueue _queue;

    public DecideCommunityReportCommandHandler(
        CommunityModerationAccess access, IPublisherReportQueue queue)
    {
        _access = access;
        _queue = queue;
    }

    public async Task<Result<CommunityReportResponse>> Handle(
        DecideCommunityReportCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var community = await _access.RequireModeratorAsync(
            command.IdOrSlug, capabilities => capabilities.CanReviewReports, cancellationToken);

        if (community.IsFailure)
        {
            return Result.Failure<CommunityReportResponse>(community.Error);
        }

        var communityId = community.Value.Id.Value;

        var decided = await _queue.DecideAsync(
            communityId,
            command.ReportId,
            command.Decision,
            command.Note,
            _access.CallerId!.Value,
            cancellationToken);

        return decided.IsFailure
            ? Result.Failure<CommunityReportResponse>(decided.Error)
            : Result.Success(CommunityModerationAccess.ToResponse(communityId, decided.Value));
    }
}

/// <summary>
/// The one check both routes make: the community exists for this caller, and the caller holds the
/// capability. Read from <see cref="CommunityCapabilitySet"/> — the same answer
/// <c>GET /communities/{idOrSlug}/capabilities</c> gives the client — so the button the app draws
/// and the action the server allows cannot disagree.
/// </summary>
public sealed class CommunityModerationAccess
{
    public const int DefaultLimit = 20;

    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly CommunityProgrammeAccessService _access;

    public CommunityModerationAccess(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        CommunityProgrammeAccessService access)
    {
        _communities = communities;
        _memberships = memberships;
        _access = access;
    }

    public Guid? CallerId => _access.CallerId?.Value;

    public async Task<Result<CommunityAggregate>> RequireModeratorAsync(
        string idOrSlug,
        Func<CommunityCapabilitySet, bool> holds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(holds);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CommunityAggregate>(caller.Error);
        }

        var community = await _communities.FindByIdOrSlugAsync(idOrSlug, cancellationToken);

        if (community is null)
        {
            return Result.Failure<CommunityAggregate>(CommunityErrors.NotFound);
        }

        var membership = await _memberships.FindAsync(community.Id, caller.Value, cancellationToken);

        // A community the caller may not read does not exist for them, here as on its profile.
        if (community.Visibility is CommunityVisibility.Private or CommunityVisibility.InviteOnly
            && membership is null
            && !_access.IsStaff)
        {
            return Result.Failure<CommunityAggregate>(CommunityErrors.NotFound);
        }

        return holds(CommunityCapabilitySet.For(community, membership, _access.IsStaff))
            ? Result.Success(community)
            : Result.Failure<CommunityAggregate>(CommunityErrors.CannotModerate);
    }

    public static CommunityReportResponse ToResponse(Guid communityId, PublisherReportSnapshot item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new CommunityReportResponse
        {
            Id = item.ReportId,
            CommunityId = communityId,
            Status = item.Status,
            Reason = item.Reason,
            ContentType = item.ContentType,
            ContentId = item.ContentId,
            ContentExcerpt = item.ContentExcerpt,
            ReporterCount = item.ReporterCount,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
            Note = item.Note
        };
    }
}
