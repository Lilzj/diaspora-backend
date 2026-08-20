using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;

namespace Diaspora.Modules.Marketplace.Application.Moderation;

/// <summary>Closes a report, upheld or not.</summary>
[Authorize(Permission = MarketplacePermissions.Moderate)]
[Auditable]
public sealed record ResolveReportCommand : ICommand
{
    public required Guid ReportId { get; init; }

    public required ResolveReportRequest Request { get; init; }
}
