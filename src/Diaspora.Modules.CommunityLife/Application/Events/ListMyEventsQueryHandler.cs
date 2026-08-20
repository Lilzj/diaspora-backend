using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Events;

/// <summary>
/// The caller's own events, drafts included.
/// <para>
/// <b>Scoped to the caller, or to one organization they may run events for.</b> Passing an
/// organization identifier without that permission narrows the page to nothing rather than refusing,
/// because a client that asked about a body it no longer organizes for should see an empty list, not
/// an error it cannot act on.
/// </para>
/// </summary>
public sealed class ListMyEventsQueryHandler
    : IQueryHandler<ListMyEventsQuery, EventSearchResultResponse>
{
    private readonly ICommunityEventRepository _events;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly CommunityEventReadContext _read;

    public ListMyEventsQueryHandler(
        ICommunityEventRepository events,
        CommunityLifeAuthorizationService authorization,
        CommunityEventReadContext read)
    {
        _events = events;
        _authorization = authorization;
        _read = read;
    }

    public async Task<Result<EventSearchResultResponse>> Handle(
        ListMyEventsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<EventSearchResultResponse>(caller.Error);
        }

        var organizations = await _authorization.CallerOrganizationsAsync(cancellationToken);

        var scope = ResolveScope(query, organizations);

        if (scope.IsFailure)
        {
            return Result.Failure<EventSearchResultResponse>(scope.Error);
        }

        var page = await _events.SearchAsync(
            new EventQueryFilter
            {
                // Either the caller's own events, or one body's. Never both, which would mix a
                // personal list with an institutional one on the same screen.
                OrganizerUserId = scope.Value is null ? caller.Value : null,
                OrganizationId = scope.Value,
                Statuses = ParseStatuses(query.Statuses),
                IncludeNonPublic = true,
                IncludePast = query.IncludePast,
                ViewerUserId = caller.Value,
                ViewerOrganizationIds = organizations,
                SortOrder = EventSortOrder.Newest,
                Cursor = query.Cursor,
                Limit = Math.Clamp(query.Limit ?? 20, 1, CommunityLifeLimits.MaxPageSize)
            },
            cancellationToken);

        return Result.Success(new EventSearchResultResponse
        {
            Items = await _read.ToSummariesAsync(
                page.Items, cancellationToken: cancellationToken),
            NextCursor = page.NextCursor,
            HasMore = page.HasMore,
            TotalCount = page.TotalCount
        });
    }

    private static Result<OrganizationId?> ResolveScope(
        ListMyEventsQuery query, IReadOnlyList<OrganizationId> organizations)
    {
        if (query.OrganizationId is not { } id || id == Guid.Empty)
        {
            return Result.Success<OrganizationId?>(null);
        }

        var organizationId = OrganizationId.From(id);

        return organizations.Contains(organizationId)
            ? Result.Success<OrganizationId?>(organizationId)
            : Result.Failure<OrganizationId?>(CommunityLifeErrors.NotOrganizationMember);
    }

    private static List<EventStatus>? ParseStatuses(IReadOnlyList<string>? statuses)
    {
        if (statuses is not { Count: > 0 })
        {
            return null;
        }

        var parsed = statuses
            .Select(CommunityLifeSharedMapper.ParseEnum<EventStatus>)
            .OfType<EventStatus>()
            .Distinct()
            .ToList();

        return parsed.Count == 0 ? null : parsed;
    }
}
