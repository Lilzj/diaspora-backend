using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.Modules.CommunityLife.Domain.Reports;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Moderation;

/// <summary>
/// The moderation queue.
/// <para>
/// Staff only, and the reporter's identity travels with each row — a moderator deciding whether a
/// complaint is co-ordinated has to be able to see who is making them, which is exactly what the
/// public contract withholds.
/// </para>
/// <para>
/// Open and under-review by default: a queue that led with everything already decided would bury the
/// work.
/// </para>
/// </summary>
public sealed class ListReportsQueryHandler : IQueryHandler<ListReportsQuery, ReportPageResponse>
{
    private readonly ICommunityLifeReportRepository _reports;
    private readonly CommunityLifeAuthorizationService _authorization;

    public ListReportsQueryHandler(
        ICommunityLifeReportRepository reports,
        CommunityLifeAuthorizationService authorization)
    {
        _reports = reports;
        _authorization = authorization;
    }

    public async Task<Result<ReportPageResponse>> Handle(
        ListReportsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var staff = _authorization.RequireStaff();

        if (staff.IsFailure)
        {
            return Result.Failure<ReportPageResponse>(staff.Error);
        }

        var page = await _reports.SearchAsync(
            CommunityLifeSharedMapper.ParseEnum<ReportSubjectType>(query.SubjectType),
            ParseStatuses(query.Statuses),
            query.Cursor,
            Math.Clamp(query.Limit ?? 20, 1, CommunityLifeLimits.MaxPageSize),
            cancellationToken);

        return Result.Success(new ReportPageResponse
        {
            Items =
            [
                .. page.Items.Select(report =>
                    CommunityLifeCatalogueMapper.ToResponse(report, includeReporter: true))
            ],
            NextCursor = page.NextCursor,
            HasMore = page.HasMore
        });
    }

    private static List<CommunityLifeReportStatus> ParseStatuses(
        IReadOnlyList<string>? statuses)
    {
        if (statuses is not { Count: > 0 })
        {
            return [CommunityLifeReportStatus.Open, CommunityLifeReportStatus.UnderReview];
        }

        var parsed = statuses
            .Select(CommunityLifeSharedMapper.ParseEnum<CommunityLifeReportStatus>)
            .OfType<CommunityLifeReportStatus>()
            .Distinct()
            .ToList();

        return parsed.Count == 0
            ? [CommunityLifeReportStatus.Open, CommunityLifeReportStatus.UnderReview]
            : parsed;
    }
}
