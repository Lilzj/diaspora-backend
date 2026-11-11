using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;

namespace Diaspora.Modules.Guides.Application.Engagement;

/// <summary>
/// Reports a guide to moderation.
/// <para>
/// The report goes to Community's workflow, and this module implements none of it — not the
/// reason catalogue, not the duplicate rule, not the moderation case the report opens in Trust.
/// A second moderation queue in a second module is how a platform ends up with two answers to
/// "has this been dealt with".
/// </para>
/// </summary>
[RequirePermission(GuidePermissions.Report)]
[Auditable]
public sealed record ReportGuideCommand : ICommand
{
    public required Guid GuideId { get; init; }

    public required ReportGuideRequest Request { get; init; }
}
