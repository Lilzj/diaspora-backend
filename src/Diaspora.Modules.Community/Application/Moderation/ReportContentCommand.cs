using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Contracts.V1.Requests;
using Diaspora.Modules.Community.Contracts.V1.Responses;

namespace Diaspora.Modules.Community.Application.Moderation;

/// <summary>Reports content for review.</summary>
[Auditable]
[Transactional]
[Authorize(Policy = CommunityPolicies.Report)]
public sealed record ReportContentCommand : ICommand<ReportResponse>
{
    public required CreateReportRequest Request { get; init; }
}
