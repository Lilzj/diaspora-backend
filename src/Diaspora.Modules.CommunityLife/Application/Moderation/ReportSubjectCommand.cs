using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Moderation;

/// <summary>
/// Reports an event or an organization.
/// <para>
/// Transactional: the report and the subject's own report count commit together, so the threshold
/// that auto-flags cannot be crossed twice by two reports racing.
/// </para>
/// </summary>
[Authorize(Permission = CommunityLifePermissions.Report)]
[Transactional(Reason = "Files the report and moves the subject's report count.")]
[Auditable]
public sealed record ReportSubjectCommand : ICommand<ReportResponse>
{
    public required string SubjectType { get; init; }

    public required Guid SubjectId { get; init; }

    public required ReportSubjectRequest Request { get; init; }
}
