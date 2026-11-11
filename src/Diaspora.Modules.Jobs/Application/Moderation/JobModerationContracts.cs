using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Contracts.V1.Requests;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;

namespace Diaspora.Modules.Jobs.Application.Moderation;

/// <summary>
/// A member reporting a job advert.
/// <para>
/// Gated on <c>jobs:report</c>, which every member holds and which was declared and gated
/// nothing until this command existed.
/// </para>
/// </summary>
[RequirePermission(JobPermissions.Report)]
[Transactional]
[Auditable]
public sealed record ReportJobPostingCommand : ICommand<Guid>
{
    public required Guid PostingId { get; init; }

    public required ReportJobPostingRequest Request { get; init; }
}

/// <summary>The moderation queue. Staff.</summary>
[RequirePermission(JobPermissions.Moderate)]
public sealed record ListJobReportsQuery : IQuery<IReadOnlyList<JobReportResponse>>
{
    public bool OpenOnly { get; init; } = true;

    /// <summary>Puts the reports about money at the front. On by default.</summary>
    public bool FraudFirst { get; init; } = true;

    public Guid? PostingId { get; init; }

    public int? Limit { get; init; }
}
