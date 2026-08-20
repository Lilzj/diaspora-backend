using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Moderation;

/// <summary>Closes a report, upholding or dismissing it.</summary>
[Authorize(Permission = HousingPermissions.Moderate)]
[Transactional(Reason = "Resolves the report and may hide the listing together.")]
[Auditable]
public sealed record ResolveHousingReportCommand : ICommand<HousingReportResponse>
{
    public required Guid ReportId { get; init; }

    public required ResolveHousingReportRequest Request { get; init; }
}
