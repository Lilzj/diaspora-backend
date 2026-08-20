using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Moderation;

/// <summary>
/// Closes a report, optionally acting on its subject at the same time.
/// <para>
/// Transactional: upholding a report and hiding what it was about are one decision, and a moderator
/// should not be able to record the first without the second taking effect.
/// </para>
/// </summary>
[Authorize(Permission = CommunityLifePermissions.Moderate)]
[Transactional(Reason = "Closes the report and applies the moderation action to its subject.")]
[Auditable]
public sealed record ResolveReportCommand : ICommand<ReportResponse>
{
    public required Guid ReportId { get; init; }

    public required ResolveReportRequest Request { get; init; }
}
