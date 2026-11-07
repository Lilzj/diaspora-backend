using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;
using Diaspora.Modules.Jobs.Domain.Employers;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Domain.Reports;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Application.Moderation;

/// <summary>
/// Reads the moderation queue.
/// <para>
/// Each row carries what a decision actually needs: the advert's title, the employer's name,
/// how many people have reported this advert, and <b>how many upheld reports that employer
/// already has across every advert they have placed</b>. The last of those is the only way a
/// moderator looking at one card can tell a one-off complaint from a pattern, which is what
/// recruitment fraud looks like from the inside.
/// </para>
/// </summary>
public sealed class ListJobReportsQueryHandler
    : IQueryHandler<ListJobReportsQuery, IReadOnlyList<JobReportResponse>>
{
    private const int DefaultLimit = 50;

    private const int MaximumLimit = 200;

    private readonly IJobReportRepository _reports;
    private readonly IJobPostingRepository _postings;
    private readonly IEmployerProfileRepository _employers;
    private readonly JobAuthorizationService _authorization;

    public ListJobReportsQueryHandler(
        IJobReportRepository reports,
        IJobPostingRepository postings,
        IEmployerProfileRepository employers,
        JobAuthorizationService authorization)
    {
        _reports = reports;
        _postings = postings;
        _employers = employers;
        _authorization = authorization;
    }

    public async Task<Result<IReadOnlyList<JobReportResponse>>> Handle(
        ListJobReportsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_authorization.IsStaff)
        {
            return Result.Failure<IReadOnlyList<JobReportResponse>>(JobErrors.NotModerator);
        }

        var limit = Math.Clamp(request.Limit ?? DefaultLimit, 1, MaximumLimit);

        var reports = await _reports.ListAsync(
            request.OpenOnly,
            request.FraudFirst,
            request.PostingId is { } postingId ? JobPostingId.From(postingId) : null,
            limit,
            cancellationToken);

        var rows = new List<JobReportResponse>(reports.Count);

        foreach (var report in reports)
        {
            var posting = await _postings.GetIncludingDeletedAsync(report.PostingId, cancellationToken);
            var employer = await _employers.GetForReadAsync(report.EmployerId, cancellationToken);

            rows.Add(new JobReportResponse
            {
                ReportId = report.Id.Value,
                PostingId = report.PostingId.Value,

                // A report outlives the advert it was about, so a removed posting reads as
                // removed rather than leaving the row blank.
                PostingTitle = posting?.Title.Value ?? "This advert has been removed",
                EmployerId = report.EmployerId.Value,
                EmployerName = employer?.Identity.DisplayName ?? "Unknown employer",
                Reason = report.Reason.ToString(),
                Detail = report.Detail,
                Status = report.Status.ToString(),
                EmployerUpheldReports = await _reports.CountUpheldForEmployerAsync(
                    report.EmployerId, cancellationToken),
                TotalReportsOnPosting = await _reports.CountForPostingAsync(
                    report.PostingId, cancellationToken),
                PostingModerationState = posting?.ModerationState.ToString() ?? "Unknown",
                Resolution = report.Resolution,
                ResolvedAt = report.ResolvedAt,
                CreatedAt = report.CreatedAt
            });
        }

        return Result.Success<IReadOnlyList<JobReportResponse>>(rows);
    }
}
