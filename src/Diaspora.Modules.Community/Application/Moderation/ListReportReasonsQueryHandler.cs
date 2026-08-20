using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Application.Mapping;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Moderation;

/// <summary>Serves the reason catalogue.</summary>
public sealed class ListReportReasonsQueryHandler
    : IQueryHandler<ListReportReasonsQuery, IReadOnlyList<ReportReasonResponse>>
{
    public Task<Result<IReadOnlyList<ReportReasonResponse>>> Handle(
        ListReportReasonsQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(ModerationMapper.Reasons()));
}
