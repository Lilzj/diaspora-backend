using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Application.Mapping;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Neighborhoods;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Neighborhoods;

/// <summary>
/// Insights about a place.
/// <para>
/// <b>Three audiences, one query, and the difference between them is what gets seen.</b> A visitor
/// sees approved insights only. An author asking for their own sees their own whatever the status,
/// because a member who submitted something deserves to know it was refused and why. A moderator
/// asking for the queue sees everything pending — and only a moderator can ask for that, because
/// requesting the pending queue without the permission is silently narrowed to approved rather than
/// refused, which keeps the existence of the queue itself out of view.
/// </para>
/// </summary>
public sealed class ListNeighborhoodInsightsQueryHandler
    : IQueryHandler<ListNeighborhoodInsightsQuery, IReadOnlyList<NeighborhoodInsightResponse>>
{
    private readonly INeighborhoodInsightRepository _insights;
    private readonly HousingAuthorizationService _authorization;

    public ListNeighborhoodInsightsQueryHandler(
        INeighborhoodInsightRepository insights,
        HousingAuthorizationService authorization)
    {
        _insights = insights;
        _authorization = authorization;
    }

    public async Task<Result<IReadOnlyList<NeighborhoodInsightResponse>>> Handle(
        ListNeighborhoodInsightsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var callerId = _authorization.CallerId;
        var isStaff = _authorization.IsStaff;

        if (request.MineOnly && callerId is null)
        {
            return Result.Failure<IReadOnlyList<NeighborhoodInsightResponse>>(
                HousingErrors.NotAuthenticated);
        }

        // Asking for the queue without the permission is narrowed rather than refused: a refusal
        // would confirm there is a queue to be refused from.
        var pending = request.PendingOnly && isStaff;

        var filter = new NeighborhoodInsightQueryFilter
        {
            AreaId = request.AreaId is { } areaId && areaId != Guid.Empty ? areaId : null,
            AuthorId = request.MineOnly ? callerId : null,
            Categories = Enum.TryParse<NeighborhoodInsightCategory>(
                request.Category, ignoreCase: true, out var category)
                ? [category]
                : null,
            Statuses = pending ? [NeighborhoodInsightStatus.Submitted] : null,
            IncludeUnapproved = pending || request.MineOnly,
            OrderByEndorsement = request.ByEndorsement && !pending,
            Limit = request.Limit ?? HousingLimits.DefaultPageSize
        };

        var page = await _insights.QueryAsync(
            filter, request.Cursor, filter.EffectiveLimit, cancellationToken);

        var rendered = new List<NeighborhoodInsightResponse>(page.Items.Count);

        foreach (var insight in page.Items)
        {
            var endorsed = callerId is not null
                && await _insights.HasEndorsedAsync(insight.Id, callerId, cancellationToken);

            rendered.Add(NeighborhoodMapper.ToResponse(
                insight, callerId, isStaff, endorsed));
        }

        return Result.Success<IReadOnlyList<NeighborhoodInsightResponse>>(rendered);
    }
}
