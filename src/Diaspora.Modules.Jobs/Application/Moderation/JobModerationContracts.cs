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
[Authorize(Permission = JobPermissions.Report)]
[Transactional]
[Auditable]
public sealed record ReportJobPostingCommand : ICommand<Guid>
{
    public required Guid PostingId { get; init; }

    public required ReportJobPostingRequest Request { get; init; }
}

/// <summary>The moderation queue. Staff.</summary>
[Authorize(Permission = JobPermissions.Moderate)]
public sealed record ListJobReportsQuery : IQuery<IReadOnlyList<JobReportResponse>>
{
    public bool OpenOnly { get; init; } = true;

    /// <summary>Puts the reports about money at the front. On by default.</summary>
    public bool FraudFirst { get; init; } = true;

    public Guid? PostingId { get; init; }

    public int? Limit { get; init; }
}

/// <summary>Closes a report, and optionally takes the advert down with it. Staff.</summary>
[Authorize(Permission = JobPermissions.Moderate)]
[Transactional]
[Auditable]
public sealed record ResolveJobReportCommand : ICommand<JobReportResponse>
{
    public required Guid ReportId { get; init; }

    public required ResolveJobReportRequest Request { get; init; }
}

/// <summary>Acts on the advert itself, separately from closing any report. Staff.</summary>
[Authorize(Permission = JobPermissions.Moderate)]
[Transactional]
[Auditable]
public sealed record ModerateJobPostingCommand : ICommand
{
    public required Guid PostingId { get; init; }

    public required ModerateJobPostingRequest Request { get; init; }
}
